data "azurerm_client_config" "current" {}

locals {
  # Short, stable suffix keeps globally unique names (ACR, storage) deterministic per subscription.
  suffix       = substr(md5(data.azurerm_client_config.current.subscription_id), 0, 6)
  base         = "${var.app_name}-${var.environment}"
  compact_base = "${var.app_name}${var.environment}"

  api_app_name      = "${local.base}-api"
  frontend_app_name = "${local.base}-web"

  tags = {
    application = "octocat-supply"
    environment = var.environment
    managed-by  = "terraform"
  }
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.base}"
  location = var.location
  tags     = local.tags
}

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${local.base}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

resource "azurerm_container_registry" "acr" {
  name                = "${local.compact_base}${local.suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "Basic"
  admin_enabled       = false
  tags                = local.tags
}

# Apps pull images with a managed identity, so no registry password exists anywhere.
resource "azurerm_user_assigned_identity" "apps" {
  name                = "id-${local.base}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.tags
}

resource "azurerm_role_assignment" "acr_pull" {
  scope                = azurerm_container_registry.acr.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.apps.principal_id
}

# SQLite database file lives on an Azure Files share mounted into the API.
resource "azurerm_storage_account" "data" {
  name                            = "st${local.compact_base}${local.suffix}"
  location                        = azurerm_resource_group.main.location
  resource_group_name             = azurerm_resource_group.main.name
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  https_traffic_only_enabled      = true
  allow_nested_items_to_be_public = false
  tags                            = local.tags
}

resource "azurerm_storage_share" "data" {
  name               = "octocat-data"
  storage_account_id = azurerm_storage_account.data.id
  quota              = 5
}

resource "azurerm_container_app_environment" "main" {
  name                       = "cae-${local.base}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = local.tags
}

resource "azurerm_container_app_environment_storage" "data" {
  name                         = "data"
  container_app_environment_id = azurerm_container_app_environment.main.id
  account_name                 = azurerm_storage_account.data.name
  share_name                   = azurerm_storage_share.data.name
  access_key                   = azurerm_storage_account.data.primary_access_key
  access_mode                  = "ReadWrite"
}

locals {
  # Both URLs derive from the environment domain so the two apps need no circular references.
  api_host      = "${local.api_app_name}.${azurerm_container_app_environment.main.default_domain}"
  frontend_host = "${local.frontend_app_name}.${azurerm_container_app_environment.main.default_domain}"
}

resource "azurerm_container_app" "api" {
  name                         = local.api_app_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.apps.id]
  }

  registry {
    server   = azurerm_container_registry.acr.login_server
    identity = azurerm_user_assigned_identity.apps.id
  }

  dynamic "secret" {
    for_each = var.payment_api_key == "" ? [] : [1]
    content {
      name  = "payment-api-key"
      value = var.payment_api_key
    }
  }

  secret {
    name  = "appinsights-connection-string"
    value = azurerm_application_insights.main.connection_string
  }

  ingress {
    external_enabled = true
    target_port      = 3000
    transport        = "http"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    # SQLite allows a single writer, so the API must not scale out.
    min_replicas = 1
    max_replicas = 1

    volume {
      name          = "data"
      storage_type  = "AzureFile"
      storage_name  = azurerm_container_app_environment_storage.data.name
      mount_options = "nobrl"
    }

    container {
      name   = "api"
      image  = "${azurerm_container_registry.acr.login_server}/octocat-api:${var.image_tag}"
      cpu    = 0.5
      memory = "1Gi"

      volume_mounts {
        name = "data"
        path = "/data"
      }

      env {
        name  = "PORT"
        value = "3000"
      }
      env {
        name  = "DB_FILE"
        value = "/data/app.db"
      }
      env {
        name  = "SQLITE_JOURNAL_MODE"
        value = "DELETE"
      }
      env {
        name  = "API_CORS_ORIGINS"
        value = "https://${local.frontend_host}"
      }
      env {
        name  = "PAYMENT_PROVIDER"
        value = var.payment_provider
      }
      env {
        name  = "PAYMENT_CURRENCY"
        value = var.payment_currency
      }
      dynamic "env" {
        for_each = var.payment_api_key == "" ? [] : [1]
        content {
          name        = "PAYMENT_API_KEY"
          secret_name = "payment-api-key"
        }
      }

      env {
        name        = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        secret_name = "appinsights-connection-string"
      }

      liveness_probe {
        transport = "HTTP"
        port      = 3000
        path      = "/health"
      }
      readiness_probe {
        transport = "HTTP"
        port      = 3000
        path      = "/ready"
      }
    }
  }

  depends_on = [azurerm_role_assignment.acr_pull]
}

resource "azurerm_container_app" "frontend" {
  name                         = local.frontend_app_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.apps.id]
  }

  registry {
    server   = azurerm_container_registry.acr.login_server
    identity = azurerm_user_assigned_identity.apps.id
  }

  ingress {
    external_enabled = true
    target_port      = 80
    transport        = "http"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = 1
    max_replicas = 2

    container {
      name   = "web"
      image  = "${azurerm_container_registry.acr.login_server}/octocat-web:${var.image_tag}"
      cpu    = 0.25
      memory = "0.5Gi"

      # The image's entrypoint writes runtime-config.js from these values at startup.
      env {
        name  = "API_HOST"
        value = local.api_host
      }
      env {
        name  = "API_PORT"
        value = "443"
      }
      env {
        name  = "API_PROTOCOL"
        value = "https"
      }

      liveness_probe {
        transport = "HTTP"
        port      = 80
        path      = "/"
      }
    }
  }

  depends_on = [azurerm_role_assignment.acr_pull]
}

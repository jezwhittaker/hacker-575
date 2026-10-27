terraform {
  required_version = ">= 1.6.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }

  # Partial configuration: the storage account, container and key are supplied with
  # -backend-config at init time (see infra/README.md and the deploy workflow).
  backend "azurerm" {
    use_oidc         = true
    use_azuread_auth = true
  }
}

# Authentication comes from the environment (ARM_CLIENT_ID, ARM_TENANT_ID,
# ARM_SUBSCRIPTION_ID, ARM_USE_OIDC) so no credentials live in source.
provider "azurerm" {
  features {}
  use_oidc = true
}

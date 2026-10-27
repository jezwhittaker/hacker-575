output "resource_group_name" {
  value = azurerm_resource_group.main.name
}

output "acr_name" {
  value = azurerm_container_registry.acr.name
}

output "acr_login_server" {
  value = azurerm_container_registry.acr.login_server
}

output "api_url" {
  value = "https://${local.api_host}"
}

output "frontend_url" {
  value = "https://${local.frontend_host}"
}

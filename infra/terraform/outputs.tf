output "api_url" {
  description = "Public URL of the FocusBot API"
  value       = "https://${azurerm_container_app.api.ingress[0].fqdn}"
}

output "acr_login_server" {
  description = "ACR login server for Docker push"
  value       = azurerm_container_registry.acr.login_server
}

output "resource_group_name" {
  description = "Name of the deployed resource group"
  value       = azurerm_resource_group.main.name
}

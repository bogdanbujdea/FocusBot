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

output "static_web_app_api_key" {
  description = "Deployment token for Azure Static Web App (used by Azure/static-web-apps-deploy)"
  value       = azurerm_static_web_app.website.api_key
  sensitive   = true
}

output "static_web_app_url" {
  description = "Default HTTPS URL of the Static Web App"
  value       = "https://${azurerm_static_web_app.website.default_host_name}"
}

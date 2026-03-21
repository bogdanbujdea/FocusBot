variable "resource_group_name" {
  description = "Name of the Azure resource group"
  type        = string
  default     = "foqus-me-rg"
}

variable "location" {
  description = "Azure region for all resources"
  type        = string
  # Match current deployed production locations to avoid Terraform
  # replacing/destroying resources when CI doesn't pass this variable.
  default     = "westeurope"
}

variable "postgresql_location" {
  description = "Azure region for PostgreSQL Flexible Server (can differ from `location`)"
  type        = string
  # Match current deployed production locations to avoid replacements.
  default     = "northeurope"
}

variable "environment" {
  description = "Environment name (e.g. prod, staging)"
  type        = string
  default     = "prod"
}

variable "postgresql_admin_password" {
  description = "Administrator password for PostgreSQL Flexible Server"
  type        = string
  sensitive   = true
}

variable "supabase_url" {
  description = "Supabase project URL"
  type        = string
  sensitive   = true
}

variable "supabase_jwt_secret" {
  description = "Supabase JWT secret for token validation"
  type        = string
  sensitive   = true
}

variable "managed_openai_key" {
  description = "API key for managed OpenAI service"
  type        = string
  sensitive   = true
  default     = ""
}

variable "paddle_webhook_secret" {
  description = "Paddle webhook verification secret"
  type        = string
  sensitive   = true
  default     = ""
}

variable "container_image_tag" {
  description = "Docker image tag to deploy (typically a git SHA)"
  type        = string
  default     = "latest"
}

variable "static_web_app_name" {
  description = "Name of the Azure Static Web App"
  type        = string
  default     = "foqus-website"
}

variable "static_web_app_sku" {
  description = "SKU for Static Web App (Free or Standard)"
  type        = string
  default     = "Free"
}

variable "domain_name" {
  description = "Root domain name (used for foqus.me and api.foqus.me)"
  type        = string
  default     = "foqus.me"
}

variable "cloudflare_api_token" {
  description = "Cloudflare API token with Zone:Edit permissions"
  type        = string
  sensitive   = true
}

variable "cloudflare_zone_id" {
  description = "Cloudflare Zone ID for the root domain"
  type        = string
}

variable "mailerlite_api_key" {
  description = "MailerLite API key (used by the API container)"
  type        = string
  sensitive   = true
  default     = ""
}

variable "mailerlite_waitlist_group_id" {
  description = "MailerLite group ID for waitlist subscribers (API container)"
  type        = string
  sensitive   = true
  default     = ""
}

variable "web_app_name" {
  description = "Name of the Azure Static Web App for the dashboard (app.foqus.me)"
  type        = string
  default     = "foqus-web-app"
}

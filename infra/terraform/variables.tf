variable "resource_group_name" {
  description = "Name of the Azure resource group"
  type        = string
  default     = "focusbot-prod"
}

variable "location" {
  description = "Azure region for all resources"
  type        = string
  default     = "westeurope"
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

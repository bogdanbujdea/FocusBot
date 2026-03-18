terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    cloudflare = {
      source = "cloudflare/cloudflare"
    }
    azapi = {
      source = "azure/azapi"
    }
    time = {
      source = "hashicorp/time"
    }
  }
  backend "azurerm" {
    # Configure via `terraform init -backend-config=backend.prod.conf`
    # (and `backend.staging.conf` when staging is added).
  }
}

provider "azurerm" {
  features {}
}

provider "cloudflare" {
  api_token = var.cloudflare_api_token
}

provider "azapi" {}

locals {
  api_domain = "api.${var.domain_name}"
}

# ── Resource Group ──────────────────────────────────────────────────────────

resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location
}

# ── Container Registry ─────────────────────────────────────────────────────

resource "azurerm_container_registry" "acr" {
  name                = "focusbotacr${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  admin_enabled       = true
}

# ── Log Analytics (for Container Apps) ─────────────────────────────────────

resource "azurerm_log_analytics_workspace" "main" {
  name                = "focusbot-logs-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

# ── Container Apps Environment ─────────────────────────────────────────────

resource "azurerm_container_app_environment" "main" {
  name                       = "focusbot-cae-${var.environment}"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
}

# ── Container App ──────────────────────────────────────────────────────────

resource "azurerm_container_app" "api" {
  name                         = "focusbot-api-${var.environment}"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  revision_mode                = "Single"

  template {
    min_replicas = 0
    max_replicas = 2

    container {
      name   = "focusbot-api"
      image  = "${azurerm_container_registry.acr.login_server}/focusbot-api:${var.container_image_tag}"
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "ConnectionStrings__DefaultConnection"
        value = "Host=${azurerm_postgresql_flexible_server.main.fqdn};Database=focusbot;Username=focusadmin;Password=${var.postgresql_admin_password};SSL Mode=Require"
      }

      env {
        name  = "Supabase__Url"
        value = var.supabase_url
      }

      env {
        name  = "Supabase__JwtSecret"
        value = var.supabase_jwt_secret
      }

      env {
        name  = "ManagedOpenAiKey"
        value = var.managed_openai_key
      }

      env {
        name  = "Paddle__WebhookSecret"
        value = var.paddle_webhook_secret
      }

      env {
        name  = "MailerLite__ApiKey"
        value = var.mailerlite_api_key
      }

      liveness_probe {
        transport = "HTTP"
        path      = "/health"
        port      = 8080
      }

      readiness_probe {
        transport = "HTTP"
        path      = "/health"
        port      = 8080
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}

# ── Static Web App + Custom Domain (foqus.me) ───────────────────────────────

resource "azurerm_static_web_app" "website" {
  name                = "${var.static_web_app_name}-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  sku_tier = var.static_web_app_sku
  sku_size = var.static_web_app_sku
}

resource "cloudflare_record" "website_cname" {
  zone_id = var.cloudflare_zone_id
  name    = "@"
  content = azurerm_static_web_app.website.default_host_name
  type    = "CNAME"
  proxied = false
  ttl     = 1
}

resource "azurerm_static_web_app_custom_domain" "root" {
  static_web_app_id = azurerm_static_web_app.website.id
  domain_name       = var.domain_name
  validation_type   = "cname-delegation"

  depends_on = [cloudflare_record.website_cname]
}

# ── Container App Custom Domain (api.foqus.me) ─────────────────────────────

data "azapi_resource" "container_app_custom_domain_verification_id" {
  type       = "Microsoft.App/managedEnvironments@2023-05-01"
  resource_id = azurerm_container_app_environment.main.id

  response_export_values = [
    "properties.customDomainConfiguration.customDomainVerificationId"
  ]
}

locals {
  api_custom_domain_verification_id = jsondecode(data.azapi_resource.container_app_custom_domain_verification_id.output).properties.customDomainConfiguration.customDomainVerificationId
}

resource "cloudflare_record" "api_cname" {
  zone_id = var.cloudflare_zone_id
  name    = "api"
  content = azurerm_container_app.api.ingress[0].fqdn
  type    = "CNAME"
  proxied = false
  ttl     = 1
}

resource "cloudflare_record" "api_txt" {
  zone_id = var.cloudflare_zone_id
  name    = "asuid.api"
  content = local.api_custom_domain_verification_id
  type    = "TXT"
  proxied = false
  ttl     = 1
}

resource "time_sleep" "api_dns_propagation" {
  create_duration = "60s"
  depends_on      = [cloudflare_record.api_cname, cloudflare_record.api_txt]
}

# Step 1: register the hostname in the container app environment (Disabled binding)
resource "azapi_update_resource" "api_custom_domain_disabled" {
  type        = "Microsoft.App/containerApps@2023-05-01"
  resource_id = azurerm_container_app.api.id
  depends_on  = [time_sleep.api_dns_propagation]

  body = jsonencode({
    properties = {
      configuration = {
        ingress = {
          customDomains = [
            {
              bindingType = "Disabled"
              name        = local.api_domain
            }
          ]
        }
      }
    }
  })
}

# Step 2: create a managed certificate for the hostname
resource "azapi_resource" "api_managed_certificate" {
  type      = "Microsoft.App/ManagedEnvironments/managedCertificates@2023-05-01"
  name      = "api-${var.environment}-cert"
  parent_id = azurerm_container_app_environment.main.id
  location  = azurerm_resource_group.main.location

  depends_on = [azapi_update_resource.api_custom_domain_disabled]

  body = jsonencode({
    properties = {
      subjectName             = local.api_domain
      domainControlValidation = "CNAME"
    }
  })
}

# Step 3: bind the hostname to the managed certificate (SNI enabled)
resource "azapi_update_resource" "api_custom_domain_binding" {
  type        = "Microsoft.App/containerApps@2023-05-01"
  resource_id = azurerm_container_app.api.id
  depends_on  = [azapi_resource.api_managed_certificate]

  body = jsonencode({
    properties = {
      configuration = {
        ingress = {
          customDomains = [
            {
              bindingType   = "SniEnabled"
              name          = local.api_domain
              certificateId = jsondecode(azapi_resource.api_managed_certificate.output).id
            }
          ]
        }
      }
    }
  })
}

# ── PostgreSQL Flexible Server ─────────────────────────────────────────────

resource "azurerm_postgresql_flexible_server" "main" {
  name                          = "focusbot-pg-${var.environment}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  version                       = "16"
  administrator_login           = "focusadmin"
  administrator_password        = var.postgresql_admin_password
  sku_name                      = "B_Standard_B1ms"
  storage_mb                    = 32768
  public_network_access_enabled = true
  zone                          = "1"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "allow_azure" {
  name             = "AllowAzureServices"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# ── PostgreSQL Database ────────────────────────────────────────────────────

resource "azurerm_postgresql_flexible_server_database" "focusbot" {
  name      = "focusbot"
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

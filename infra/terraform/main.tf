terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
  backend "azurerm" {
    resource_group_name  = "focusbot-tfstate"
    storage_account_name = "focusbottfstate"
    container_name       = "tfstate"
    key                  = "focusbot.terraform.tfstate"
  }
}

provider "azurerm" {
  features {}
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

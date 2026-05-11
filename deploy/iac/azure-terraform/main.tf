terraform {
  required_version = ">= 1.7"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.100"
    }
  }
  backend "azurerm" {
    resource_group_name  = "rg-terraform-state"
    storage_account_name = "tfstatesaasbuilder"
    container_name       = "tfstate"
    key                  = "saasbuilder.tfstate"
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = false
    }
  }
}

locals {
  resource_prefix = "${var.base_name}-${var.environment}"
  tags = {
    environment = var.environment
    project     = "saasbuilder"
  }
}

# ── Resource Group ────────────────────────────────────────────────────────────
resource "azurerm_resource_group" "main" {
  name     = "rg-${local.resource_prefix}"
  location = var.location
  tags     = local.tags
}

# ── Container Registry ────────────────────────────────────────────────────────
resource "azurerm_container_registry" "acr" {
  name                = replace("${local.resource_prefix}acr", "-", "")
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
  sku                 = "Basic"
  admin_enabled       = false
  tags                = local.tags
}

# ── Key Vault ─────────────────────────────────────────────────────────────────
data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "kv" {
  name                       = "${local.resource_prefix}-kv"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = var.location
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 90
  enable_rbac_authorization  = true
  tags                       = local.tags
}

# ── Storage Account ───────────────────────────────────────────────────────────
resource "azurerm_storage_account" "sa" {
  name                     = replace("${local.resource_prefix}sa", "-", "")
  resource_group_name      = azurerm_resource_group.main.name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
  allow_nested_items_to_be_public = false
  tags                     = local.tags
}

# ── Service Bus ───────────────────────────────────────────────────────────────
resource "azurerm_servicebus_namespace" "sb" {
  name                = "${local.resource_prefix}-sb"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
  sku                 = "Standard"
  tags                = local.tags
}

# ── Postgres Flexible Server ──────────────────────────────────────────────────
resource "azurerm_postgresql_flexible_server" "pg" {
  name                   = "${local.resource_prefix}-pg"
  resource_group_name    = azurerm_resource_group.main.name
  location               = var.location
  version                = "16"
  administrator_login    = var.postgres_admin_user
  administrator_password = var.postgres_admin_password
  sku_name               = "B_Standard_B2ms"
  storage_mb             = 32768
  backup_retention_days  = 7
  tags                   = local.tags
}

resource "azurerm_postgresql_flexible_server_database" "db" {
  name      = "saasbuilder"
  server_id = azurerm_postgresql_flexible_server.pg.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

# ── App Service Plan ──────────────────────────────────────────────────────────
resource "azurerm_service_plan" "asp" {
  name                = "${local.resource_prefix}-asp"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
  os_type             = "Linux"
  sku_name            = "B2"
  tags                = local.tags
}

# ── Web App ───────────────────────────────────────────────────────────────────
resource "azurerm_linux_web_app" "app" {
  name                = "${local.resource_prefix}-app"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.location
  service_plan_id     = azurerm_service_plan.asp.id
  https_only          = true
  tags                = local.tags

  identity {
    type = "SystemAssigned"
  }

  site_config {
    health_check_path = "/health/live"
    application_stack {
      docker_image_name = var.docker_image
    }
  }

  app_settings = {
    ASPNETCORE_ENVIRONMENT = "Production"
    ASPNETCORE_HTTP_PORTS  = "8080"
    AZURE_KEY_VAULT_URI    = azurerm_key_vault.kv.vault_uri
  }
}

# Grant Web App managed identity read access to Key Vault secrets
resource "azurerm_role_assignment" "kv_secrets_user" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app.app.identity[0].principal_id
}

# ── Outputs ───────────────────────────────────────────────────────────────────
output "web_app_url" {
  value = "https://${azurerm_linux_web_app.app.default_hostname}"
}
output "acr_login_server" {
  value = azurerm_container_registry.acr.login_server
}
output "postgres_host" {
  value     = azurerm_postgresql_flexible_server.pg.fqdn
  sensitive = true
}

resource "random_string" "suffix" {
  length  = 6
  upper   = false
  special = false
}

locals {
  prefix = "${var.project_name}-${var.environment_name}"
  suffix = random_string.suffix.result
  tags = merge(var.tags, {
    environment = var.environment_name
  })
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.prefix}"
  location = var.location
  tags     = local.tags
}

# Milestone 1: PostgreSQL
resource "azurerm_postgresql_flexible_server" "main" {
  name                          = "psql-${local.prefix}-${local.suffix}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  version                       = "16"
  administrator_login           = var.postgres_admin_username
  administrator_password        = var.postgres_admin_password
  sku_name                      = "B_Standard_B1ms"
  storage_mb                    = 32768
  backup_retention_days         = 7
  geo_redundant_backup_enabled  = false
  public_network_access_enabled = true
  tags                          = local.tags

  lifecycle {
    # Azure assigns a zone when none is requested. Ignore that service-managed
    # value so later applies do not attempt an unsupported zone reset.
    ignore_changes = [zone]
  }
}

resource "azurerm_postgresql_flexible_server_database" "dev" {
  name      = "training_dev"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "UTF8"
}

resource "azurerm_postgresql_flexible_server_database" "test" {
  name      = "training_test"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "UTF8"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "developer" {
  name             = "developer-ip"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = var.developer_public_ip
  end_ip_address   = var.developer_public_ip
}

# App Service and Flex Consumption have dynamic outbound addresses. This rule is
# only added with the optional compute milestone. Replace it with private
# networking before using the platform for non-demo data.
resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  count = var.deploy_compute ? 1 : 0

  name             = "allow-azure-services"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# Milestone 2: Service Bus
resource "azurerm_servicebus_namespace" "main" {
  name                          = "sb-${local.prefix}-${local.suffix}"
  location                      = azurerm_resource_group.main.location
  resource_group_name           = azurerm_resource_group.main.name
  sku                           = "Standard"
  minimum_tls_version           = "1.2"
  local_auth_enabled            = true
  public_network_access_enabled = true
  tags                          = local.tags
}

resource "azurerm_servicebus_topic" "course_completed" {
  name                  = "course-completed"
  namespace_id          = azurerm_servicebus_namespace.main.id
  partitioning_enabled  = true
  max_size_in_megabytes = 1024
  default_message_ttl   = "P14D"
  support_ordering      = false
}

resource "azurerm_servicebus_subscription" "consumers" {
  for_each = toset(["certificate", "reporting", "notification"])

  name                                      = each.key
  topic_id                                  = azurerm_servicebus_topic.course_completed.id
  max_delivery_count                        = 5
  lock_duration                             = "PT1M"
  default_message_ttl                       = "P14D"
  dead_lettering_on_message_expiration      = true
  dead_lettering_on_filter_evaluation_error = true
}

resource "azurerm_servicebus_namespace_authorization_rule" "local_development" {
  name         = "local-development"
  namespace_id = azurerm_servicebus_namespace.main.id
  listen       = true
  send         = true
  manage       = false
}

# Milestone 3: storage and observability
resource "azurerm_storage_account" "main" {
  name                            = "st${substr(var.project_name, 0, 12)}${var.environment_name}${local.suffix}"
  resource_group_name             = azurerm_resource_group.main.name
  location                        = azurerm_resource_group.main.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  public_network_access_enabled   = true
  tags                            = local.tags
}

resource "azurerm_storage_container" "certificates" {
  name                  = "certificates"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${local.prefix}-${local.suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

resource "azurerm_application_insights" "main" {
  name                = "appi-${local.prefix}-${local.suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = local.tags
}

resource "azurerm_consumption_budget_resource_group" "main" {
  count = var.budget_notification_email == null ? 0 : 1

  name              = "budget-${local.prefix}"
  resource_group_id = azurerm_resource_group.main.id
  amount            = var.monthly_budget_amount
  time_grain        = "Monthly"

  time_period {
    start_date = "2026-07-01T00:00:00Z"
    end_date   = "2030-07-01T00:00:00Z"
  }

  notification {
    enabled        = true
    threshold      = 80
    operator       = "GreaterThan"
    threshold_type = "Actual"
    contact_emails = [var.budget_notification_email]
  }
}

# Milestone 4: compute. Nothing below is created unless deploy_compute=true.
resource "azurerm_key_vault" "main" {
  count = var.deploy_compute ? 1 : 0

  name                       = "kv-${substr(local.prefix, 0, 14)}-${local.suffix}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true
  purge_protection_enabled   = false
  soft_delete_retention_days = 7
  tags                       = local.tags
}

resource "azurerm_role_assignment" "terraform_secrets_officer" {
  count = var.deploy_compute ? 1 : 0

  scope                = azurerm_key_vault.main[0].id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_key_vault_secret" "postgres_connection" {
  count = var.deploy_compute ? 1 : 0

  name         = "postgres-connection"
  key_vault_id = azurerm_key_vault.main[0].id
  value        = "Host=${azurerm_postgresql_flexible_server.main.fqdn};Port=5432;Database=training_dev;Username=${var.postgres_admin_username};Password=${var.postgres_admin_password};SSL Mode=Require;Trust Server Certificate=false"
  depends_on   = [azurerm_role_assignment.terraform_secrets_officer]
}

resource "azurerm_service_plan" "api" {
  count = var.deploy_compute ? 1 : 0

  name                = "plan-api-${local.prefix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = "B1"
  tags                = local.tags
}

resource "azurerm_linux_web_app" "api" {
  count = var.deploy_compute ? 1 : 0

  name                = "app-api-${local.prefix}-${local.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.api[0].id
  https_only          = true
  tags                = local.tags

  identity {
    type = "SystemAssigned"
  }

  site_config {
    always_on                         = true
    health_check_path                 = "/health"
    health_check_eviction_time_in_min = 5
    minimum_tls_version               = "1.2"
    ftps_state                        = "Disabled"
    application_stack {
      dotnet_version = "10.0"
    }

    cors {
      allowed_origins = concat(
        [
          "http://localhost:5173",
          "https://${azurerm_static_web_app.web[0].default_host_name}"
        ],
        var.additional_allowed_origins
      )
      support_credentials = false
    }
  }

  app_settings = {
    "ASPNETCORE_ENVIRONMENT"                = "Production"
    "ConnectionStrings__Postgres"           = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.postgres_connection[0].versionless_id})"
    "ServiceBus__Enabled"                   = "true"
    "ServiceBus__FullyQualifiedNamespace"   = trimsuffix(trimprefix(azurerm_servicebus_namespace.main.endpoint, "sb://"), "/")
    "ServiceBus__TopicName"                 = azurerm_servicebus_topic.course_completed.name
    "Storage__ServiceUri"                   = azurerm_storage_account.main.primary_blob_endpoint
    "Storage__CertificateContainer"         = azurerm_storage_container.certificates.name
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.main.connection_string
  }
}

resource "azurerm_storage_container" "function_releases" {
  count = var.deploy_compute ? 1 : 0

  name                  = "function-releases"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_service_plan" "functions" {
  count = var.deploy_compute ? 1 : 0

  name                = "plan-func-${local.prefix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = "FC1"
  tags                = local.tags
}

resource "azurerm_function_app_flex_consumption" "consumers" {
  count = var.deploy_compute ? 1 : 0

  name                        = "func-${local.prefix}-${local.suffix}"
  resource_group_name         = azurerm_resource_group.main.name
  location                    = azurerm_resource_group.main.location
  service_plan_id             = azurerm_service_plan.functions[0].id
  storage_container_type      = "blobContainer"
  storage_container_endpoint  = "${azurerm_storage_account.main.primary_blob_endpoint}${azurerm_storage_container.function_releases[0].name}"
  storage_authentication_type = "StorageAccountConnectionString"
  storage_access_key          = azurerm_storage_account.main.primary_access_key
  runtime_name                = "dotnet-isolated"
  runtime_version             = "10.0"
  maximum_instance_count      = 20
  instance_memory_in_mb       = 2048
  tags                        = local.tags

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_insights_connection_string = azurerm_application_insights.main.connection_string
  }

  app_settings = {
    "PostgresConnection"                            = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.postgres_connection[0].versionless_id})"
    "ServiceBusConnection__fullyQualifiedNamespace" = trimsuffix(trimprefix(azurerm_servicebus_namespace.main.endpoint, "sb://"), "/")
    "Storage__ServiceUri"                           = azurerm_storage_account.main.primary_blob_endpoint
    "Storage__CertificateContainer"                 = azurerm_storage_container.certificates.name
  }
}

resource "azurerm_static_web_app" "web" {
  count = var.deploy_compute ? 1 : 0

  name                = "swa-${local.prefix}-${local.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku_tier            = "Free"
  sku_size            = "Free"
  tags                = local.tags
}

resource "azurerm_role_assignment" "api_servicebus_sender" {
  count = var.deploy_compute ? 1 : 0

  scope                = azurerm_servicebus_namespace.main.id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = azurerm_linux_web_app.api[0].identity[0].principal_id
}

resource "azurerm_role_assignment" "api_blob_reader" {
  count = var.deploy_compute ? 1 : 0

  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Reader"
  principal_id         = azurerm_linux_web_app.api[0].identity[0].principal_id
}

resource "azurerm_role_assignment" "api_keyvault_reader" {
  count = var.deploy_compute ? 1 : 0

  scope                = azurerm_key_vault.main[0].id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app.api[0].identity[0].principal_id
}

resource "azurerm_role_assignment" "functions_servicebus_receiver" {
  count = var.deploy_compute ? 1 : 0

  scope                = azurerm_servicebus_namespace.main.id
  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = azurerm_function_app_flex_consumption.consumers[0].identity[0].principal_id
}

resource "azurerm_role_assignment" "functions_blob_contributor" {
  count = var.deploy_compute ? 1 : 0

  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_function_app_flex_consumption.consumers[0].identity[0].principal_id
}

resource "azurerm_role_assignment" "functions_keyvault_reader" {
  count = var.deploy_compute ? 1 : 0

  scope                = azurerm_key_vault.main[0].id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_function_app_flex_consumption.consumers[0].identity[0].principal_id
}

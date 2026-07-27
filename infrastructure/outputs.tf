output "resource_group_name" {
  value = azurerm_resource_group.main.name
}

output "postgres_host" {
  value = azurerm_postgresql_flexible_server.main.fqdn
}

output "postgres_dev_database" {
  value = azurerm_postgresql_flexible_server_database.dev.name
}

output "postgres_test_database" {
  value = azurerm_postgresql_flexible_server_database.test.name
}

output "postgres_dev_connection_string" {
  value     = "Host=${azurerm_postgresql_flexible_server.main.fqdn};Port=5432;Database=training_dev;Username=${var.postgres_admin_username};Password=${var.postgres_admin_password};SSL Mode=Require;Trust Server Certificate=false"
  sensitive = true
}

output "postgres_test_connection_string" {
  value     = "Host=${azurerm_postgresql_flexible_server.main.fqdn};Port=5432;Database=training_test;Username=${var.postgres_admin_username};Password=${var.postgres_admin_password};SSL Mode=Require;Trust Server Certificate=false"
  sensitive = true
}

output "service_bus_namespace" {
  value = azurerm_servicebus_namespace.main.name
}

output "service_bus_fully_qualified_namespace" {
  value = trimsuffix(trimprefix(azurerm_servicebus_namespace.main.endpoint, "sb://"), "/")
}

output "service_bus_topic" {
  value = azurerm_servicebus_topic.course_completed.name
}

output "service_bus_local_connection_string" {
  value     = azurerm_servicebus_namespace_authorization_rule.local_development.primary_connection_string
  sensitive = true
}

output "storage_account_name" {
  value = azurerm_storage_account.main.name
}

output "storage_connection_string" {
  value     = azurerm_storage_account.main.primary_connection_string
  sensitive = true
}

output "certificate_container" {
  value = azurerm_storage_container.certificates.name
}

output "api_url" {
  value = var.deploy_compute ? "https://${azurerm_linux_web_app.api[0].default_hostname}" : null
}

output "functions_name" {
  value = var.deploy_compute ? azurerm_function_app_flex_consumption.consumers[0].name : null
}

output "static_web_app_url" {
  value = var.deploy_compute ? "https://${azurerm_static_web_app.web[0].default_host_name}" : null
}


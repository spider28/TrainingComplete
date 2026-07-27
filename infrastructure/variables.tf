variable "subscription_id" {
  description = "Azure subscription ID. Azure CLI context is used when null."
  type        = string
  default     = null
  nullable    = true
}

variable "location" {
  description = "Azure region for regional resources."
  type        = string
  default     = "centralus"
}

variable "project_name" {
  description = "Short lowercase project name used in resource names."
  type        = string
  default     = "trainingcomplete"

  validation {
    condition     = can(regex("^[a-z0-9]{3,18}$", var.project_name))
    error_message = "project_name must contain 3-18 lowercase letters or digits."
  }
}

variable "environment_name" {
  description = "Environment name such as dev, test, or prod."
  type        = string
  default     = "dev"
}

variable "postgres_admin_username" {
  description = "PostgreSQL administrator username."
  type        = string
  default     = "trainingadmin"
}

variable "postgres_admin_password" {
  description = "PostgreSQL administrator password. Set with TF_VAR_postgres_admin_password."
  type        = string
  sensitive   = true
}

variable "developer_public_ip" {
  description = "Single IPv4 address allowed to connect to PostgreSQL."
  type        = string

  validation {
    condition     = can(cidrhost("${var.developer_public_ip}/32", 0))
    error_message = "developer_public_ip must be a valid IPv4 address."
  }
}

variable "budget_notification_email" {
  description = "Optional address for an 80 percent resource-group budget alert."
  type        = string
  default     = null
  nullable    = true
}

variable "monthly_budget_amount" {
  description = "Monthly budget amount in the subscription billing currency."
  type        = number
  default     = 30
}

variable "deploy_compute" {
  description = "Explicitly enable App Service, Functions, Static Web Apps, and Key Vault."
  type        = bool
  default     = false
}

variable "additional_allowed_origins" {
  description = "Additional browser origins allowed by the deployed API."
  type        = list(string)
  default     = []
}

variable "tags" {
  description = "Tags applied to Azure resources."
  type        = map(string)
  default = {
    application = "training-completion-platform"
    managed-by  = "terraform"
    purpose     = "learning"
  }
}


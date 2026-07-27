terraform {
  required_version = "~> 1.15.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.0"
    }
  }

  # Start with local state. During phase 6, uncomment this block and run:
  # terraform init -migrate-state -backend-config=backend.hcl
  # backend "azurerm" {}
}


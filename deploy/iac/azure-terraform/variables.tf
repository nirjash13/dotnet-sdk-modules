variable "environment" {
  description = "Environment name (dev, staging, production)"
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "eastus"
}

variable "base_name" {
  description = "Base name prefix for all resources"
  type        = string
  default     = "saasbuilder"
}

variable "postgres_admin_user" {
  description = "Postgres administrator login"
  type        = string
  default     = "saasadmin"
}

variable "postgres_admin_password" {
  description = "Postgres administrator password"
  type        = string
  sensitive   = true
}

variable "docker_image" {
  description = "Docker image (repository:tag)"
  type        = string
  default     = "ghcr.io/placeholder/saasbuilder:latest"
}

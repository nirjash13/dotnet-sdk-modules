variable "gcp_project" {
  description = "GCP project ID"
  type        = string
}

variable "gcp_region" {
  description = "GCP region"
  type        = string
  default     = "us-central1"
}

variable "environment" {
  description = "Environment name (dev, staging, production)"
  type        = string
  default     = "dev"
}

variable "base_name" {
  description = "Base name prefix for all resources"
  type        = string
  default     = "saasbuilder"
}

variable "postgres_admin_user" {
  description = "Cloud SQL administrator login"
  type        = string
  default     = "saasadmin"
}

variable "postgres_admin_password" {
  description = "Cloud SQL administrator password"
  type        = string
  sensitive   = true
}

variable "docker_image" {
  description = "Docker image (Artifact Registry URI:tag)"
  type        = string
}

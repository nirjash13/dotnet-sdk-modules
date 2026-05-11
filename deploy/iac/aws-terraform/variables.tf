variable "environment" {
  description = "Environment name (dev, staging, production)"
  type        = string
  default     = "dev"
}

variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "us-east-1"
}

variable "base_name" {
  description = "Base name prefix for all resources"
  type        = string
  default     = "saasbuilder"
}

variable "postgres_admin_user" {
  description = "RDS administrator login"
  type        = string
  default     = "saasadmin"
}

variable "postgres_admin_password" {
  description = "RDS administrator password"
  type        = string
  sensitive   = true
}

variable "docker_image" {
  description = "Docker image (ECR URI:tag)"
  type        = string
}

variable "private_subnet_ids" {
  description = "List of private subnet IDs for RDS subnet group"
  type        = list(string)
  default     = []
}

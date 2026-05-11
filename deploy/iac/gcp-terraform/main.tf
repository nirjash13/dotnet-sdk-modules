terraform {
  required_version = ">= 1.7"
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.30"
    }
  }
  backend "gcs" {
    bucket = "saasbuilder-tfstate"
    prefix = "terraform/state"
  }
}

provider "google" {
  project = var.gcp_project
  region  = var.gcp_region
}

locals {
  resource_prefix = "${var.base_name}-${var.environment}"
  labels = {
    environment = var.environment
    project     = "saasbuilder"
  }
}

# ── Enable required APIs ──────────────────────────────────────────────────────
resource "google_project_service" "apis" {
  for_each = toset([
    "run.googleapis.com",
    "sqladmin.googleapis.com",
    "secretmanager.googleapis.com",
    "pubsub.googleapis.com",
    "storage.googleapis.com",
    "artifactregistry.googleapis.com",
  ])
  service            = each.value
  disable_on_destroy = false
}

# ── Artifact Registry (Docker) ────────────────────────────────────────────────
resource "google_artifact_registry_repository" "app" {
  location      = var.gcp_region
  repository_id = local.resource_prefix
  description   = "SaasBuilder Docker images"
  format        = "DOCKER"
  labels        = local.labels
  depends_on    = [google_project_service.apis]
}

# ── Secret Manager ─────────────────────────────────────────────────────────────
resource "google_secret_manager_secret" "app" {
  secret_id = "${local.resource_prefix}-app"
  labels    = local.labels
  replication { auto {} }
  depends_on = [google_project_service.apis]
}

# ── GCS Bucket (blob storage) ─────────────────────────────────────────────────
resource "google_storage_bucket" "blobs" {
  name          = "${local.resource_prefix}-blobs"
  location      = var.gcp_region
  force_destroy = var.environment != "production"
  uniform_bucket_level_access = true
  labels        = local.labels

  versioning { enabled = true }

  lifecycle_rule {
    condition { age = 365 }
    action { type = "Delete" }
  }

  depends_on = [google_project_service.apis]
}

# ── Pub/Sub (message bus) ─────────────────────────────────────────────────────
resource "google_pubsub_topic" "main" {
  name   = local.resource_prefix
  labels = local.labels
  depends_on = [google_project_service.apis]
}

# ── Cloud SQL (Postgres 16) ───────────────────────────────────────────────────
resource "google_sql_database_instance" "pg" {
  name             = "${local.resource_prefix}-pg"
  database_version = "POSTGRES_16"
  region           = var.gcp_region
  deletion_protection = var.environment == "production"

  settings {
    tier = "db-f1-micro"
    backup_configuration {
      enabled                        = true
      point_in_time_recovery_enabled = true
    }
    database_flags {
      name  = "cloudsql.enable_pg_cron"
      value = "on"
    }
  }

  depends_on = [google_project_service.apis]
}

resource "google_sql_database" "db" {
  name     = "saasbuilder"
  instance = google_sql_database_instance.pg.name
}

resource "google_sql_user" "app" {
  name     = var.postgres_admin_user
  instance = google_sql_database_instance.pg.name
  password = var.postgres_admin_password
}

# ── Service Account for Cloud Run ─────────────────────────────────────────────
resource "google_service_account" "run" {
  account_id   = "${local.resource_prefix}-run"
  display_name = "SaasBuilder Cloud Run Service Account"
}

resource "google_project_iam_member" "run_secret_accessor" {
  project = var.gcp_project
  role    = "roles/secretmanager.secretAccessor"
  member  = "serviceAccount:${google_service_account.run.email}"
}

resource "google_project_iam_member" "run_storage_object" {
  project = var.gcp_project
  role    = "roles/storage.objectAdmin"
  member  = "serviceAccount:${google_service_account.run.email}"
}

# ── Cloud Run Service ─────────────────────────────────────────────────────────
resource "google_cloud_run_v2_service" "app" {
  name     = local.resource_prefix
  location = var.gcp_region
  labels   = local.labels

  template {
    service_account = google_service_account.run.email
    scaling {
      min_instance_count = 1
      max_instance_count = 10
    }
    containers {
      image = var.docker_image
      ports { container_port = 8080 }
      resources {
        limits   = { cpu = "1", memory = "512Mi" }
        startup_cpu_boost = true
      }
      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }
      env {
        name  = "ASPNETCORE_HTTP_PORTS"
        value = "8080"
      }
      startup_probe {
        http_get { path = "/health/startup" port = 8080 }
        initial_delay_seconds = 5
        period_seconds        = 5
        failure_threshold     = 30
      }
      liveness_probe {
        http_get { path = "/health/live" port = 8080 }
        period_seconds = 10
      }
    }
  }

  depends_on = [google_project_service.apis]
}

# Allow unauthenticated invocations (public SaaS)
resource "google_cloud_run_service_iam_member" "public" {
  location = google_cloud_run_v2_service.app.location
  service  = google_cloud_run_v2_service.app.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}

# ── Outputs ───────────────────────────────────────────────────────────────────
output "cloud_run_url" {
  value = google_cloud_run_v2_service.app.uri
}
output "artifact_registry_url" {
  value = "${var.gcp_region}-docker.pkg.dev/${var.gcp_project}/${google_artifact_registry_repository.app.repository_id}"
}
output "postgres_connection_name" {
  value     = google_sql_database_instance.pg.connection_name
  sensitive = true
}

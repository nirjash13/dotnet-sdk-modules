# GCP Terraform IaC — SaasBuilder

Provisions SaasBuilder on Google Cloud using Cloud Run + Cloud SQL + GCS + Pub/Sub + Secret Manager.

## Resources Created

| Resource | Purpose |
|---|---|
| Artifact Registry | Docker image storage |
| Cloud Run v2 | Serverless container runtime (auto-scales to 0) |
| Cloud SQL Postgres 16 | Primary database with PITR enabled |
| GCS Bucket | Blob storage for exports, file uploads |
| Pub/Sub Topic | Message bus (alternative to RabbitMQ) |
| Secret Manager | App secrets |
| Service Account | Least-privilege identity for Cloud Run |

## Prerequisites

- Terraform 1.7+
- `gcloud` CLI authenticated (`gcloud auth application-default login`)
- A GCS bucket for Terraform state (update `backend "gcs"` in `main.tf`)

## Deployment

```bash
cd deploy/iac/gcp-terraform

terraform init

terraform plan \
  -var="gcp_project=my-project-id" \
  -var="environment=dev" \
  -var="docker_image=us-central1-docker.pkg.dev/my-project/saasbuilder-dev/app:latest" \
  -var="postgres_admin_password=$POSTGRES_PASSWORD"

terraform apply [same vars]
```

## Secrets

After deployment, store secrets in Secret Manager:

```bash
echo -n '{"ConnectionStrings__DefaultConnection":"..."}' | \
  gcloud secrets versions add saasbuilder-dev-app --data-file=-
```

Access from Cloud Run via environment variable referencing the secret version.

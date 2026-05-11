# AWS Terraform IaC — SaasBuilder

Provisions SaasBuilder on AWS using ECS Fargate + RDS + S3 + SQS + Secrets Manager.

## Resources Created

| Resource | Purpose |
|---|---|
| ECR Repository | Docker image storage |
| ECS Cluster + Task + Service | Fargate container runtime |
| RDS Postgres 16 | Primary database |
| S3 Bucket | Blob storage for exports, file uploads |
| SQS FIFO Queue | Message bus (alternative to RabbitMQ) |
| Secrets Manager | App secrets (connection strings, JWT key) |
| IAM Roles | ECS execution + task roles with least-privilege |

## Prerequisites

- Terraform 1.7+
- AWS CLI 2.x (`aws configure`)
- An S3 bucket for Terraform state (update `backend "s3"` in `main.tf`)
- A VPC with private subnets for RDS (pass via `private_subnet_ids` variable)

## Deployment

```bash
cd deploy/iac/aws-terraform

terraform init

terraform plan \
  -var="environment=dev" \
  -var="docker_image=<account>.dkr.ecr.us-east-1.amazonaws.com/saasbuilder-dev:latest" \
  -var="postgres_admin_password=$POSTGRES_PASSWORD" \
  -var='private_subnet_ids=["subnet-abc","subnet-def"]'

terraform apply [same vars]
```

## Secrets

Store secrets in AWS Secrets Manager after deployment:

```bash
aws secretsmanager put-secret-value \
  --secret-id saasbuilder-dev/app \
  --secret-string '{"ConnectionStrings__DefaultConnection":"Host=...","Jwt__Secret":"..."}'
```

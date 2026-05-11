terraform {
  required_version = ">= 1.7"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.50"
    }
  }
  backend "s3" {
    bucket = "saasbuilder-tfstate"
    key    = "saasbuilder.tfstate"
    region = "us-east-1"
  }
}

provider "aws" {
  region = var.aws_region
}

locals {
  resource_prefix = "${var.base_name}-${var.environment}"
  tags = {
    Environment = var.environment
    Project     = "saasbuilder"
  }
}

# ── ECR Repository ────────────────────────────────────────────────────────────
resource "aws_ecr_repository" "app" {
  name                 = local.resource_prefix
  image_tag_mutability = "MUTABLE"
  image_scanning_configuration { scan_on_push = true }
  tags = local.tags
}

# ── Secrets Manager ───────────────────────────────────────────────────────────
resource "aws_secretsmanager_secret" "app" {
  name        = "${local.resource_prefix}/app"
  description = "SaasBuilder application secrets"
  tags        = local.tags
}

# ── S3 Bucket (blob storage) ──────────────────────────────────────────────────
resource "aws_s3_bucket" "blobs" {
  bucket = "${local.resource_prefix}-blobs"
  tags   = local.tags
}

resource "aws_s3_bucket_public_access_block" "blobs" {
  bucket                  = aws_s3_bucket.blobs.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "blobs" {
  bucket = aws_s3_bucket.blobs.id
  rule {
    apply_server_side_encryption_by_default { sse_algorithm = "AES256" }
  }
}

# ── SQS Queue (message bus fallback) ─────────────────────────────────────────
resource "aws_sqs_queue" "main" {
  name                       = "${local.resource_prefix}.fifo"
  fifo_queue                 = true
  content_based_deduplication = true
  tags                       = local.tags
}

# ── RDS Postgres ──────────────────────────────────────────────────────────────
resource "aws_db_subnet_group" "pg" {
  name       = "${local.resource_prefix}-pg"
  subnet_ids = var.private_subnet_ids
  tags       = local.tags
}

resource "aws_db_instance" "pg" {
  identifier             = "${local.resource_prefix}-pg"
  engine                 = "postgres"
  engine_version         = "16"
  instance_class         = "db.t3.micro"
  allocated_storage      = 20
  db_name                = "saasbuilder"
  username               = var.postgres_admin_user
  password               = var.postgres_admin_password
  db_subnet_group_name   = aws_db_subnet_group.pg.name
  skip_final_snapshot    = var.environment != "production"
  deletion_protection    = var.environment == "production"
  storage_encrypted      = true
  backup_retention_period = 7
  tags                   = local.tags
}

# ── ECS Cluster ───────────────────────────────────────────────────────────────
resource "aws_ecs_cluster" "main" {
  name = local.resource_prefix
  tags = local.tags
}

# ── ECS Task Definition ───────────────────────────────────────────────────────
resource "aws_ecs_task_definition" "app" {
  family                   = local.resource_prefix
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = 256
  memory                   = 512
  execution_role_arn       = aws_iam_role.ecs_execution.arn
  task_role_arn            = aws_iam_role.ecs_task.arn

  container_definitions = jsonencode([{
    name      = "saasbuilder"
    image     = var.docker_image
    essential = true
    portMappings = [{ containerPort = 8080, protocol = "tcp" }]
    environment = [
      { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
      { name = "ASPNETCORE_HTTP_PORTS", value = "8080" }
    ]
    secrets = [
      { name = "ConnectionStrings__DefaultConnection", valueFrom = "${aws_secretsmanager_secret.app.arn}:ConnectionStrings__DefaultConnection::" }
    ]
    healthCheck = {
      command     = ["CMD-SHELL", "curl -f http://localhost:8080/health/live || exit 1"]
      interval    = 10
      timeout     = 5
      retries     = 3
      startPeriod = 30
    }
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = "/ecs/${local.resource_prefix}"
        "awslogs-region"        = var.aws_region
        "awslogs-stream-prefix" = "ecs"
      }
    }
  }])

  tags = local.tags
}

# ── IAM Roles ─────────────────────────────────────────────────────────────────
resource "aws_iam_role" "ecs_execution" {
  name = "${local.resource_prefix}-ecs-execution"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{ Effect = "Allow", Principal = { Service = "ecs-tasks.amazonaws.com" }, Action = "sts:AssumeRole" }]
  })
  managed_policy_arns = ["arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"]
  tags = local.tags
}

resource "aws_iam_role" "ecs_task" {
  name = "${local.resource_prefix}-ecs-task"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{ Effect = "Allow", Principal = { Service = "ecs-tasks.amazonaws.com" }, Action = "sts:AssumeRole" }]
  })
  tags = local.tags
}

resource "aws_iam_role_policy" "ecs_task_secrets" {
  role = aws_iam_role.ecs_task.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      { Effect = "Allow", Action = ["secretsmanager:GetSecretValue"], Resource = aws_secretsmanager_secret.app.arn },
      { Effect = "Allow", Action = ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"], Resource = "${aws_s3_bucket.blobs.arn}/*" }
    ]
  })
}

# ── Outputs ───────────────────────────────────────────────────────────────────
output "ecr_repository_url" {
  value = aws_ecr_repository.app.repository_url
}
output "rds_endpoint" {
  value     = aws_db_instance.pg.endpoint
  sensitive = true
}
output "s3_bucket" {
  value = aws_s3_bucket.blobs.bucket
}

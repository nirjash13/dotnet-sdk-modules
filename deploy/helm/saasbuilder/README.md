# SaasBuilder Helm Chart

Deploys the SaasBuilder modular monolith on Kubernetes with optional Bitnami PostgreSQL, RabbitMQ, and Redis.

## Prerequisites

- Kubernetes 1.26+
- Helm 3.10+
- `kubectl` configured against your cluster

## Quickstart

> **Security note:** Never pass secrets via `--set` on the command line. Doing so leaks them into
> shell history and into the Helm release manifest stored in-cluster as a Kubernetes Secret.
> Use `--values secrets.yaml` with a gitignored file instead (see below).

```bash
# Add Bitnami repo (for postgresql / rabbitmq / redis dependencies)
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo update

# Update chart dependencies
helm dependency update deploy/helm/saasbuilder

# 1. Copy the example secrets file and fill in real values
cp deploy/helm/saasbuilder/secrets.example.yaml secrets.yaml
# Edit secrets.yaml — never commit it to source control (it is in .gitignore)

# 2. Install with in-cluster PostgreSQL (dev/staging)
helm install saasbuilder deploy/helm/saasbuilder \
  --namespace saasbuilder \
  --create-namespace \
  --values secrets.yaml

# 3. Install pointing to an external DB (production)
helm install saasbuilder deploy/helm/saasbuilder \
  --namespace saasbuilder \
  --create-namespace \
  --set postgresql.enabled=false \
  --values secrets.yaml
```

### Recommended: External Secrets Operator

For production clusters, use the [External Secrets Operator](https://external-secrets.io/) to
sync secrets from AWS Secrets Manager, Azure Key Vault, or GCP Secret Manager instead of
maintaining a local `secrets.yaml`:

```yaml
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: saasbuilder-secrets
spec:
  refreshInterval: 1h
  secretStoreRef:
    name: aws-secretsmanager
    kind: ClusterSecretStore
  target:
    name: saasbuilder-secrets
  data:
    - secretKey: Jwt__Secret
      remoteRef:
        key: saasbuilder/prod
        property: jwt_secret
    - secretKey: ConnectionStrings__Default
      remoteRef:
        key: saasbuilder/prod
        property: db_connection_string
```

## Configuration

| Parameter | Description | Default |
|---|---|---|
| `replicaCount` | Number of pods | `2` |
| `image.repository` | Container image | `ghcr.io/placeholder/saasbuilder` |
| `image.tag` | Image tag (default: appVersion) | `""` |
| `autoscaling.enabled` | Enable HPA | `true` |
| `autoscaling.minReplicas` | HPA minimum | `2` |
| `autoscaling.maxReplicas` | HPA maximum | `10` |
| `autoscaling.targetCPUUtilizationPercentage` | CPU trigger | `70` |
| `podDisruptionBudget.minAvailable` | PDB minimum available | `1` |
| `postgresql.enabled` | Deploy in-cluster Postgres | `true` |
| `rabbitmq.enabled` | Deploy in-cluster RabbitMQ | `false` |
| `redis.enabled` | Deploy in-cluster Redis | `false` |
| `migration.enabled` | Run DB migrations as pre-install Job | `true` |
| `envSecrets.secretName` | K8s secret name for env vars | `saasbuilder-secrets` |

## Health Probes

The chart maps the SaasBuilder health endpoints to Kubernetes probes:

| Probe | Endpoint | Purpose |
|---|---|---|
| Liveness | `/health/live` | Is the process alive? Always 200. |
| Readiness | `/health/ready` | Are DB/MQ/Redis reachable? 503 if not. |
| Startup | `/health/startup` | Have migrations run? 503 until complete. |

## Upgrading

```bash
helm upgrade saasbuilder deploy/helm/saasbuilder --namespace saasbuilder
```

The pre-upgrade migration Job acquires a Postgres advisory lock so only one replica applies migrations during rolling deploys.

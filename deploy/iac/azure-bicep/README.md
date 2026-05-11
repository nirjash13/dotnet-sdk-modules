# Azure Bicep IaC — SaasBuilder

Provisions a complete Azure environment for SaasBuilder using Bicep.

## Resources Created

| Resource | SKU | Purpose |
|---|---|---|
| App Service Plan | B2 Linux | Hosts the .NET 10 web app |
| Web App | Container | Runs SaasBuilder Docker image |
| Container Registry | Basic | Stores Docker images |
| Key Vault | Standard | Stores secrets (connection strings, JWT secret) |
| Postgres Flexible Server | B2ms | Primary database |
| Service Bus | Standard | Message bus for async messaging |
| Storage Account | Standard_LRS | Blob storage for exports, file uploads |

## Prerequisites

- Azure CLI 2.50+ logged in (`az login`)
- An existing Resource Group
- Bicep CLI 0.23+

## Deployment

```bash
# Create resource group
az group create --name rg-saasbuilder-dev --location eastus

# Deploy with parameters file
az deployment group create \
  --resource-group rg-saasbuilder-dev \
  --template-file deploy/iac/azure-bicep/main.bicep \
  --parameters deploy/iac/azure-bicep/parameters.json \
  --parameters postgresAdminPassword="$(az keyvault secret show --vault-name my-kv --name postgres-password --query value -o tsv)"

# View outputs
az deployment group show \
  --resource-group rg-saasbuilder-dev \
  --name main \
  --query properties.outputs
```

## Environment-Specific Overrides

Override parameters on the command line for different environments:

```bash
az deployment group create \
  --template-file main.bicep \
  --parameters environment=production location=westeurope
```

## Secrets

Secrets are never stored in parameters files. Use:
- A Key Vault reference in `parameters.json` (shown in the template), or
- `--parameters secretValue="$(az keyvault secret show ...)"` at deploy time.

Never commit real secrets to source control.

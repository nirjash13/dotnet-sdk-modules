# Azure Terraform IaC — SaasBuilder

Terraform equivalent of the Bicep template — provisions identical Azure resources.

## Prerequisites

- Terraform 1.7+
- Azure CLI 2.50+ (`az login`)
- Azure Storage Account for Terraform state (update `backend "azurerm"` in `main.tf`)

## Deployment

```bash
cd deploy/iac/azure-terraform

# Initialize Terraform (downloads provider, configures backend)
terraform init

# Preview changes
terraform plan \
  -var="environment=dev" \
  -var="postgres_admin_password=$POSTGRES_PASSWORD"

# Apply
terraform apply \
  -var="environment=dev" \
  -var="postgres_admin_password=$POSTGRES_PASSWORD"
```

## State Backend

Update the `backend "azurerm"` block in `main.tf` with your own storage account details before first run. Never commit real backend credentials to source control.

## Destroy

```bash
terraform destroy -var="postgres_admin_password=$POSTGRES_PASSWORD"
```

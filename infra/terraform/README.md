# Foqus Infrastructure — Terraform

This directory contains Terraform configuration for deploying the Foqus WebAPI to Azure.

## Architecture

| Resource | Purpose |
|---|---|
| Resource Group | Logical container for all resources |
| Container Registry (ACR) | Stores Docker images for the API |
| Container Apps Environment | Serverless hosting with Log Analytics |
| Container App | Runs the Foqus WebAPI (0–2 replicas) |
| PostgreSQL Flexible Server | Managed PostgreSQL 16 database |

## Prerequisites

1. **Azure CLI** — [Install](https://learn.microsoft.com/cli/azure/install-azure-cli)
2. **Terraform ≥ 1.5** — [Install](https://developer.hashicorp.com/terraform/install)
3. An Azure subscription with an authenticated `az login` session

## 1 — Create Terraform state storage

Terraform state is stored in an Azure Storage Account. Create it once:

```bash
az group create --name focusbot-tfstate --location westeurope

az storage account create \
  --name focusbottfstate \
  --resource-group focusbot-tfstate \
  --location westeurope \
  --sku Standard_LRS

az storage container create \
  --name tfstate \
  --account-name focusbottfstate
```

## 2 — Configure variables

```bash
cp terraform.tfvars.example terraform.tfvars
```

Edit `terraform.tfvars` and supply the required values (passwords, Supabase credentials, etc.). **Do not commit this file.**

## 3 — Initialize and deploy

```bash
terraform init
terraform plan          # review the execution plan
terraform apply         # provision resources
```

## 4 — Post-deployment steps

1. **Push the Docker image** to the ACR login server shown in `terraform output acr_login_server`.
2. **Run EF Core migrations** — the API applies pending migrations on startup, but you may also run them manually:
   ```bash
   dotnet ef database update \
     --project src/FocusBot.WebAPI/FocusBot.WebAPI.csproj \
     --connection "Host=<pg-fqdn>;Database=focusbot;Username=focusadmin;Password=<pw>;SSL Mode=Require"
   ```
3. **Verify** the health endpoint at the URL from `terraform output api_url` + `/health`.

## CI/CD

The GitHub Actions workflow at `.github/workflows/ci.yml` automates build → test → deploy on push to `main`. See the workflow file for required secrets and variables.

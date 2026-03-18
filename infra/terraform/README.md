# Foqus Infrastructure — Terraform

This directory contains Terraform configuration for deploying the Foqus WebAPI to Azure.

## Architecture

| Resource | Purpose |
| --- | --- |
| Resource Group | Logical container for all resources |
| Container Registry (ACR) | Stores Docker images for the API |
| Container Apps Environment | Serverless hosting with Log Analytics |
| Container App | Runs the Foqus WebAPI (0–2 replicas) |
| PostgreSQL Flexible Server | Managed PostgreSQL 16 database |
| Static Web App (SWA) | Hosts the Foqus frontend on `foqus.me` |

## Prerequisites

1. **Azure CLI** — [Install](https://learn.microsoft.com/cli/azure/install-azure-cli)
2. **Terraform ≥ 1.5** — [Install](https://developer.hashicorp.com/terraform/install)
3. An Azure subscription with an authenticated `az login` session

## 1 — Create Terraform state storage

Terraform state is stored in an Azure Storage Account. Create it once:

```bash
az group create --name foqusbot-tfstate --location westeurope

az storage account create \
  --name foqusbottfstate \
  --resource-group foqusbot-tfstate \
  --location westeurope \
  --sku Standard_LRS

az storage container create \
  --name tfstate \
  --account-name foqusbottfstate
```

## 2 — Configure variables

```bash
cp terraform.tfvars.example terraform.tfvars
```

Edit `terraform.tfvars` and supply the required values (passwords, Supabase credentials, etc.). **Do not commit this file.**

## 3 — Initialize and deploy

```bash
terraform init -backend-config=backend.prod.conf
terraform plan  # review the execution plan
terraform apply # provision resources
```

## 4 — Post-deployment steps

1. **Push the Docker image** to the ACR login server shown in `terraform output acr_login_server` before running `terraform apply`.
   - The Container App provisions a revision during `apply`, and it must be able to pull the image immediately.
   - The Container App pulls that image using a managed identity with `AcrPull` permissions (so you do not need to configure ACR admin credentials on the app itself).
2. **Run EF Core migrations** — the API applies pending migrations on startup, but you may also run them manually:

   ```bash
   dotnet ef database update \
     --project src/FocusBot.WebAPI/FocusBot.WebAPI.csproj \
     --connection "Host=<pg-fqdn>;Database=focusbot;Username=focusadmin;Password=<pw>;SSL Mode=Require"
   ```

3. **Verify** the health endpoint at the URL from `terraform output api_url` + `/health`.
4. **Verify custom domains**:
   - Frontend: `https://foqus.me`
   - API: `https://api.foqus.me`

## CI/CD

The GitHub Actions workflow at `.github/workflows/ci.yml` automates:

- Terraform apply (includes SWA + custom domains)
- Build/push the backend container image and update the Container App to `${{ github.sha }}`
- Build/deploy the frontend to the Static Web App

In GitHub, ensure the `production` environment contains the following additional secrets:

- `MAILERLITE_API_KEY`
- `CLOUDFLARE_API_TOKEN`
- `CLOUDFLARE_ZONE_ID`

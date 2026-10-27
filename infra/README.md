# Azure deployment

OctoCAT Supply runs on **Azure Container Apps**, provisioned with **Terraform** and deployed by a single GitHub Actions workflow ([deploy.yml](../.github/workflows/deploy.yml)) that authenticates with **OIDC** (no stored credentials).

## What gets created

| Resource | Purpose |
| --- | --- |
| Resource group `rg-octocat-dev` | Holds everything below |
| Container Registry (Basic, admin disabled) | `octocat-api` and `octocat-web` images |
| User-assigned managed identity + `AcrPull` | Apps pull images without a registry password |
| Container Apps environment + Log Analytics | Hosting and logs |
| Container App `octocat-dev-api` (.NET API, port 3000) | Single replica, SQLite file on a mounted share |
| Container App `octocat-dev-web` (nginx + React) | Public frontend, configured at start with the API URL |
| Storage account + Azure Files share | Persists the SQLite database at `/data/app.db` |

Notes on the design:

- **SQLite on Azure Files:** the API is pinned to one replica because SQLite has a single writer. The share is mounted with `nobrl` and the API runs with `SQLITE_JOURNAL_MODE=DELETE`, because WAL mode is unsafe on network file systems. For multiple replicas, move to Azure SQL or PostgreSQL.
- **Configuration:** the API reads `PAYMENT_PROVIDER`, `PAYMENT_CURRENCY`, optional `PAYMENT_API_KEY`, `API_CORS_ORIGINS`, `DB_FILE` and `PORT` from environment variables. `PAYMENT_API_KEY` is passed from the `PAYMENT_API_KEY` GitHub secret as a Container App secret (leave the GitHub secret unset for the simulated provider).
- **Terraform state** lives in an Azure Storage account (Entra ID auth, shared keys disabled).

## One-time setup

Prerequisites: Azure CLI (`az login`) and GitHub CLI (`gh auth login`) with rights to create an app registration and subscription role assignments.

```powershell
./infra/bootstrap/bootstrap.ps1 -Repo <owner>/<repo>
```

This creates the state storage, an Entra ID app registration with a federated credential for the `dev` GitHub environment (no client secret), the role assignments (Contributor and Role Based Access Control Administrator on the subscription, Storage Blob Data Contributor on the state account), and the GitHub `dev` environment with the secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID` and the variables `TFSTATE_RESOURCE_GROUP`, `TFSTATE_STORAGE_ACCOUNT` and `TFSTATE_CONTAINER`.

## Pipeline

Pull requests run **build and test** only. Pushes to `master` and manual runs also **deploy**:

1. Run C# tests, frontend lint, unit tests and build, and `terraform fmt` and `validate`.
2. Log in to Azure with OIDC.
3. Create the registry, then build and push both images tagged with the commit SHA.
4. `terraform apply` the full stack with that image tag.
5. Smoke test the live app: load the frontend, create a cart, add an item, confirm a declined card returns 402 and leaves the cart intact, then confirm a valid card creates an order and empties the cart.

The workflow summary lists the frontend and API URLs.

## Monitoring and the Azure SRE Agent

Telemetry provisioned by Terraform ([monitoring.tf](terraform/monitoring.tf)):

- API endpoints: `GET /health` (liveness) and `GET /ready` (readiness, checks SQLite; 503 when the database is unavailable). The Container App probes use them.
- Application Insights (workspace-based, backed by `log-octocat-dev`). The API exports requests, dependencies, logs and metrics when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set (stored as a Container App secret).
- Alerts: HTTP 5xx, container restarts (crash loops), and an availability test against `/ready`, all routed to action group `ag-octocat-dev-ops` (set `TF_VAR_alert_email` to add an email receiver).

Connect the SRE Agent (manual, portal; sign-in and MFA required):

1. Open <https://sre.azure.com> and create an agent in the subscription, region of your choice.
2. Add resource group `rg-octocat-dev` as a managed resource group. Use **Review** run mode so the agent proposes and you approve actions.
3. Check the agent identity has Reader on the group (Contributor only if you want it to apply fixes).
4. Optionally connect the GitHub repo so the agent can correlate failures with deployments.

Fault drill: run the **Deploy to Azure** workflow manually (Actions, Run workflow) and set `payment_provider` to an invalid value such as `bogus`. The API fails fast at startup, the new revision crash-loops, and the restart and availability alerts fire. Ask the agent "why is octocat-dev-api unhealthy?". Remediate by reverting the setting (or `git revert`) and redeploying.

## Running Terraform locally

```powershell
cd infra/terraform
az login
$env:ARM_SUBSCRIPTION_ID = (az account show --query id -o tsv)
terraform init -backend-config="resource_group_name=rg-octocat-tfstate" `
  -backend-config="storage_account_name=<state account>" `
  -backend-config="container_name=tfstate" `
  -backend-config="key=octocat-<repo>.tfstate"
terraform plan
```

To tear everything down: `terraform destroy`.

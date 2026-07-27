# Training Completion Platform

A full-stack project for learning event-driven architecture. The core course-completion write is committed synchronously to PostgreSQL. Certificate generation, reporting, and notification are processed asynchronously through a Transactional Outbox, an Azure Service Bus topic, and three idempotent Azure Functions consumers.

## Architecture

```text
React / TypeScript
        |
ASP.NET Core Web API
        |
PostgreSQL transaction
CourseCompletion + OutboxMessage
        |
API BackgroundService
        |
Azure Service Bus: course-completed
        |
  +-----+---------+-------------+
  |               |             |
Certificate    Reporting    Notification
Function       Function     Function
  |               |             |
Blob Storage  Summary table NotificationLog
```

Message delivery is at least once. Every consumer uses `(EventId, ConsumerName)` in PostgreSQL for deduplication. Certificate blob names are derived from the completion ID, so retries remain safe if a process fails before or after the database commit.

## Repository Structure

```text
src/             Domain, Application, Infrastructure, API, and Functions
tests/           xUnit unit tests and PostgreSQL integration tests
web/             React, Vite, and TanStack Query
infrastructure/  AzureRM Terraform and remote-state bootstrap
scripts/         Local configuration and smoke tests
.github/         CI and manually approved application deployment
```

## Prerequisites

- .NET SDK 10
- Node.js 24 and npm
- PostgreSQL 16, or Azure Database for PostgreSQL created by Terraform
- Terraform 1.15.x
- Azure CLI
- Azure Functions Core Tools v4

Check the installed versions:

```powershell
dotnet --version
node --version
npm.cmd --version
terraform version
az version
func --version
```

The PowerShell examples use `npm.cmd`, so changing the PowerShell execution policy is not required.

## 1. Create the Azure Development Resources

By default, Terraform creates:

- A resource group
- PostgreSQL Flexible Server 16 with B1ms compute, 32 GB storage, and 7-day backup retention
- `training_dev` and `training_test` databases
- A PostgreSQL firewall rule limited to the specified developer IPv4 address
- A Standard Service Bus namespace, the `course-completed` topic, and three subscriptions
- A local-development SAS rule with Send and Listen permissions
- Standard LRS Storage and a private `certificates` container
- Log Analytics and Application Insights
- An optional resource-group budget

The default is `deploy_compute=false`, so Terraform does not create App Service, Function App, Static Web Apps, or Key Vault resources.

```powershell
Copy-Item infrastructure/terraform.tfvars.example infrastructure/terraform.tfvars
$env:TF_VAR_postgres_admin_password = "use-a-long-random-password"

az login
az account set --subscription "SUBSCRIPTION_NAME_OR_ID"

terraform -chdir=infrastructure init
terraform -chdir=infrastructure fmt -recursive
terraform -chdir=infrastructure validate
terraform -chdir=infrastructure plan -out tfplan
```

Review every item in the plan. After confirming the resources, region, and estimated cost, apply the reviewed plan yourself:

```powershell
terraform -chdir=infrastructure apply tfplan
```

Do not commit `terraform.tfvars`, state files, or plan files. Sensitive output values are still stored in Terraform state.

## 2. Configure Local Secrets

After Terraform apply finishes, run:

```powershell
.\scripts\configure-local.ps1
```

The script reads `terraform output -json` and configures:

- .NET user secrets for the API
- The ignored Functions `local.settings.json`
- The ignored React `.env.local`

The script does not print passwords to the terminal. You can also refer to:

- `src/TrainingCompletion.Functions/local.settings.json.example`
- `web/training-completion-web/.env.example`

## 3. Create the Database Schema

```powershell
dotnet tool restore
dotnet ef database update `
  --project src/TrainingCompletion.Infrastructure `
  --startup-project src/TrainingCompletion.Api
```

The initial migration creates the complete schema and seeds:

- `learner-1001`
- `course-2001`
- `course-2002`
- `course-2003`

## 4. Run Locally

Terminal 1:

```powershell
dotnet run --project src/TrainingCompletion.Api
```

Terminal 2:

```powershell
Set-Location src/TrainingCompletion.Functions
func start
```

Terminal 3:

```powershell
Set-Location web/training-completion-web
npm.cmd ci
npm.cmd run dev
```

Open `http://localhost:5173`. The API runs at `http://localhost:5000`, and the Functions host normally runs at `http://localhost:7071`.

Run a quick API verification:

```powershell
.\scripts\verify-local.ps1
```

## API

```http
GET    /api/courses?learnerId=learner-1001
POST   /api/courses/{courseId}/enrollments
POST   /api/course-completions
GET    /api/course-completions/{completionId}
GET    /api/course-completions/{completionId}/certificate
GET    /api/admin/diagnostics
GET    /health
```

Enrollment request body:

```json
{
  "learnerId": "learner-1001"
}
```

Completion request:

```http
POST /api/course-completions
Idempotency-Key: client-generated-key
Content-Type: application/json
```

```json
{
  "learnerId": "learner-1001",
  "courseId": "course-2001"
}
```

The initial successful request returns `201 Created`. Retrying the same body with the same key returns the same completion with `200 OK` and `Idempotency-Replayed: true`. Reusing the key with a different body returns an RFC 7807 `409 Conflict`.

## Testing

Backend:

```powershell
dotnet restore TrainingCompletionPlatform.slnx
dotnet build TrainingCompletionPlatform.slnx --no-restore
dotnet test TrainingCompletionPlatform.slnx --no-build
```

Tests that require PostgreSQL run only when `POSTGRES_TEST_CONNECTION` is set. They enforce that the database name is exactly `training_test`:

```powershell
$env:POSTGRES_TEST_CONNECTION = "Host=...;Database=training_test;Username=...;Password=...;SSL Mode=Require"
dotnet test tests/TrainingCompletion.IntegrationTests
```

If the connection string points to `training_dev`, the tests refuse to perform destructive cleanup.

Frontend:

```powershell
Set-Location web/training-completion-web
npm.cmd ci
npm.cmd audit
npm.cmd run lint
npm.cmd test -- --run
npm.cmd run build
```

Terraform:

```powershell
terraform fmt -check -recursive
terraform -chdir=infrastructure init -backend=false
terraform -chdir=infrastructure validate
terraform -chdir=infrastructure/bootstrap init -backend=false
terraform -chdir=infrastructure/bootstrap validate
```

## Failures, Retries, and Diagnostics

- The outbox publisher reads at most 20 messages every 2 seconds.
- `PublishedAt` is written only after a successful Service Bus send.
- Publishing failures use capped backoff from 5 seconds to 5 minutes.
- Each subscription has `MaxDeliveryCount=5`.
- A fifth consumer failure records a sanitized `ConsumerFailure`, marks the workflow step as `Failed`, and dead-letters the message.
- `/api/admin/diagnostics` returns only counts, attempt information, and sanitized errors of at most 500 characters. It does not return stack traces, connection strings, or event payloads.
- The API accepts or generates `X-Correlation-ID` and passes it to the event and structured logs.

## Deploy the Compute Resources

After local end-to-end testing succeeds, change this value in `terraform.tfvars`:

```hcl
deploy_compute = true
```

Run `terraform plan` again. The additional resources include:

- Linux App Service B1
- Functions Flex Consumption with .NET 10 isolated worker
- Static Web Apps Free
- Key Vault
- System-assigned managed identities for the API and Functions
- Least-privilege Service Bus, Blob Storage, and Key Vault role assignments

Because App Service and Flex Consumption outbound IP addresses can change, enabling compute adds Azure PostgreSQL's `0.0.0.0` "Allow Azure services" firewall rule. This rule is not created when `deploy_compute=false`. Replace it with VNet and private endpoint connectivity before processing real data.

The GitHub `CI` workflow automatically runs builds, tests, npm audit, and Terraform validation. The `Deploy applications` workflow can only be triggered manually and requires approval through the GitHub `development` environment. It does not execute Terraform apply.

Configure these GitHub settings:

- Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, and `AZURE_STATIC_WEB_APPS_API_TOKEN`
- Variables: `API_APP_NAME`, `FUNCTION_APP_NAME`, and `API_BASE_URL`
- An Azure OIDC federated credential

After deployment:

```powershell
.\scripts\verify-deployed.ps1 -ApiBaseUrl "https://YOUR_API_HOST"
```

## Remote Terraform State

Use local state initially. Before using CI for infrastructure or collaborating with a team:

```powershell
terraform -chdir=infrastructure/bootstrap init
terraform -chdir=infrastructure/bootstrap plan -out tfplan
terraform -chdir=infrastructure/bootstrap apply tfplan
```

Create the ignored `infrastructure/backend.hcl` from the bootstrap outputs, uncomment `backend "azurerm" {}` in `versions.tf`, and then run:

```powershell
terraform -chdir=infrastructure init -migrate-state -backend-config=backend.hcl
```

## Cost and Cleanup

- Keep `deploy_compute=false` until the local workflow is ready.
- Stopping PostgreSQL compute does not stop storage charges, and the server does not remain stopped indefinitely.
- Service Bus Standard, Storage, Log Analytics, and other provisioned services can incur charges without active traffic.
- `terraform destroy` permanently deletes databases and certificates.

Before a long pause, export any data you need and review the destroy plan:

```powershell
terraform -chdir=infrastructure plan -destroy
terraform -chdir=infrastructure destroy
```

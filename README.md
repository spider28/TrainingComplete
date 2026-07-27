# Training Completion Platform

一个用于学习事件驱动架构的全栈项目。课程完成的核心写入同步提交到 PostgreSQL；证书、报表和通知通过 Transactional Outbox、Azure Service Bus Topic 和三个幂等 Azure Functions 消费者异步处理。

## 架构

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

消息是 at-least-once 投递。所有消费者在 PostgreSQL 中使用 `(EventId, ConsumerName)` 去重。证书 Blob 名由 Completion ID 确定，因此数据库提交前后发生崩溃也能安全重试。

## 目录

```text
src/             Domain、Application、Infrastructure、API、Functions
tests/           xUnit 单元测试与 PostgreSQL 集成测试
web/             React、Vite、TanStack Query
infrastructure/  AzureRM Terraform 和远程 state bootstrap
scripts/         本地配置与 smoke test
.github/         CI 和手动应用部署
```

## 前置工具

- .NET SDK 10
- Node.js 24 和 npm
- PostgreSQL 16，或由 Terraform 创建的 Azure PostgreSQL
- Terraform 1.15.x
- Azure CLI
- Azure Functions Core Tools v4

检查版本：

```powershell
dotnet --version
node --version
npm.cmd --version
terraform version
az version
func --version
```

本仓库的 PowerShell 示例使用 `npm.cmd`，不需要修改 PowerShell execution policy。

## 1. 创建 Azure 开发资源

Terraform 默认创建：

- Resource Group
- PostgreSQL Flexible Server 16：B1ms、32 GB、7 天备份
- `training_dev` 和 `training_test`
- 仅允许指定开发者 IPv4 的 PostgreSQL firewall rule
- Service Bus Standard、`course-completed` topic、三个 subscriptions
- 本地开发用 Send/Listen SAS rule
- Standard LRS Storage 和私有 `certificates` container
- Log Analytics 和 Application Insights
- 可选 Resource Group budget

默认 `deploy_compute=false`，不会创建 App Service、Function App、Static Web App 或 Key Vault。

```powershell
Copy-Item infrastructure/terraform.tfvars.example infrastructure/terraform.tfvars
$env:TF_VAR_postgres_admin_password = "使用一个长随机密码"

az login
az account set --subscription "SUBSCRIPTION_NAME_OR_ID"

terraform -chdir=infrastructure init
terraform -chdir=infrastructure fmt -recursive
terraform -chdir=infrastructure validate
terraform -chdir=infrastructure plan -out tfplan
```

逐项检查 plan。确认资源、区域和预估成本后，由你执行：

```powershell
terraform -chdir=infrastructure apply tfplan
```

不要提交 `terraform.tfvars`、state 或 plan 文件。敏感 output 仍会进入 Terraform state。

## 2. 配置本地 secrets

Terraform apply 完成后：

```powershell
.\scripts\configure-local.ps1
```

脚本读取 `terraform output -json`，配置：

- API 的 .NET user secrets
- Functions 的 ignored `local.settings.json`
- React 的 ignored `.env.local`

脚本不会把密码输出到终端。也可以参考：

- `src/TrainingCompletion.Functions/local.settings.json.example`
- `web/training-completion-web/.env.example`

## 3. 创建数据库 schema

```powershell
dotnet tool restore
dotnet ef database update `
  --project src/TrainingCompletion.Infrastructure `
  --startup-project src/TrainingCompletion.Api
```

初始 migration 创建完整 schema，并预置：

- `learner-1001`
- `course-2001`
- `course-2002`
- `course-2003`

## 4. 本地运行

终端 1：

```powershell
dotnet run --project src/TrainingCompletion.Api
```

终端 2：

```powershell
Set-Location src/TrainingCompletion.Functions
func start
```

终端 3：

```powershell
Set-Location web/training-completion-web
npm.cmd ci
npm.cmd run dev
```

打开 `http://localhost:5173`。API 为 `http://localhost:5000`，Functions host 通常为 `http://localhost:7071`。

快速验证 API：

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

报名 body：

```json
{
  "learnerId": "learner-1001"
}
```

完成请求：

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

首次成功返回 `201 Created`。相同 key 和相同 body 的重试返回同一个 Completion、`200 OK` 和 `Idempotency-Replayed: true`。相同 key 配合不同 body 返回 RFC 7807 `409 Conflict`。

## 测试

后端：

```powershell
dotnet restore TrainingCompletionPlatform.slnx
dotnet build TrainingCompletionPlatform.slnx --no-restore
dotnet test TrainingCompletionPlatform.slnx --no-build
```

需要 PostgreSQL 的测试只会在设置 `POSTGRES_TEST_CONNECTION` 后运行，并严格检查数据库名必须是 `training_test`：

```powershell
$env:POSTGRES_TEST_CONNECTION = "Host=...;Database=training_test;Username=...;Password=...;SSL Mode=Require"
dotnet test tests/TrainingCompletion.IntegrationTests
```

如果连接串指向 `training_dev`，测试会拒绝执行 destructive cleanup。

前端：

```powershell
Set-Location web/training-completion-web
npm.cmd ci
npm.cmd audit
npm.cmd run lint
npm.cmd test -- --run
npm.cmd run build
```

Terraform：

```powershell
terraform fmt -check -recursive
terraform -chdir=infrastructure init -backend=false
terraform -chdir=infrastructure validate
terraform -chdir=infrastructure/bootstrap init -backend=false
terraform -chdir=infrastructure/bootstrap validate
```

## 故障、重试与诊断

- Outbox 每 2 秒读取最多 20 条。
- 只有 Service Bus send 成功后才写 `PublishedAt`。
- 发布失败采用 5 秒至 5 分钟的封顶退避。
- Subscription `MaxDeliveryCount=5`。
- 第五次消费者失败会记录脱敏的 `ConsumerFailure`、将步骤标记为 `Failed` 并 dead-letter。
- `/api/admin/diagnostics` 只返回计数、attempt 和最多 500 字符的脱敏错误，不返回 stack trace、connection string 或 payload。
- API 接受或生成 `X-Correlation-ID`，并把它传到事件和结构化日志。

## 部署计算资源

本地端到端测试成功后，把 `terraform.tfvars` 中的值改为：

```hcl
deploy_compute = true
```

重新运行 `terraform plan`。新增资源包括：

- Linux App Service B1
- Functions Flex Consumption / .NET 10 isolated
- Static Web Apps Free
- Key Vault
- API 和 Functions 的 system-assigned managed identities
- Service Bus、Blob 和 Key Vault 最小权限 role assignments

由于 App Service/Flex Consumption 的 outbound IP 会变化，演示环境在启用 compute
时增加 Azure PostgreSQL 的 `0.0.0.0` “Allow Azure services”规则。它不会在
`deploy_compute=false` 时创建；处理真实数据前应改为 VNet/private endpoint。

GitHub `CI` workflow 自动执行 build、test、npm audit 和 Terraform validate。`Deploy applications` 只能手动触发，并要求 GitHub `development` Environment 审批；它不会执行 Terraform apply。

需要配置 GitHub：

- Secrets：`AZURE_CLIENT_ID`、`AZURE_TENANT_ID`、`AZURE_SUBSCRIPTION_ID`、`AZURE_STATIC_WEB_APPS_API_TOKEN`
- Variables：`API_APP_NAME`、`FUNCTION_APP_NAME`、`API_BASE_URL`
- Azure OIDC federated credential

部署后：

```powershell
.\scripts\verify-deployed.ps1 -ApiBaseUrl "https://YOUR_API_HOST"
```

## 远程 Terraform state

初期使用本地 state。需要 CI 或团队协作前：

```powershell
terraform -chdir=infrastructure/bootstrap init
terraform -chdir=infrastructure/bootstrap plan -out tfplan
terraform -chdir=infrastructure/bootstrap apply tfplan
```

根据 bootstrap output 创建 ignored `infrastructure/backend.hcl`，取消 `versions.tf` 中的 `backend "azurerm" {}` 注释，然后：

```powershell
terraform -chdir=infrastructure init -migrate-state -backend-config=backend.hcl
```

## 成本和清理

- 先保持 `deploy_compute=false`。
- 停用 PostgreSQL compute 时仍会收取 storage 费用，而且不会无限期保持停止。
- Service Bus Standard、Storage、Log Analytics 等在没有流量时仍可能产生成本。
- `terraform destroy` 会删除数据库和证书，无法恢复。

长期暂停前先导出需要的数据，然后检查销毁计划：

```powershell
terraform -chdir=infrastructure plan -destroy
terraform -chdir=infrastructure destroy
```

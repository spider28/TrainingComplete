[CmdletBinding()]
param(
    [string]$InfrastructureDirectory = (Join-Path $PSScriptRoot "..\infrastructure")
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command terraform -ErrorAction SilentlyContinue)) {
    throw "Terraform is not installed or is not on PATH."
}

$infrastructurePath = (Resolve-Path -LiteralPath $InfrastructureDirectory).Path
$outputJson = & terraform "-chdir=$infrastructurePath" output -json
if ($LASTEXITCODE -ne 0) {
    throw "terraform output failed. Apply or select the intended Terraform state first."
}

$outputs = $outputJson | ConvertFrom-Json
function Get-OutputValue([string]$Name) {
    $property = $outputs.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value.value) {
        throw "Terraform output '$Name' is missing."
    }
    return [string]$property.Value.value
}

$postgres = Get-OutputValue "postgres_dev_connection_string"
$serviceBus = Get-OutputValue "service_bus_local_connection_string"
$storage = Get-OutputValue "storage_connection_string"
$container = Get-OutputValue "certificate_container"

$apiProject = Join-Path $PSScriptRoot "..\src\TrainingCompletion.Api"
& dotnet user-secrets set "ConnectionStrings:Postgres" $postgres --project $apiProject | Out-Null
& dotnet user-secrets set "ServiceBus:ConnectionString" $serviceBus --project $apiProject | Out-Null
& dotnet user-secrets set "ServiceBus:Enabled" "true" --project $apiProject | Out-Null
& dotnet user-secrets set "Storage:ConnectionString" $storage --project $apiProject | Out-Null
& dotnet user-secrets set "Storage:CertificateContainer" $container --project $apiProject | Out-Null

$functionDirectory = Join-Path $PSScriptRoot "..\src\TrainingCompletion.Functions"
$localSettingsPath = Join-Path $functionDirectory "local.settings.json"
$localSettings = [ordered]@{
    IsEncrypted = $false
    Values = [ordered]@{
        AzureWebJobsStorage = $storage
        FUNCTIONS_WORKER_RUNTIME = "dotnet-isolated"
        ServiceBusConnection = $serviceBus
        PostgresConnection = $postgres
        "Storage__ConnectionString" = $storage
        "Storage__CertificateContainer" = $container
        "AzureWebJobs.CertificateConsumerFunction.Disabled" = "false"
    }
}
$localSettings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $localSettingsPath -Encoding UTF8

$webDirectory = Join-Path $PSScriptRoot "..\web\training-completion-web"
"VITE_API_BASE_URL=http://localhost:5000" |
    Set-Content -LiteralPath (Join-Path $webDirectory ".env.local") -Encoding UTF8

Write-Host "Local settings were configured without printing secret values."
Write-Host "Files containing secrets remain excluded by .gitignore."


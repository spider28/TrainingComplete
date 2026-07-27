[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"

Write-Host "Checking API health..."
$health = Invoke-WebRequest -Uri "$ApiBaseUrl/health" -UseBasicParsing
if ($health.StatusCode -ne 200) {
    throw "API health check returned $($health.StatusCode)."
}

Write-Host "Checking seeded courses..."
$courses = Invoke-RestMethod -Uri "$ApiBaseUrl/api/courses?learnerId=learner-1001"
if ($courses.Count -lt 3) {
    throw "Expected at least three seeded courses."
}

Write-Host "Checking diagnostics contract..."
$diagnostics = Invoke-RestMethod -Uri "$ApiBaseUrl/api/admin/diagnostics"
if ($null -eq $diagnostics.pendingOutboxCount) {
    throw "Diagnostics did not return pendingOutboxCount."
}

Write-Host "Local API verification passed."


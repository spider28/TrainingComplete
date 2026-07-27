[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ApiBaseUrl
)

$ErrorActionPreference = "Stop"
$baseUrl = $ApiBaseUrl.TrimEnd("/")
$health = Invoke-WebRequest -Uri "$baseUrl/health" -UseBasicParsing
if ($health.StatusCode -ne 200) {
    throw "Deployed API health check failed with $($health.StatusCode)."
}

$courses = Invoke-RestMethod -Uri "$baseUrl/api/courses?learnerId=learner-1001"
if ($courses.Count -lt 3) {
    throw "Deployed API did not return the seeded courses."
}

Write-Host "Deployed API smoke test passed."


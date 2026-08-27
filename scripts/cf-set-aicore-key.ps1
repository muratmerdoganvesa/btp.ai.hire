# Sets AICORE_SERVICE_KEY on CF without PowerShell eating '$' in clientsecret.
# Usage (from hirelens/):  pwsh ./scripts/cf-set-aicore-key.ps1
param(
    [string]$KeyPath = (Join-Path $PSScriptRoot "..\aicore-service-key.json"),
    [string[]]$Apps = @("hirelens-api", "hirelens-worker")
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $KeyPath)) {
    throw "Key file not found: $KeyPath"
}

$json = [System.IO.File]::ReadAllText((Resolve-Path $KeyPath)).Trim()
if (-not $json.StartsWith("{")) {
    throw "Key file is not a JSON object."
}

$cf = Get-Command cf -ErrorAction Stop
foreach ($app in $Apps) {
    Write-Host "Setting AICORE_SERVICE_KEY on $app"
    $p = Start-Process -FilePath $cf.Source -ArgumentList @("set-env", $app, "AICORE_SERVICE_KEY", $json) -Wait -NoNewWindow -PassThru
    if ($p.ExitCode -ne 0) {
        throw "cf set-env $app failed with $($p.ExitCode)"
    }
}

Write-Host "Restarting hirelens-api"
$restart = Start-Process -FilePath $cf.Source -ArgumentList @("restart", "hirelens-api") -Wait -NoNewWindow -PassThru
if ($restart.ExitCode -ne 0) {
    throw "cf restart hirelens-api failed with $($restart.ExitCode)"
}

$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    docker compose stop api web
    if ($LASTEXITCODE -ne 0) { throw 'Stop the development services before removing their certificate.' }
    & (Join-Path $PSScriptRoot 'manage-dev-https.ps1') -Action Remove
}
finally { Pop-Location }

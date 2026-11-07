$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'check-dev-https.ps1')
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    docker compose up -d --build api web
    if ($LASTEXITCODE -ne 0) { throw 'Development startup failed.' }
}
finally { Pop-Location }

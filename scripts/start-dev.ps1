$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'check-dev-https.ps1')
if (Test-Path (Join-Path (Split-Path -Parent $PSScriptRoot) '.env.sql')) {
    & (Join-Path $PSScriptRoot 'setup-dev-sql.ps1')
}
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    docker compose up -d --build api web
    if ($LASTEXITCODE -ne 0) { throw 'Development startup failed.' }
}
finally { Pop-Location }

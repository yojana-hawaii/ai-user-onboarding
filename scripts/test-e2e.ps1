$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    docker compose -f compose.yaml -f compose.e2e.yaml build e2e
    if ($LASTEXITCODE -ne 0) { throw 'E2E image build failed.' }
    docker compose -f compose.yaml -f compose.e2e.yaml run --rm e2e
    if ($LASTEXITCODE -ne 0) { throw 'Browser E2E tests failed. Reports are in the e2e_reports Docker volume.' }
}
finally {
    docker compose -f compose.yaml -f compose.e2e.yaml rm --force --stop e2e-api
    Pop-Location
}

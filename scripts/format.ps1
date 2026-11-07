$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    docker compose -f compose.yaml -f .docker/format.compose.yaml run --rm --no-deps backend-check dotnet format DaHub.slnx
    if ($LASTEXITCODE -ne 0) { throw 'Backend formatting failed.' }
    docker compose -f compose.yaml -f .docker/format.compose.yaml run --rm --no-deps web-check sh -c 'npm ci --ignore-scripts && npm run format'
    if ($LASTEXITCODE -ne 0) { throw 'Frontend formatting failed.' }
}
finally { Pop-Location }

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$workspaceRoot = Split-Path -Parent $PSScriptRoot
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker is missing. Start Docker Desktop with Linux containers. See README.md.'
}
if (-not (Test-Path (Join-Path $workspaceRoot 'src/dahub-web/package-lock.json'))) {
    throw 'Start the web container, then review package-lock.json before verification. See README.md.'
}

Push-Location $workspaceRoot
try {
    docker compose run --rm --no-deps backend-check sh -c 'dotnet build DaHub.slnx --configuration Release && dotnet test DaHub.slnx --configuration Release --no-build && dotnet format DaHub.slnx --verify-no-changes --no-restore'
    if ($LASTEXITCODE -ne 0) { throw 'Backend verification failed.' }
    docker compose run --rm --no-deps web-check sh -c 'npm ci && npm run build && npm test && npm run format:check'
    if ($LASTEXITCODE -ne 0) { throw 'Frontend verification failed.' }
}
finally { Pop-Location }

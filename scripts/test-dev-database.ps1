#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    docker compose run --rm --no-deps database-check dotnet test tests/DaHub.Tests.Integration --configuration Release --filter FullyQualifiedName~SqlServerMigrationTests
    if ($LASTEXITCODE -ne 0) { throw 'Isolated SQL migration and API persistence checks failed.' }
}
finally { Pop-Location }

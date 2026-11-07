$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $running = @(docker compose ps --status running --services | Where-Object { $_ -in @('api','web') })
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect Docker. Stop DaHub before changing its certificate.' }
    if ($running.Count) {
        docker compose stop @running
        if ($LASTEXITCODE -ne 0) { throw 'Could not stop the development services.' }
    }
    try { & (Join-Path $PSScriptRoot 'manage-dev-https.ps1') -Action Renew }
    finally {
        if ($running.Count) {
            docker compose up -d @running
            if ($LASTEXITCODE -ne 0) { throw 'Certificate operation finished, but development services could not restart.' }
        }
    }
}
finally { Pop-Location }

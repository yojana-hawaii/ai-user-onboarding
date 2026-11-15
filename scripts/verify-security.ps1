$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    New-Item -ItemType Directory -Path (Join-Path (Get-Location) '.tools/security') -Force | Out-Null
    $composeArguments = @('compose','-f','compose.yaml','-f','compose.security.yaml')
    docker @composeArguments config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Security Compose configuration failed.' }

    docker @composeArguments build image-scan
    if ($LASTEXITCODE -ne 0) { throw 'Scanner image build failed.' }
    docker @composeArguments run --rm --no-deps secret-scan dir /repo --config /repo/.gitleaks.toml --redact=100 --exit-code 1
    if ($LASTEXITCODE -ne 0) { throw 'Working-tree secret scan failed. Findings are redacted.' }
    docker @composeArguments run --rm --no-deps secret-scan git /repo --config /repo/.gitleaks.toml --redact=100 --exit-code 1 --log-opts=--all
    if ($LASTEXITCODE -ne 0) { throw 'Git-history secret scan failed. Findings are redacted.' }

    docker compose run --rm --no-deps backend-check dotnet restore DaHub.slnx --force --no-cache -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=low
    if ($LASTEXITCODE -ne 0) { throw 'NuGet vulnerability audit or dependency restore failed.' }
    docker compose run --rm --no-deps web-check npm audit --package-lock-only --audit-level=high
    if ($LASTEXITCODE -ne 0) { throw 'npm dependency audit failed.' }

    $archiveDirectory = Join-Path (Get-Location) '.tools/security'
    New-Item -ItemType Directory -Path $archiveDirectory -Force | Out-Null
    docker compose -f compose.yaml -f compose.e2e.yaml build backend-check web-check sql e2e
    if ($LASTEXITCODE -ne 0) { throw 'Development image build failed.' }
    foreach ($image in @('dahub-backend-dev:local','dahub-frontend-dev:local','dahub-sql-dev:local','dahub-e2e-dev:local')) {
        $archivePath = Join-Path $archiveDirectory ($image.Replace(':','-') + '.tar')
        try {
            docker image save --output $archivePath $image
            if ($LASTEXITCODE -ne 0) { throw 'Development image export failed.' }
            if (-not $IsWindows) {
                # Docker exports may be owner-only; the non-root scanner reads public tool images.
                [System.IO.File]::SetUnixFileMode($archivePath, [System.IO.UnixFileMode]::UserRead -bor [System.IO.UnixFileMode]::UserWrite -bor [System.IO.UnixFileMode]::GroupRead -bor [System.IO.UnixFileMode]::OtherRead)
            }
            $containerPath = '/images/' + [System.IO.Path]::GetFileName($archivePath)
            # Scanning a large archive through a Windows bind mount is very slow.
            # Copy it once to Docker-local storage and release the host copy before analysis.
            docker @composeArguments run --rm --no-deps --entrypoint sh image-scan -c 'cp "$1" /cache/dahub-security-image.tar' sh $containerPath
            if ($LASTEXITCODE -ne 0) { throw 'Development image cache copy failed.' }
            Remove-Item -LiteralPath $archivePath
            $reportLines = docker @composeArguments run --rm --no-deps image-scan image --no-progress --timeout 20m --input /cache/dahub-security-image.tar --scanners vuln --severity HIGH,CRITICAL --exit-code 1 --format json
            $scanExitCode = $LASTEXITCODE
            if ($scanExitCode -notin @(0,1)) { throw "Image scanner failed operationally: $image" }
            $report = ($reportLines -join "`n") | ConvertFrom-Json -AsHashtable -Depth 100
            $findings = @(foreach ($target in $report['Results']) {
                foreach ($finding in $target['Vulnerabilities']) {
                    [pscustomobject]@{ Image=$image; Package=$finding['PkgName']; CVE=$finding['VulnerabilityID']; Severity=$finding['Severity']; Installed=$finding['InstalledVersion']; Fixed=$finding['FixedVersion'] }
                }
            })
            Write-Output "$image : $($findings.Count) HIGH/CRITICAL findings."
            foreach ($finding in $findings) { $finding | ConvertTo-Json -Compress }
            if ($scanExitCode -ne 0) { throw "Development image vulnerability scan failed: $image" }
        }
        finally {
            if (Test-Path $archivePath) { Remove-Item -LiteralPath $archivePath }
            docker @composeArguments run --rm --no-deps --entrypoint sh image-scan -c 'rm -f /cache/dahub-security-image.tar'
            if ($LASTEXITCODE -ne 0) { Write-Warning 'Temporary public image archive cleanup failed.' }
        }
    }
}
finally { Pop-Location }

#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$workspaceRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'setup-dev-sql.ps1')

function Write-ProtectedSecret([string]$Path, [string]$Value) {
    if (-not (Test-Path -LiteralPath $Path)) { New-Item -ItemType File -Path $Path | Out-Null }
    if ($IsWindows) {
        $sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        icacls $Path /inheritance:r /grant:r "*${sid}:(F)" '*S-1-5-18:(F)' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not protect database credentials.' }
    }
    else {
        [System.IO.File]::SetUnixFileMode($Path, [System.IO.UnixFileMode]::UserRead -bor [System.IO.UnixFileMode]::UserWrite)
    }
    [System.IO.File]::WriteAllText($Path, $Value)
}

$secretDirectory = Join-Path $workspaceRoot '.secrets'
New-Item -ItemType Directory -Force -Path $secretDirectory | Out-Null
$runtimePath = Join-Path $secretDirectory 'database-connection'
$migrationPath = Join-Path $secretDirectory 'database-migration'
$adminPassword = $null
$appPassword = $null
try {
    $adminLine = Get-Content -LiteralPath (Join-Path $workspaceRoot '.env.sql') |
        Where-Object { $_ -match '^MSSQL_SA_PASSWORD=' } | Select-Object -First 1
    $adminPassword = $adminLine.Substring('MSSQL_SA_PASSWORD='.Length)
    # Setup generates base64 credentials; refusing other forms avoids SQL/connection-string interpolation.
    if ($adminPassword -notmatch '^[A-Za-z0-9+/=!]{16,}$') { throw 'Unsupported local administrator credential format.' }
    Write-ProtectedSecret $migrationPath "Server=sql,1433;Database=DaHub;User ID=sa;Password=$adminPassword;Encrypt=true;TrustServerCertificate=true"

    if (Test-Path -LiteralPath $runtimePath) {
        $existing = [System.IO.File]::ReadAllText($runtimePath)
        if ($existing -notmatch ';Password=([A-Za-z0-9+/=!]{16,});') {
            throw 'Existing application credential is not in the managed format; preserved without modification.'
        }
        $appPassword = $Matches[1]
    }
    else {
        $appPassword = 'Aa1!' + [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    }
    Write-ProtectedSecret $runtimePath "Server=sql,1433;Database=DaHub;User ID=dahub_app;Password=$appPassword;Encrypt=true;TrustServerCertificate=true"

    Push-Location $workspaceRoot
    try {
        docker compose run --rm --no-deps database-migrate
        if ($LASTEXITCODE -ne 0) { throw 'Database migration failed.' }
        $query = @"
SET NOCOUNT ON;
IF SUSER_ID(N'dahub_app') IS NULL
    CREATE LOGIN [dahub_app] WITH PASSWORD=N'$appPassword', CHECK_POLICY=ON;
IF PWDCOMPARE(N'$appPassword', (SELECT password_hash FROM sys.sql_logins WHERE name=N'dahub_app')) <> 1
    THROW 51000, 'Existing API login does not match the preserved local credential.', 1;
IF IS_SRVROLEMEMBER(N'sysadmin',N'dahub_app') = 1
    THROW 51000, 'The API identity must not be a server administrator.', 1;
USE [DaHub];
IF USER_ID(N'dahub_app') IS NULL CREATE USER [dahub_app] FOR LOGIN [dahub_app];
GRANT CONNECT TO [dahub_app];
IF ISNULL(IS_ROLEMEMBER(N'dahub_app_runtime',N'dahub_app'),0) = 0
    ALTER ROLE [dahub_app_runtime] ADD MEMBER [dahub_app];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dahub_ita.Locations TO [dahub_app];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dahub_ita.Departments TO [dahub_app];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dahub_ita.JobTitles TO [dahub_app];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dahub_ita.Specialties TO [dahub_app];
GRANT SELECT ON OBJECT::dahub_ita.__EFMigrationsHistory TO [dahub_app];
"@
        # sqlcmd does not echo input. Administrator credentials stay inside the SQL container.
        $query | docker compose exec -T sql sh -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -o /dev/null'
        if ($LASTEXITCODE -ne 0) { throw 'Application database identity setup failed.' }
    }
    finally { Pop-Location }
}
finally {
    $adminPassword = $null; $appPassword = $null; $query = $null; $existing = $null
    # The migration service uses the administrator secret only for this explicit operation.
    if (Test-Path -LiteralPath $migrationPath) { Remove-Item -LiteralPath $migrationPath -Force }
}
Write-Output 'All first-release schema migrations applied. Dedicated API credentials are protected in .secrets/database-connection.'
Write-Output 'Run scripts/start-dev.ps1 to start the API with SQL readiness enabled.'

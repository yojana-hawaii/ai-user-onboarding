#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$passwordPath = Join-Path $workspaceRoot '.env.sql'

docker info --format '{{.OSType}}' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Start Docker Desktop with Linux containers before setting up SQL Server.' }

if (-not (Test-Path -LiteralPath $passwordPath) -or (Get-Item -LiteralPath $passwordPath).Length -eq 0) {
    # Create an empty protected file before writing the generated credential.
    if (-not (Test-Path -LiteralPath $passwordPath)) { New-Item -ItemType File -Path $passwordPath | Out-Null }
    if ($IsWindows) {
        $userSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        icacls $passwordPath /inheritance:r /grant:r "*${userSid}:(F)" '*S-1-5-18:(F)' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not protect .env.sql. No password was written.' }
    }
    else {
        [System.IO.File]::SetUnixFileMode($passwordPath, [System.IO.UnixFileMode]::UserRead -bor [System.IO.UnixFileMode]::UserWrite)
    }
    $password = 'Aa1!' + [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    [System.IO.File]::WriteAllText($passwordPath, "MSSQL_SA_PASSWORD=$password`n")
    $password = $null
}
if (-not (Select-String -LiteralPath $passwordPath -Pattern '^MSSQL_SA_PASSWORD=.{16,}$' -Quiet)) {
    throw '.env.sql must contain a strong MSSQL_SA_PASSWORD. Existing credentials were preserved.'
}

Push-Location $workspaceRoot
try {
    docker compose --profile database up -d --build --wait --wait-timeout 240 sql
    if ($LASTEXITCODE -ne 0) { throw 'SQL Server startup failed. Inspect docker compose logs sql.' }

    # sqlcmd reads the password inside the container, never from a command argument.
    $query = "IF DB_ID(N'DaHub') IS NULL EXEC(N'CREATE DATABASE [DaHub]'); SELECT name, state_desc FROM sys.databases WHERE name = N'DaHub';"
    $query | docker compose exec -T sql sh -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b'
    if ($LASTEXITCODE -ne 0) { throw 'DaHub database initialization failed.' }
}
finally { Pop-Location }

Write-Output 'SQL Server 2022 Developer is ready at localhost,1433. Database: DaHub. Login: sa.'
Write-Output 'The local administrator password is stored in the Git-ignored .env.sql file.'

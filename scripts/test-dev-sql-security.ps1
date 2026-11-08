#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    # Optional client encryption must still produce an encrypted TCP session.
    $query = @'
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM sys.dm_exec_connections WHERE session_id=@@SPID AND encrypt_option='TRUE')
    THROW 51000, 'Server must require encryption even when the client requests optional encryption.', 1;
IF EXISTS (SELECT 1 FROM sys.configurations WHERE name IN ('external scripts enabled','xp_cmdshell','Ole Automation Procedures','clr enabled','Ad Hoc Distributed Queries','cross db ownership chaining') AND CONVERT(int,value_in_use)<>0)
    THROW 51000, 'Unsafe SQL feature is enabled.', 1;
IF EXISTS (SELECT 1 FROM sys.databases WHERE name='DaHub' AND (is_trustworthy_on=1 OR is_db_chaining_on=1))
    THROW 51000, 'DaHub database security settings are unsafe.', 1;
IF DB_ID('DaHub') IS NULL THROW 51000, 'DaHub database is missing.', 1;
PRINT 'PASS: SQL encryption and feature restrictions.';
'@
    $query | docker compose exec -T sql sh -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S tcp:localhost,1433 -U sa -C -No -b'
    if ($LASTEXITCODE -ne 0) { throw 'SQL security assertions failed.' }

    docker compose exec -T sql sh -c 'test ! -e /opt/mssql-extensibility/bin/launchpad && test ! -e /opt/mssql/bin/launchpadd && test ! -e /opt/mssql/bin/setnetbr && grep -q "forceencryption = 1" /var/opt/mssql/mssql.conf && grep -q "tlsprotocols = 1.2" /var/opt/mssql/mssql.conf'
    if ($LASTEXITCODE -ne 0) { throw 'SQL image or TLS configuration assertions failed.' }

    $containerId = docker compose ps -q sql
    if ($LASTEXITCODE -ne 0 -or -not $containerId) { throw 'SQL container is not running.' }
    $container = (docker inspect $containerId | ConvertFrom-Json)[0]
    if ($container.Config.User -ne '10001:10001' -or $container.HostConfig.Privileged -or
        $container.HostConfig.CapDrop -notcontains 'ALL' -or
        $container.HostConfig.SecurityOpt -notcontains 'no-new-privileges:true') {
        throw 'SQL container privilege restrictions changed.'
    }
    $bindings = @($container.HostConfig.PortBindings.'1433/tcp')
    if ($bindings.Count -ne 1 -or $bindings[0].HostIp -ne '127.0.0.1') {
        throw 'SQL port must be published only on IPv4 loopback.'
    }
    Write-Output 'PASS: SQL helper removal, TLS policy and container restrictions.'
}
finally { Pop-Location }

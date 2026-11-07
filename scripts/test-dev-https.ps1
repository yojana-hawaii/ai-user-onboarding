$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'Certificate integration checks require Windows PowerShell 7.' }
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$testParent = Join-Path $workspaceRoot '.tools/certificate-tests'
$testRoot = Join-Path $testParent ([guid]::NewGuid().ToString())
$scriptDirectory = Join-Path $testRoot 'scripts'
$certificateDirectory = Join-Path $testRoot '.certs'
$testFriendlyName = 'DaHub certificate test ' + [guid]::NewGuid().ToString()
$keyLock = $null
$unmanaged = $null
try {
    New-Item -ItemType Directory -Force -Path $scriptDirectory,$certificateDirectory | Out-Null
    $source = Get-Content (Join-Path $PSScriptRoot 'manage-dev-https.ps1') -Raw
    $testScript = Join-Path $scriptDirectory 'manage-dev-https.ps1'
    [System.IO.File]::WriteAllText($testScript, $source.Replace('DaHub local development HTTPS', $testFriendlyName))
    $certificatePath = Join-Path $certificateDirectory 'fullchain.pem'
    $keyPath = Join-Path $certificateDirectory 'privkey.pem'

    $acl = [System.Security.AccessControl.DirectorySecurity]::new($certificateDirectory,[System.Security.AccessControl.AccessControlSections]::Access)
    $acl.SetAccessRuleProtection($true,$false)
    foreach ($rule in @($acl.Access)) { $acl.RemoveAccessRuleSpecific($rule) }
    foreach ($principal in @([System.Security.Principal.WindowsIdentity]::GetCurrent().Name,'SYSTEM')) {
        $acl.AddAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new($principal,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
    }
    [System.IO.FileSystemAclExtensions]::SetAccessControl([System.IO.DirectoryInfo]::new($certificateDirectory),$acl)
    # An unrelated certificate must survive both removal and renewal requests.
    $unmanaged = New-SelfSignedCertificate -Subject 'CN=localhost' -FriendlyName ($testFriendlyName + ' unmanaged') -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -KeyLength 2048 -KeyExportPolicy Exportable
    $key = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($unmanaged)
    try {
        [System.IO.File]::WriteAllText($certificatePath,$unmanaged.ExportCertificatePem())
        [System.IO.File]::WriteAllText($keyPath,$key.ExportPkcs8PrivateKeyPem())
    }
    finally { $key.Dispose() }
    foreach ($action in @('Remove','Renew')) {
        $rejected = $false
        try { & $testScript -Action $action } catch { $rejected = $true }
        if (-not $rejected -or -not (Test-Path $keyPath) -or -not (Test-Path "Cert:\CurrentUser\My\$($unmanaged.Thumbprint)")) { throw 'Unmanaged certificate protection failed.' }
    }
    Remove-Item -LiteralPath $certificatePath,$keyPath
    Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($unmanaged.Thumbprint)" -DeleteKey
    $unmanaged = $null
    Write-Output 'PASS: unrelated certificates are preserved.'

    # Generate an untrusted test certificate; no test roots are installed.
    & $testScript -Action Create
    $before = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem([System.IO.File]::ReadAllText($certificatePath))
    $beforeThumbprint = $before.Thumbprint
    $before.Dispose()
    $keyLock = [System.IO.File]::Open($keyPath,[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,[System.IO.FileShare]::Read)
    $rejected = $false
    try { & $testScript -Action Renew } catch { $rejected = $true }
    $keyLock.Dispose(); $keyLock = $null
    $after = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPemFile($certificatePath,$keyPath)
    try { if (-not $rejected -or $after.Thumbprint -ne $beforeThumbprint) { throw 'Interrupted replacement did not preserve the old certificate/key pair.' } }
    finally { $after.Dispose() }
    $remaining = @(Get-ChildItem Cert:\CurrentUser\My | Where-Object FriendlyName -EQ $testFriendlyName)
    if ($remaining.Count -ne 1) { throw 'Failed renewal left an extra stored private key.' }
    Write-Output 'PASS: failed key replacement rolls back and removes the new stored key.'

    & $testScript -Action Remove
    if ((Test-Path $certificatePath) -or (Test-Path $keyPath) -or (Test-Path "Cert:\CurrentUser\My\$beforeThumbprint")) { throw 'Managed certificate removal did not complete.' }
    Write-Output 'PASS: managed removal deletes only its certificate files and key.'
}
finally {
    if ($keyLock) { $keyLock.Dispose() }
    foreach ($certificate in @(Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.FriendlyName -eq $testFriendlyName -or $_.FriendlyName -eq ($testFriendlyName + ' unmanaged') })) {
        Remove-Item -LiteralPath $certificate.PSPath -DeleteKey
    }
    $resolvedTarget = [System.IO.Path]::GetFullPath($testRoot)
    if (-not $resolvedTarget.StartsWith([System.IO.Path]::GetFullPath($testParent) + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Test cleanup escaped its workspace directory.' }
    if (Test-Path $resolvedTarget) { Remove-Item -LiteralPath $resolvedTarget -Recurse }
}

param(
    [Parameter(Mandatory)][ValidateSet('Create','Check','Renew','Remove')][string]$Action,
    [switch]$Trust,
    [ValidateRange(0,90)][int]$MinimumDays = 14
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'Local certificate management requires Windows PowerShell 7.' }

$certificateDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) '.certs'
$certificatePath = Join-Path $certificateDirectory 'fullchain.pem'
$keyPath = Join-Path $certificateDirectory 'privkey.pem'
$friendlyName = 'DaHub local development HTTPS'
$pendingCleanupPath = Join-Path $certificateDirectory 'pending-cleanup.json'

function Get-CurrentCertificate {
    if (-not (Test-Path $certificatePath) -or -not (Test-Path $keyPath)) {
        throw 'Both .certs/fullchain.pem and .certs/privkey.pem are required. Run setup-dev-https.ps1 for local development.'
    }
    $pair = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPemFile($certificatePath, $keyPath)
    try {
        return [pscustomobject]@{ Thumbprint=$pair.Thumbprint; NotBefore=$pair.NotBefore; NotAfter=$pair.NotAfter; Subject=$pair.Subject }
    }
    finally { $pair.Dispose() }
}

function Assert-OwnedCertificate($info) {
    $stored = Get-Item -LiteralPath "Cert:\CurrentUser\My\$($info.Thumbprint)" -ErrorAction SilentlyContinue
    if (-not $stored -or $stored.FriendlyName -ne $friendlyName -or $stored.Subject -ne 'CN=localhost' -or -not $stored.HasPrivateKey) {
        throw 'This is not the matching DaHub local certificate owned by this Windows user. ACME and unrelated certificates will not be changed.'
    }
}

function Remove-OwnedStoreEntries([string]$thumbprint) {
    $info = [pscustomobject]@{Thumbprint=$thumbprint}
    Assert-OwnedCertificate $info
    $rootPath = "Cert:\CurrentUser\Root\$thumbprint"
    if (Test-Path $rootPath) {
        $store = [System.Security.Cryptography.X509Certificates.X509Store]::new('Root','CurrentUser')
        try {
            $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $matches = $store.Certificates.Find([System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint, $thumbprint, $false)
            foreach ($entry in $matches) { $store.Remove($entry) }
        }
        finally { $store.Dispose() }
    }
    Remove-Item -LiteralPath "Cert:\CurrentUser\My\$thumbprint" -DeleteKey
}

if ($Action -eq 'Check') {
    $info = Get-CurrentCertificate
    if ($info.NotBefore -gt (Get-Date) -or $info.NotAfter -le (Get-Date).AddDays($MinimumDays)) {
        throw "Certificate is not yet valid, expired, or expires within $MinimumDays days. Run scripts/renew-dev-https.ps1 for a managed development certificate."
    }
    $chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
    $publicCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem([System.IO.File]::ReadAllText($certificatePath))
    try {
        if (-not $chain.Build($publicCertificate)) { throw 'Certificate is not trusted by this Windows user. Run setup-dev-https.ps1 -Trust for a managed local certificate.' }
    }
    finally { $chain.Dispose(); $publicCertificate.Dispose() }
    Write-Output "HTTPS certificate is trusted and valid through $($info.NotAfter.ToString('yyyy-MM-dd'))."
    return
}

# Serialize certificate mutations for this user/workspace.
$mutexMaterial = $certificateDirectory + [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$mutexHash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($mutexMaterial)))
$mutex = [System.Threading.Mutex]::new($false, "Local\DaHubHttps-$mutexHash")
$locked = $false
try {
    $locked = $mutex.WaitOne(0)
    if (-not $locked) { throw 'Another DaHub certificate operation is already running.' }
    $oldInfo = $null
    if ((Test-Path $certificatePath) -or (Test-Path $keyPath)) {
        $oldInfo = Get-CurrentCertificate
        Assert-OwnedCertificate $oldInfo
    }
    if (Test-Path $pendingCleanupPath) {
        $pending = Get-Content -LiteralPath $pendingCleanupPath -Raw | ConvertFrom-Json
        if ($pending.Thumbprint -notmatch '^[A-Fa-f0-9]{40}$' -or ($oldInfo -and $pending.Thumbprint -eq $oldInfo.Thumbprint)) {
            throw 'Invalid cleanup record; the active certificate will not be removed.'
        }
        if ((Test-Path "Cert:\CurrentUser\My\$($pending.Thumbprint)") -or (Test-Path "Cert:\CurrentUser\Root\$($pending.Thumbprint)")) {
            Remove-OwnedStoreEntries $pending.Thumbprint
        }
        Remove-Item -LiteralPath $pendingCleanupPath
    }
    if ($Action -in @('Renew','Remove') -and -not $oldInfo) { throw 'No managed local certificate exists.' }
    if ($Action -eq 'Remove') {
        Remove-OwnedStoreEntries $oldInfo.Thumbprint
        Remove-Item -LiteralPath $certificatePath, $keyPath
        Write-Output 'Removed the matching DaHub certificate files, Windows trust entry, and stored private key.'
        return
    }
    if ($Action -eq 'Create' -and $oldInfo) {
        if ($Trust) { Import-Certificate -FilePath $certificatePath -CertStoreLocation Cert:\CurrentUser\Root | Out-Null }
        if ($oldInfo.NotAfter -le (Get-Date).AddDays($MinimumDays)) { throw 'Existing certificate needs renewal. Run scripts/renew-dev-https.ps1.' }
        Write-Output 'Using the existing DaHub local certificate.'
        return
    }

    New-Item -ItemType Directory -Path $certificateDirectory -Force | Out-Null
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $directoryAcl = [System.Security.AccessControl.DirectorySecurity]::new($certificateDirectory, [System.Security.AccessControl.AccessControlSections]::Access)
    $directoryAcl.SetAccessRuleProtection($true, $false)
    foreach ($existingRule in @($directoryAcl.Access)) { $directoryAcl.RemoveAccessRuleSpecific($existingRule) }
    foreach ($principal in @($identity, 'SYSTEM')) {
        $directoryAcl.AddAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new($principal, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    }
    [System.IO.FileSystemAclExtensions]::SetAccessControl([System.IO.DirectoryInfo]::new($certificateDirectory), $directoryAcl)

    $oldCertificateText = if ($oldInfo) { [System.IO.File]::ReadAllText($certificatePath) } else { $null }
    $oldKeyText = if ($oldInfo) { [System.IO.File]::ReadAllText($keyPath) } else { $null }
    $shouldTrust = $Trust -or ($oldInfo -and (Test-Path "Cert:\CurrentUser\Root\$($oldInfo.Thumbprint)"))
    $newCertificate = New-SelfSignedCertificate -Subject 'CN=localhost' -FriendlyName $friendlyName `
        -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 `
        -KeyExportPolicy Exportable -KeyUsage DigitalSignature,KeyEncipherment -NotAfter (Get-Date).AddDays(90) `
        -TextExtension @('2.5.29.17={text}DNS=localhost&DNS=api&IPAddress=127.0.0.1&IPAddress=::1', '2.5.29.37={text}1.3.6.1.5.5.7.3.1')
    $temporaryCertificate = Join-Path $certificateDirectory ([guid]::NewGuid().ToString() + '.pem')
    $temporaryKey = Join-Path $certificateDirectory ([guid]::NewGuid().ToString() + '.key')
    $privateKey = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($newCertificate)
    $certificateReplaced = $false
    $keyReplaced = $false
    try {
        [System.IO.File]::WriteAllText($temporaryCertificate, $newCertificate.ExportCertificatePem())
        [System.IO.File]::WriteAllText($temporaryKey, $privateKey.ExportPkcs8PrivateKeyPem())
        if ($shouldTrust) { Import-Certificate -FilePath $temporaryCertificate -CertStoreLocation Cert:\CurrentUser\Root | Out-Null }
        [System.IO.File]::Move($temporaryCertificate, $certificatePath, $true)
        $certificateReplaced = $true
        [System.IO.File]::Move($temporaryKey, $keyPath, $true)
        $keyReplaced = $true
    }
    catch {
        if ($oldInfo) {
            if ($certificateReplaced) { [System.IO.File]::WriteAllText($certificatePath, $oldCertificateText) }
            if ($keyReplaced) { [System.IO.File]::WriteAllText($keyPath, $oldKeyText) }
        }
        else {
            foreach ($partialPath in @($certificatePath,$keyPath)) { if (Test-Path $partialPath) { Remove-Item -LiteralPath $partialPath } }
        }
        Remove-OwnedStoreEntries $newCertificate.Thumbprint
        throw 'Certificate replacement failed; the previous certificate files were restored when present.'
    }
    finally {
        $privateKey.Dispose()
        $oldKeyText = $null
        foreach ($temporaryPath in @($temporaryCertificate,$temporaryKey)) { if (Test-Path $temporaryPath) { Remove-Item -LiteralPath $temporaryPath } }
    }
    if ($oldInfo) {
        [System.IO.File]::WriteAllText($pendingCleanupPath, (@{Thumbprint=$oldInfo.Thumbprint} | ConvertTo-Json -Compress))
        Remove-OwnedStoreEntries $oldInfo.Thumbprint
        Remove-Item -LiteralPath $pendingCleanupPath
    }
    Write-Output 'DaHub local certificate created; valid for 90 days. Previous managed trust/key entries were removed when renewing.'
}
finally {
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}

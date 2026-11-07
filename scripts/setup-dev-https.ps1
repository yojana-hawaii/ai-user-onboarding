param([switch]$Trust)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'manage-dev-https.ps1') -Action Create -Trust:$Trust

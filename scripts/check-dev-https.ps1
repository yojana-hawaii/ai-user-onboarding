param([ValidateRange(0,90)][int]$MinimumDays = 14)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'manage-dev-https.ps1') -Action Check -MinimumDays $MinimumDays

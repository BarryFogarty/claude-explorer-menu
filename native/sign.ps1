#Requires -Version 7
<#
.SYNOPSIS
  Signs dist\ClaudeMenu.msix with a self-signed certificate (created on first run) and exports dist\ClaudeMenu.cer.
  The certificate must be trusted once, from an elevated prompt:
    Import-Certificate -FilePath <repo>\dist\ClaudeMenu.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$dist = Join-Path $PSScriptRoot 'dist'
$subject = 'CN=BarryFogarty'

$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type Custom -Subject $subject -KeyUsage DigitalSignature `
        -FriendlyName 'Claude Context Menu' -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(5) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}

Export-Certificate -Cert $cert -FilePath (Join-Path $dist 'ClaudeMenu.cer') -Force | Out-Null

$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe |
    Where-Object FullName -Match '\\x64\\' | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $signtool) { throw 'signtool.exe not found (install the Windows SDK)' }

& $signtool.FullName sign /fd SHA256 /sha1 $cert.Thumbprint (Join-Path $dist 'ClaudeMenu.msix')
if ($LASTEXITCODE -ne 0) { throw 'signtool failed' }

Write-Host "Signed. Trust the certificate once (elevated): Import-Certificate -FilePath '$dist\ClaudeMenu.cer' -CertStoreLocation Cert:\LocalMachine\TrustedPeople"

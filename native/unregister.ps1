#Requires -Version 7
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Get-AppxPackage BarryFogarty.ClaudeContextMenu | Remove-AppxPackage
Stop-Process -Name explorer -Force
Start-Sleep -Seconds 2
if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe }
Write-Host 'Unregistered.'

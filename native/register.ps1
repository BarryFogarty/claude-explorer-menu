#Requires -Version 7
<#
.SYNOPSIS
  Registers the sparse package (top-level Windows 11 context menu entry) for the current user, then restarts Explorer.
  Prerequisite (once, elevated): trust dist\ClaudeMenu.cer - see sign.ps1.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$dist = Join-Path $PSScriptRoot 'dist'

Get-AppxPackage BarryFogarty.ClaudeContextMenu | Remove-AppxPackage
Add-AppxPackage -Path (Join-Path $dist 'ClaudeMenu.msix') -ExternalLocation $dist

# Explorer caches context menu registrations; restart it so the entry appears.
Stop-Process -Name explorer -Force
Start-Sleep -Seconds 2
if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe }
Write-Host 'Registered. Right-click a folder.'

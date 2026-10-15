#Requires -Version 7
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

foreach ($root in 'HKCU:\Software\Classes\Directory\shell\Claude', 'HKCU:\Software\Classes\Directory\Background\shell\Claude') {
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
}
Write-Host 'Removed Claude context menu entries. Config (~\.claude-menu.json) left in place.'

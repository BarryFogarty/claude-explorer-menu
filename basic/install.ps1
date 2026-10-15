#Requires -Version 7
<#
.SYNOPSIS
  Registers a "Claude" cascading context menu for folders and folder backgrounds (HKCU, no admin).
  Config: config.json in the repository root (edited in place; nothing is copied).
  Re-run after editing the config. Appears under "Show more options" on Windows 11.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$configPath = Join-Path $PSScriptRoot '..\config.json'
$config = Get-Content $configPath -Raw | ConvertFrom-Json

$wt = (Get-Command wt.exe -ErrorAction Stop).Source
$shell = (Get-Command $config.shell -ErrorAction Stop).Source
$extra = ($config.extraArgs | ForEach-Object { $_ }) -join ' '

# "%V." keeps a drive root ("C:\") from escaping the closing quote.
function Get-LaunchCommand([string] $claudeArgs) {
    $invocation = ("claude $claudeArgs $extra").Trim()
    "`"$wt`" -d `"%V.`" `"$shell`" -NoExit -Command $invocation"
}

# Verbs: label, claude args, separator-before
$verbs = @()
foreach ($m in $config.models) {
    $verbs += [pscustomobject]@{ Label = $m.label; Args = "--model $($m.flag)"; Sep = $false }
}
$verbs += [pscustomobject]@{ Label = 'Continue last chat'; Args = '--continue'; Sep = $true }
$verbs += [pscustomobject]@{ Label = 'Resume picker...'; Args = '--resume'; Sep = $false }

$roots = @(
    'HKCU:\Software\Classes\Directory\shell\Claude',
    'HKCU:\Software\Classes\Directory\Background\shell\Claude'
)

foreach ($root in $roots) {
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
    New-Item $root -Force | Out-Null
    New-ItemProperty $root -Name MUIVerb -Value 'Claude' | Out-Null
    New-ItemProperty $root -Name Icon -Value (Resolve-Path (Join-Path $PSScriptRoot '..\assets\claude-glyph.ico')).Path | Out-Null
    New-ItemProperty $root -Name SubCommands -Value '' | Out-Null

    $i = 0
    foreach ($v in $verbs) {
        $i++
        $key = Join-Path $root ('shell\{0:D2}' -f $i)
        New-Item "$key\command" -Force | Out-Null
        New-ItemProperty $key -Name MUIVerb -Value $v.Label | Out-Null
        if ($v.Sep) { New-ItemProperty $key -Name CommandFlags -PropertyType DWord -Value 0x20 | Out-Null }
        Set-Item "$key\command" -Value (Get-LaunchCommand $v.Args)
    }
}

Write-Host "Installed $($verbs.Count) entries for folders and folder backgrounds. Config: $configPath"

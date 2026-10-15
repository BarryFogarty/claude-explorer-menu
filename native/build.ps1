#Requires -Version 7
<#
.SYNOPSIS
  Builds the native handler (Native AOT) and packs the sparse package. Output: dist\ClaudeMenu.dll, dist\ClaudeMenu.msix.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$assetsDir = Join-Path (Split-Path $root) 'assets'

# The AOT link step shells out to vswhere, which is not on PATH by default.
$installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if (Test-Path $installer) { $env:PATH += ";$installer" }

dotnet publish (Join-Path $root 'src\ClaudeMenu\ClaudeMenu.csproj') -c Release -r win-x64 -o $dist
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Copy-Item (Join-Path $assetsDir 'claude-glyph.ico') $dist -Force

# Package logos from the glyph (manifest requires them; they are not shown anywhere).
Add-Type -AssemblyName System.Drawing
$assets = Join-Path $root 'package\Assets'
New-Item $assets -ItemType Directory -Force | Out-Null
$glyph = [System.Drawing.Image]::FromFile((Join-Path $assetsDir 'glyph256.png'))
foreach ($logo in @{ 'StoreLogo.png' = 50; 'Square44x44Logo.png' = 44; 'Square150x150Logo.png' = 150 }.GetEnumerator()) {
    $bmp = New-Object System.Drawing.Bitmap($logo.Value, $logo.Value)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.DrawImage($glyph, 0, 0, $logo.Value, $logo.Value)
    $g.Dispose()
    $bmp.Save((Join-Path $assets $logo.Key), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
$glyph.Dispose()

$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter makeappx.exe |
    Where-Object FullName -Match '\\x64\\' | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw 'makeappx.exe not found (install the Windows SDK)' }

$msix = Join-Path $dist 'ClaudeMenu.msix'
Remove-Item -LiteralPath $msix -ErrorAction SilentlyContinue
& $makeappx.FullName pack /d (Join-Path $root 'package') /p $msix /nv /o
if ($LASTEXITCODE -ne 0) { throw 'makeappx failed' }

Write-Host "Built $dist"

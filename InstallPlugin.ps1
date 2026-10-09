[CmdletBinding()]
param(
    [string]$PluginRoot = (Join-Path $env:LOCALAPPDATA 'NINA\Plugins\3.0.0'),
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
if (Get-Process -Name NINA -ErrorAction SilentlyContinue) {
    throw 'Close NINA before installing Live Focus.'
}
if (-not $SkipBuild) {
    & dotnet build (Join-Path $PSScriptRoot 'LiveFocus.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Live Focus build failed.' }
}
$taskDll = Join-Path $PSScriptRoot 'bin\Release\net8.0-windows\Cwseo.NINA.LiveFocus.dll'
if (-not (Test-Path -LiteralPath $taskDll)) { throw "Build output not found: $taskDll" }
if (Get-Process -Name NINA -ErrorAction SilentlyContinue) {
    throw 'NINA started during the build. Close it before installing.'
}
$taskDestination = Join-Path $PluginRoot 'Live Focus'
New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
Copy-Item -LiteralPath $taskDll -Destination $taskDestination -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE.txt') -Destination $taskDestination -Force
$taskInstalled = Join-Path $taskDestination 'Cwseo.NINA.LiveFocus.dll'
if ((Get-FileHash -LiteralPath $taskDll).Hash -ne (Get-FileHash -LiteralPath $taskInstalled).Hash) {
    throw 'Installed DLL verification failed.'
}
Write-Output "Installed Live Focus: $taskInstalled"

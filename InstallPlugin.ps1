[CmdletBinding()]
param(
    [string]$PluginRoot = (Join-Path $env:LOCALAPPDATA 'NINA\Plugins\3.0.0'),
    [string]$StagingRoot = (Join-Path $env:LOCALAPPDATA 'NINA\PluginStaging\3.0.0'),
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
if (-not $SkipBuild) {
    & dotnet build (Join-Path $PSScriptRoot 'LiveFocus.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Live Focus build failed.' }
}
$taskDll = Join-Path $PSScriptRoot 'bin\Release\net8.0-windows\Cwseo.NINA.LiveFocus.dll'
if (-not (Test-Path -LiteralPath $taskDll)) { throw "Build output not found: $taskDll" }
function Copy-LiveFocus([string]$Destination) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Copy-Item -LiteralPath $taskDll -Destination $Destination -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE.txt') -Destination $Destination -Force
    $taskCopied = Join-Path $Destination 'Cwseo.NINA.LiveFocus.dll'
    if ((Get-FileHash -LiteralPath $taskDll).Hash -ne (Get-FileHash -LiteralPath $taskCopied).Hash) {
        throw "DLL verification failed: $taskCopied"
    }
}

# Always refresh staging so an older pending DLL cannot replace this build.
$taskPending = Join-Path $StagingRoot 'Live Focus'
Copy-LiveFocus $taskPending
Write-Output "Staged Live Focus: $(Join-Path $taskPending 'Cwseo.NINA.LiveFocus.dll')"
if (Get-Process -Name NINA -ErrorAction SilentlyContinue) {
    Write-Output 'NINA is running. Restart NINA to apply this build.'
    return
}

$taskDestination = Join-Path $PluginRoot 'Live Focus'
try {
    Copy-LiveFocus $taskDestination
} catch {
    if (Get-Process -Name NINA -ErrorAction SilentlyContinue) {
        Write-Output 'NINA started during installation. The verified staged build will apply on restart.'
        return
    }
    throw
}
Write-Output "Installed Live Focus: $(Join-Path $taskDestination 'Cwseo.NINA.LiveFocus.dll')"

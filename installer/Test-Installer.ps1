#requires -Version 5
<#
.SYNOPSIS
    Smoke-tests the compiled DiskPart UI installer end to end.

.DESCRIPTION
    Silently installs DiskPartUI-v<Version>-setup.exe, verifies that the app files,
    the self-contained runtime, the Start Menu shortcut, the uninstaller and the
    Programs-and-Features entry were all created, then silently uninstalls and
    confirms everything was removed again.

    The installer is marked requireAdministrator, so this script must be run from an
    elevated PowerShell session. It changes nothing permanently: the app is always
    uninstalled before the script returns.

.PARAMETER Version
    Version embedded in the setup file name (bin\DiskPartUI-v<Version>-setup.exe).
    Defaults to 1.0.0.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File installer\Test-Installer.ps1
#>
[CmdletBinding()]
param(
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'

# installer\ sits next to bin\ under the repo root, so resolve paths from here.
$repoRoot = Split-Path -Parent $PSScriptRoot
$setup = Join-Path $repoRoot "bin\DiskPartUI-v$Version-setup.exe"
$app = Join-Path $env:ProgramFiles 'DiskPartUI'
$exe = Join-Path $app 'DiskPartUI.exe'
$uninstaller = Join-Path $app 'unins000.exe'
$shortcut = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\DiskPart UI\DiskPart UI.lnk'
$installLog = Join-Path $env:TEMP 'DiskPartUI-inno-install.log'

# The installer requests administrator rights, so it cannot run unelevated.
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
{
    throw 'Run this from an elevated PowerShell session (Run as administrator).'
}

if (-not (Test-Path $setup))
{
    throw "Installer not found: $setup. Build it first by compiling installer\DiskPartUI.iss."
}

$script:passed = $true
function Check($label, $condition)
{
    $status = 'FAIL'
    if ($condition)
    {
        $status = 'ok'
    }
    else
    {
        $script:passed = $false
    }
    Write-Host ("  [{0,-4}] {1}" -f $status, $label)
}

Write-Host "Installing $setup ..."
$install = Start-Process $setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=$installLog" -Wait -PassThru

# Let the installer finish writing files before checking.
$deadline = (Get-Date).AddSeconds(60)
while ((-not (Test-Path $exe)) -and (Get-Date) -lt $deadline)
{
    Start-Sleep -Milliseconds 500
}

Write-Host 'Verifying install:'
Check 'installer exited 0' ($install.ExitCode -eq 0)
Check 'app folder created' (Test-Path $app)
Check 'DiskPartUI.exe present' (Test-Path $exe)
Check 'self-contained runtime (coreclr.dll)' (Test-Path (Join-Path $app 'coreclr.dll'))
Check 'WebView2 loader present' (Test-Path (Join-Path $app 'WebView2Loader.dll'))
Check 'Blazor host (wwwroot\index.html)' (Test-Path (Join-Path $app 'wwwroot\index.html'))
Check 'Start Menu shortcut created' (Test-Path $shortcut)
Check 'uninstaller present' (Test-Path $uninstaller)

$arpKeys = @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
)
$arp = foreach ($key in $arpKeys)
{
    if (Test-Path $key)
    {
        Get-ChildItem $key |
            ForEach-Object { Get-ItemProperty $_.PSPath } |
            Where-Object { $_.DisplayName -like '*DiskPart UI*' }
    }
}
Check 'Programs-and-Features entry registered' ([bool]$arp)
if ($arp)
{
    $entry = $arp | Select-Object -First 1
    Write-Host ("       {0} / {1} / {2}" -f $entry.DisplayName, $entry.DisplayVersion, $entry.Publisher)
}

Write-Host 'Launching the installed app:'
if (Test-Path $exe)
{
    #WebView2 must not put its data folder next to the exe: under Program Files that is
    #read-only and the app dies with "We couldn't create the data directory".
    $webViewData = Join-Path $env:LOCALAPPDATA 'DiskPartUI\WebView2'
    $strayData = Join-Path $app 'DiskPartUI.exe.WebView2'
    #Clear both locations first so the checks below reflect only this launch. A stray folder
    #beside the exe survives uninstall (the installer never created it), and would otherwise
    #fail the check forever after one bad build.
    foreach ($stale in @($webViewData, $strayData))
    {
        if (Test-Path $stale)
        {
            Remove-Item $stale -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    $app_proc = Start-Process $exe -PassThru
    #Give the WebView time to initialize before inspecting where it wrote its data.
    $deadline = (Get-Date).AddSeconds(30)
    while ((-not (Test-Path $webViewData)) -and (Get-Date) -lt $deadline -and -not $app_proc.HasExited)
    {
        Start-Sleep -Milliseconds 500
    }

    Check 'process still running' (-not $app_proc.HasExited)
    Check 'WebView2 data folder created under LocalAppData' (Test-Path $webViewData)
    Check 'no WebView2 folder written beside the exe' (-not (Test-Path $strayData))

    try { $app_proc | Stop-Process -Force -ErrorAction Stop } catch {}
    Start-Sleep -Milliseconds 1500
}

Write-Host 'Uninstalling ...'
if (Test-Path $uninstaller)
{
    # The Inno uninstaller relaunches itself from a temp copy, so poll for removal.
    Start-Process $uninstaller -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'
    $deadline = (Get-Date).AddSeconds(45)
    while ((Test-Path $app) -and (Get-Date) -lt $deadline)
    {
        Start-Sleep -Milliseconds 500
    }
}
Check 'app folder removed' (-not (Test-Path $app))
Check 'Start Menu shortcut removed' (-not (Test-Path $shortcut))

Write-Host ''
if ($script:passed)
{
    Write-Host 'SMOKE TEST PASSED' -ForegroundColor Green
    exit 0
}
else
{
    Write-Host 'SMOKE TEST FAILED' -ForegroundColor Red
    exit 1
}

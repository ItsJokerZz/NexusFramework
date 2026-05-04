#!/usr/bin/env pwsh
<#
.SYNOPSIS
    NexusCheatFramework — WebUI Launch Script (PowerShell)

.DESCRIPTION
    Launches the NexusCheatFramework WebUI, connecting to a PS4/PS5
    console with NexusFramework payload loaded.

.PARAMETER ConsoleIp
    IP address of the PS4/PS5 with NexusFramework loaded (required).

.PARAMETER WebPort
    WebUI listen port (optional, default: 9080).

.EXAMPLE
    .\scripts\run-webui.ps1 192.168.1.100

.EXAMPLE
    .\scripts\run-webui.ps1 192.168.1.100 8080

.NOTES
    Prerequisites:
      - .NET SDK 8.0+ installed
      - NexusFramework payload loaded on the console
      - Console and host PC on the same network
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$ConsoleIp,

    [Parameter(Mandatory = $false, Position = 1)]
    [int]$WebPort = 9080
)

# --- Validate IP format ---
$ipRegex = '^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$'
if ($ConsoleIp -notmatch $ipRegex) {
    Write-Error "'$ConsoleIp' does not look like a valid IPv4 address"
    exit 1
}

# --- Validate port ---
if ($WebPort -lt 1 -or $WebPort -gt 65535) {
    Write-Error "Port must be between 1 and 65535, got $WebPort"
    exit 1
}

# --- Check dotnet ---
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Error ".NET SDK not found. Please install .NET SDK 8.0+ from https://dotnet.microsoft.com/download"
    exit 1
}

# --- Determine project directory ---
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir
$WebProject = Join-Path $ProjectDir "src\NexusCheatFramework.Web"

if (-not (Test-Path (Join-Path $WebProject "NexusCheatFramework.Web.csproj"))) {
    Write-Error "WebUI project not found at $WebProject"
    exit 1
}

# --- Print info ---
Write-Host "============================================"
Write-Host " NexusCheatFramework WebUI"
Write-Host "============================================"
Write-Host " Console IP:  $ConsoleIp"
Write-Host " WebUI Port:  $WebPort"
Write-Host " WebUI URL:   http://localhost:$WebPort"
Write-Host ""
Write-Host " IMPORTANT: Make sure the NexusFramework payload"
Write-Host " is already running on the console ($ConsoleIp)."
Write-Host ""
Write-Host " Open the WebUI in your browser at:"
Write-Host "   http://localhost:$WebPort"
Write-Host ""
Write-Host " On the PS4/PS5 browser, open:"
Write-Host "   http://<YOUR-PC-IP>:$WebPort"
Write-Host "============================================"
Write-Host ""

# --- Launch WebUI ---
Set-Location $ProjectDir
dotnet run --project $WebProject -- --ip $ConsoleIp --port $WebPort

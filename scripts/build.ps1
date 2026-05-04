#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

Write-Host "==> dotnet --info"
dotnet --info | Select-Object -First 5

Write-Host "==> restore"
dotnet restore NexusCheatFramework.sln

Write-Host "==> build"
dotnet build   NexusCheatFramework.sln -c Release --nologo

Write-Host "==> test"
dotnet test    NexusCheatFramework.sln -c Release --nologo --no-build

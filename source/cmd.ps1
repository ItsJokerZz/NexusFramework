param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("build","send","buildsend")]
    [string]$Command,

    [Parameter(Position=1)]
    [string]$MakeOpt,

    [Parameter(Position=2)]
    [string]$TargetIP
)

# -------------------- Logging --------------------
function Log { param($Level, $Msg)
    $color = switch ($Level.ToLower()) {
        "info"  { "Cyan" }
        "okay"  { "Green" }
        "warn"  { "Yellow" }
        "error" { "Red" }
        default { "White" }
    }
    Write-Host ("[{0:HH:mm:ss}] [{1}] {2}" -f (Get-Date), $Level.ToUpper(), $Msg) -ForegroundColor $color
}

function Log-Info  { param($m) Log "INFO" $m }
function Log-Ok    { param($m) Log "OK"   $m }
function Log-Warn  { param($m) Log "WARN" $m }
function Log-Error { param($m) Log "ERROR"$m }

# -------------------- IP Config --------------------
$ps4_ip = "192.168.137.34"
$ps5_ip = "192.168.137.21"

function Update-ScriptIP {
    param ($VarName, $IP)
    $scriptPath = $PSCommandPath
    if (-not $scriptPath) { Log-Error "Cannot determine script path."; exit 1 }
    $content = Get-Content $scriptPath
    $regex = '^\s*\$' + [regex]::Escape($VarName) + '\s*=\s*"(.*)"\s*$'
    $found = $false
    for ($i=0; $i -lt $content.Length; $i++) {
        if ($content[$i] -match $regex) {
            $found = $true
            $currentIP = $matches[1]
            if ($currentIP -eq $IP) { return }
            $content[$i] = "`$$VarName = `"$IP`""
            Set-Content -Path $scriptPath -Value $content -Force
            Log-Ok "Updated $VarName in script to $IP"
            return
        }
    }
    if (-not $found) {
        $content += "`$$VarName = `"$IP`""
        Set-Content -Path $scriptPath -Value $content -Force
        Log-Ok "Added $VarName in script as $IP"
    }
}

function Resolve-IP {
    param ($MakeOpt, $TargetIP)
    $ipVar = switch ($MakeOpt) { "ps4" { "ps4_ip" } "ps5" { "ps5_ip" } default { $null } }
    if (-not $ipVar) { Log-Error "Unknown console: $MakeOpt"; exit 1 }
    $currentIP = Get-Variable -Name $ipVar -Scope Script -ValueOnly
    if ($TargetIP) {
        if (-not [System.Net.IPAddress]::TryParse($TargetIP,[ref]$null)) { Log-Error "Invalid IP $TargetIP"; exit 1 }
        Update-ScriptIP -VarName $ipVar -IP $TargetIP
        return $TargetIP
    }
    if (-not [string]::IsNullOrWhiteSpace($currentIP)) {
        Log-Info "$ipVar is already set to $currentIP, using existing value"
        return $currentIP
    }
    Log-Error "$MakeOpt IP not set. Provide TargetIP or set it in the script."
    exit 1
}

# -------------------- Build --------------------
function Run-WSLBuild { param($MakeOpt)
    $arg = if ($MakeOpt) { " $MakeOpt" } else { "" }
    $cmd = "export PS4_PAYLOAD_SDK=/opt/ps4-payload-sdk; export PS5_PAYLOAD_SDK=/opt/ps5-payload-sdk; ./build.sh$arg"
    Log-Info "Starting WSL build..."
    wsl bash -c $cmd
    if ($LASTEXITCODE -ne 0) { Log-Error "WSL build failed"; exit 1 }
    Log-Ok "WSL build completed"
}

function Run-MSBuild {
    $Solution = Join-Path $PSScriptRoot "libraries/C#/NexusFramework.slnx"
    $dllOutput = Join-Path $PSScriptRoot "libraries/C#/bin/Release/netstandard2.1/NexusFramework.dll"
    if (Test-Path $dllOutput) { Remove-Item $dllOutput }
    $Config = "Release"; $Platform="Any CPU"
    Log-Info ("Building solution ({0} | {1})..." -f $Config, $Platform)
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) { Log-Error "vswhere.exe not found"; exit 1 }
    $msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
    if (-not $msbuild) { Log-Error "MSBuild not found"; exit 1 }
    $log = Join-Path $env:TEMP "msbuild_temp.log"
    & $msbuild $Solution /p:Configuration=$Config /p:Platform="$Platform" /t:Build *> $log
    if ($LASTEXITCODE -ne 0) { Get-Content $log; Log-Error "MSBuild failed"; exit 1 }
    Remove-Item $log -ErrorAction SilentlyContinue
    Log-Ok "MSBuild succeeded"
}

# -------------------- Networking --------------------
function Test-PortOpen { param($IP,$Port)
    try { $c=[Net.Sockets.TcpClient]::new(); $c.Connect($IP,$Port); $c.Close(); return $true } catch { return $false }
}

function Send-TcpPayload {
    param($IP,$Port,$Path,$Retries=5)
    if (-not (Test-Path $Path)) { Log-Error "File not found: $Path"; exit 1 }
    $bytes = [IO.File]::ReadAllBytes($Path)

    for ($i=1; $i -le $Retries; $i++) {
        try {
            $client = [Net.Sockets.TcpClient]::new()
            $client.Connect($IP,$Port)
            $stream = $client.GetStream()
            $stream.Write($bytes,0,$bytes.Length)
            $stream.Close(); $client.Close()
            Log-Ok ("Sent {0} bytes -> {1}:{2}" -f $bytes.Length, $IP, $Port)
            return
        }
        catch {
            Log-Warn ("Attempt {0} failed to connect to {1}:{2}, retrying..." -f $i, $IP, $Port)
            Start-Sleep -Seconds 1
        }
    }
    Log-Error ("Failed to send payload to {0}:{1} after {2} attempts" -f $IP, $Port, $Retries)
    exit 1
}

function Send-Payload {
    param($MakeOpt,$IP)
    switch ($MakeOpt) {
        "ps4" {
            $ElfLdr = Join-Path $PSScriptRoot "libraries/C#/payloads/elfldr-ps4.elf"
            $Nexus  = Join-Path $PSScriptRoot "console/output/nexus-ps4.elf"
            Log-Info "Sending elfldr..."
            Send-TcpPayload $IP 9090 $ElfLdr -Retries 5
            Log-Info "Waiting for listener (9021)..."
            for ($i=0; $i -lt 10; $i++) {
                if (Test-PortOpen $IP 9021) { break }
                Start-Sleep -Seconds 1
            }
        }
        "ps5" {
            $Nexus = Join-Path $PSScriptRoot "console/output/nexus-ps5.elf"
        }
        default { Log-Warn "No payload send logic for $MakeOpt" }
    }
    
    Start-Sleep -Seconds 1
    Send-TcpPayload $IP 9021 $Nexus -Retries 5

    Log-Ok "Payload delivery completed"
}

# -------------------- Main Switch --------------------
switch ($Command) {
    "build" {
        if (-not $MakeOpt) { Log-Error "Provide MakeOpt (ps4/ps5) for build"; exit 1 }
        $BuildOpt = $MakeOpt.ToLower()
        Run-WSLBuild -MakeOpt $BuildOpt
        Run-MSBuild
    }
    "send" {
        if (-not $MakeOpt) { Log-Error "Provide target console (ps4/ps5)"; exit 1 }
        $Console = $MakeOpt.ToLower()
        $IP = Resolve-IP -MakeOpt $Console -TargetIP $TargetIP
        Send-Payload -MakeOpt $Console -IP $IP
    }
    "buildsend" {
        if (-not $MakeOpt) { Log-Error "Provide MakeOpt (ps4/ps5)"; exit 1 }
        $BuildOpt = $MakeOpt.ToLower()
        Run-WSLBuild -MakeOpt $BuildOpt
        Run-MSBuild
        $IP = Resolve-IP -MakeOpt $BuildOpt -TargetIP $TargetIP
        Send-Payload -MakeOpt $BuildOpt -IP $IP
    }
    default { Log-Error "Unknown command: $Command"; exit 1 }
}

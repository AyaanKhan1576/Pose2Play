# Phase 7 - Start RL API Server and Demo
# This script starts the Flask API server and opens the web demo

param(
	[ValidateSet('Auto', 'Local', 'Quest')]
	[string]$Mode = 'Auto',
	[string]$QuestIp = ''
)

Write-Host "================================" -ForegroundColor Cyan
Write-Host " Phase 7: RL Web Integration" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

$mlPath = "D:\University\FYP\Pose2Play_BaseModel\ml"

# Auto-detect active IPv4 (avoids stale hardcoded IP after network changes)

function Get-PrimaryIPv4 {
	$ip = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
	Where-Object {
		$_.IPAddress -ne '127.0.0.1' -and
		$_.IPAddress -notlike '169.254*' -and
		$_.IPAddress -notlike '0.*' -and
		$_.PrefixOrigin -ne 'WellKnown'
	} |
	Sort-Object -Property InterfaceMetric |
	Select-Object -First 1 -ExpandProperty IPAddress)

	if ([string]::IsNullOrWhiteSpace($ip)) { return '127.0.0.1' }
	return $ip
}

function Get-SubnetBroadcast([string]$ipAddress) {
	try {
		$ipObj = Get-NetIPAddress -AddressFamily IPv4 -IPAddress $ipAddress -ErrorAction Stop | Select-Object -First 1
		if ($null -eq $ipObj) { return $null }

		$ipBytes = [System.Net.IPAddress]::Parse($ipAddress).GetAddressBytes()
		$prefix = [int]$ipObj.PrefixLength
		$maskBytes = New-Object byte[] 4
		for ($i = 0; $i -lt 4; $i++) {
			$bits = [Math]::Min([Math]::Max($prefix - ($i * 8), 0), 8)
			if ($bits -eq 0) { $maskBytes[$i] = 0 }
			elseif ($bits -eq 8) { $maskBytes[$i] = 255 }
			else { $maskBytes[$i] = [byte](256 - [Math]::Pow(2, 8 - $bits)) }
		}

		$broadcastBytes = New-Object byte[] 4
		for ($i = 0; $i -lt 4; $i++) {
			$broadcastBytes[$i] = [byte](($ipBytes[$i] -band $maskBytes[$i]) -bor (255 -bxor $maskBytes[$i]))
		}

		return ([System.Net.IPAddress]::new($broadcastBytes)).ToString()
	}
	catch {
		return $null
	}
}

function Get-QuestIpFromAdb {
	$adbPath = "C:\Program Files\Unity\Hub\Editor\6000.2.7f2\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
	if (-not (Test-Path $adbPath)) { return $null }

	try {
		$devices = & $adbPath devices 2>$null
		if ($LASTEXITCODE -ne 0 -or ($devices -join "`n") -notmatch "\tdevice") { return $null }

		$ipOutput = & $adbPath shell ip -f inet addr show wlan0 2>$null
		if ($LASTEXITCODE -ne 0) { return $null }

		$match = [regex]::Match(($ipOutput -join "`n"), 'inet\s+(\d+\.\d+\.\d+\.\d+)/')
		if ($match.Success) { return $match.Groups[1].Value }
		return $null
	}
	catch {
		return $null
	}
}

function Add-UniqueTarget([System.Collections.Generic.List[string]]$targets, [string]$value) {
	if ([string]::IsNullOrWhiteSpace($value)) { return }
	if (-not $targets.Contains($value)) { $targets.Add($value) }
}

$desktopIp = Get-PrimaryIPv4

$subnetBroadcast = Get-SubnetBroadcast -ipAddress $desktopIp
$resolvedQuestIp = if ([string]::IsNullOrWhiteSpace($QuestIp)) { Get-QuestIpFromAdb } else { $QuestIp }

$udpTargets = New-Object 'System.Collections.Generic.List[string]'

if ($Mode -eq 'Local') {
	Add-UniqueTarget $udpTargets '127.0.0.1'
	Add-UniqueTarget $udpTargets $desktopIp
}

if ($Mode -eq 'Quest') {
	Add-UniqueTarget $udpTargets $resolvedQuestIp
	Add-UniqueTarget $udpTargets $subnetBroadcast
	Add-UniqueTarget $udpTargets '255.255.255.255'
}

if ($Mode -eq 'Auto') {
	Add-UniqueTarget $udpTargets '127.0.0.1'
	Add-UniqueTarget $udpTargets $desktopIp
	Add-UniqueTarget $udpTargets $resolvedQuestIp
	Add-UniqueTarget $udpTargets $subnetBroadcast
	Add-UniqueTarget $udpTargets '255.255.255.255'
}

if ($udpTargets.Count -eq 0) {
	Add-UniqueTarget $udpTargets '127.0.0.1'
}

$unityUdpTarget = [string]::Join(',', $udpTargets)

# Auto-mode fallback if Quest IP not available
if ($Mode -eq 'Quest' -and [string]::IsNullOrWhiteSpace($resolvedQuestIp)) {
	Write-Host "Quest IP not detected via ADB; using broadcast targets." -ForegroundColor Yellow
}

# Start Flask server (serves both API and demo)
Write-Host "Starting Pose2Play in $Mode mode..." -ForegroundColor Cyan
$serverScript = @'
cd "{0}"

# Quest/LAN-ready backend defaults

$env:POSE2PLAY_SERVER_HOST = '0.0.0.0'
$env:POSE2PLAY_SERVER_PORT = '5000'
$env:UNITY_UDP_IP = '{1}'
$env:UNITY_POSE_UDP_PORT = '5055'
$env:UNITY_DASHBOARD_UDP_PORT = '5056'
$env:POSE2PLAY_DESKTOP_IP = '{2}'

python api_server.py
'@ -f $mlPath, $unityUdpTarget, $desktopIp
Start-Process powershell -ArgumentList "-NoExit", "-Command", $serverScript

Write-Host "Waiting for server..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Open browser
Write-Host "Opening browser..." -ForegroundColor Cyan
Start-Process "http://localhost:5000"

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Pose2Play Running!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "  URL: http://localhost:5000" -ForegroundColor Cyan
Write-Host "  Desktop IPv4: $desktopIp" -ForegroundColor Cyan
if (-not [string]::IsNullOrWhiteSpace($resolvedQuestIp)) {
	Write-Host "  Quest IPv4: $resolvedQuestIp" -ForegroundColor Cyan
}
if (-not [string]::IsNullOrWhiteSpace($subnetBroadcast)) {
	Write-Host "  Subnet broadcast: $subnetBroadcast" -ForegroundColor Cyan
}
Write-Host "  UDP targets: $unityUdpTarget" -ForegroundColor Cyan
Write-Host ""
Write-Host "  1. ALLOW camera when browser asks" -ForegroundColor Yellow
Write-Host "  2. Click 'Start Detection'" -ForegroundColor Yellow
Write-Host "  3. Do shoulder exercises!" -ForegroundColor Yellow
Write-Host ""
Write-Host "Close the server window to stop" -ForegroundColor Gray
Write-Host ""

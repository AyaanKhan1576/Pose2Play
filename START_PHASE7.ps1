# Phase 7 - Start RL API Server and Demo
# This script starts the Flask API server and opens the web demo

param(
	[ValidateSet('Local', 'Quest')]
	[string]$Mode = 'Local'
)

Write-Host "================================" -ForegroundColor Cyan
Write-Host " Phase 7: RL Web Integration" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

$mlPath = "D:\University\FYP\Pose2Play_BaseModel\ml"
$desktopIp = "192.168.18.30"

$unityUdpTarget = if ($Mode -eq 'Quest') { '255.255.255.255' } else { "127.0.0.1,$desktopIp" }

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

python api_server.py
'@ -f $mlPath, $unityUdpTarget
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
Write-Host "  UDP target: $unityUdpTarget" -ForegroundColor Cyan
Write-Host ""
Write-Host "  1. ALLOW camera when browser asks" -ForegroundColor Yellow
Write-Host "  2. Click 'Start Detection'" -ForegroundColor Yellow
Write-Host "  3. Do shoulder exercises!" -ForegroundColor Yellow
Write-Host ""
Write-Host "Close the server window to stop" -ForegroundColor Gray
Write-Host ""

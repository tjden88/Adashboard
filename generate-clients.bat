@echo off
cd /d %~dp0

echo === Generation RefitClients ===
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate-api-clients.ps1
if errorlevel 1 (
	echo === Generation failed ===
	pause
	exit /b 1
)

echo === Done ===
pause

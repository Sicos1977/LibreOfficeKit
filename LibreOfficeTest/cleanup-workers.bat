@echo off
echo Killing any remaining worker processes...
taskkill /F /IM LibreOfficeKitWorker.exe 2>nul
if %ERRORLEVEL%==128 (
	echo No worker processes found
) else (
	echo Worker processes killed
)
timeout /t 1 /nobreak >nul
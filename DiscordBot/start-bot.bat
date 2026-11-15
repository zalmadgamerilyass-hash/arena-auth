@echo off
title Arena Discord Bot
cd /d "%~dp0"
echo =========================================================
echo             Starting Arena Discord Bot...
echo =========================================================
if not exist node_modules (
    echo [INFO] Installing required packages...
    call npm install
)
node index.js
pause

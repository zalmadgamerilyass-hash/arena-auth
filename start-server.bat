@echo off
title POWER X Server
cd /d "%~dp0BlackoutAuth.Server"
echo =========================================================
echo             Starting POWER X Server...
echo =========================================================
dotnet run
pause

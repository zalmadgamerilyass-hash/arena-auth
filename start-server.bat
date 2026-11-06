@echo off
title Blackout Auth Server
cd /d "%~dp0BlackoutAuth.Server"
echo =========================================================
echo             Starting Blackout Auth Server...
echo =========================================================
dotnet run
pause

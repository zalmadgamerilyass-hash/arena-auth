@echo off
title Arena Auth Server
cd /d "%~dp0BlackoutAuth.Server"
echo =========================================================
echo             Starting Arena Auth Server...
echo =========================================================
dotnet run
pause

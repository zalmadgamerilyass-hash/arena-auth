@echo off
title Building Blackout Auth C++ Client Demo...
echo =========================================================
echo    Compiling Blackout Auth C++ Native Client Demo
echo =========================================================

REM Locate Visual Studio 2022 VC Environment
set VCVARS="C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"

if not exist %VCVARS% (
    echo [ERROR] Visual Studio 2022 vcvars64.bat not found at %VCVARS%
    echo Please verify your Visual Studio installation.
    pause
    exit /b 1
)

call %VCVARS%

cd /d "%~dp0BlackoutAuth.Demo.Cpp"
cl /EHsc /std:c++17 main.cpp /Fe:BlackoutDemoCpp.exe /link winhttp.lib bcrypt.lib crypt32.lib advapi32.lib

if %ERRORLEVEL% EQU 0 (
    echo.
    echo =========================================================
    echo [SUCCESS] BlackoutDemoCpp.exe built successfully!
    echo Location: %~dp0BlackoutAuth.Demo.Cpp\BlackoutDemoCpp.exe
    echo =========================================================
    del main.obj >nul 2>&1
) else (
    echo [ERROR] Compilation failed.
)

pause

@echo off
setlocal DisableDelayedExpansion

set "APPDOCK_DIRECTORY="
if not "%~1"=="" (
    set "APPDOCK_DIRECTORY=%~f1"
) else if exist "%~dp0deploy.local.txt" (
    set /p "APPDOCK_DIRECTORY="<"%~dp0deploy.local.txt"
)

if defined APPDOCK_DIRECTORY (
    pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\install-local.ps1" -AppDockDirectory "%APPDOCK_DIRECTORY%"
) else (
    pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\install-local.ps1"
)

exit /b %errorlevel%

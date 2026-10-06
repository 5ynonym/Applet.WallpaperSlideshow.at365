@echo off
setlocal

pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\publish.ps1" %*
exit /b %errorlevel%

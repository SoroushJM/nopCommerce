@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-storefront.ps1" %*
if errorlevel 1 (
    echo.
    echo Local startup failed. See the message above.
    pause
)

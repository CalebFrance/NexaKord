@echo off
rem NexaKord - uninstall (also removes the older GamepadMIDI v1 / v2 if present). No administrator rights needed.
if /I "%~1"=="go" goto :main
start "NexaKord uninstall" cmd /k ""%~f0" go"
exit /b

:main
setlocal EnableExtensions
title Uninstalling NexaKord
cd /d "%~dp0"
set "HERE=%~dp0"
set "TOOLS=%HERE%tools"
set "RUNKEY=HKCU\Software\Microsoft\Windows\CurrentVersion\Run"
echo.
echo  This removes NexaKord from this PC: the Desktop and Start menu shortcuts and NexaKord.exe.
echo.
choice /C YN /M "Continue"
if errorlevel 2 goto :cancel

echo Stopping NexaKord if it is running...
taskkill /F /IM NexaKord.exe >nul 2>nul
taskkill /F /IM GamepadMIDI.exe >nul 2>nul
timeout /t 2 /nobreak >nul

echo Removing shortcuts...
if exist "%TOOLS%\shortcuts.ps1" powershell -NoProfile -ExecutionPolicy Bypass -File "%TOOLS%\shortcuts.ps1" -Action remove >nul 2>nul

echo Deleting the program...
if exist "%HERE%NexaKord.exe" del /f /q "%HERE%NexaKord.exe"
if exist "%TOOLS%\find-old.ps1" for /f "usebackq delims=" %%P in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%TOOLS%\find-old.ps1"`) do del /f /q "%%P" >nul 2>nul
if exist "%LOCALAPPDATA%\GamepadMIDI" rmdir /s /q "%LOCALAPPDATA%\GamepadMIDI" >nul 2>nul
reg delete "%RUNKEY%" /v NexaKord /f >nul 2>nul
reg delete "%RUNKEY%" /v GamepadMIDI /f >nul 2>nul

echo.
choice /C YN /M "Do you want to remove your profiles and settings"
if errorlevel 2 goto :keep
if exist "%APPDATA%\NexaKord" rmdir /s /q "%APPDATA%\NexaKord"
if exist "%APPDATA%\GamepadMIDI" rmdir /s /q "%APPDATA%\GamepadMIDI"
echo Profiles and settings deleted.
goto :done

:keep
echo Your profiles and settings were kept in %APPDATA%\NexaKord

:done
echo.
echo Uninstall complete. You can close this window.
pause
goto :eof

:cancel
echo Nothing was changed.
pause
goto :eof

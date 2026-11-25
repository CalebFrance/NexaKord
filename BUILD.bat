@echo off
rem NexaKord v3 - build, install or upgrade. Per-user: NO administrator rights needed.
rem It relaunches itself in a window that cannot close on its own, so every message stays readable.
if /I "%~1"=="go" goto :main
start "NexaKord build" cmd /k ""%~f0" go"
exit /b

:main
setlocal EnableExtensions
title Building NexaKord
cd /d "%~dp0"
set "HERE=%~dp0"
set "SRC=%HERE%src"
set "TOOLS=%HERE%tools"
set "WORK=%TEMP%\NexaKord_build"
set "LOG=%HERE%build-log.txt"
set "OLDDATA=%APPDATA%\GamepadMIDI"
set "NEWDATA=%APPDATA%\NexaKord"
set "RUNKEY=HKCU\Software\Microsoft\Windows\CurrentVersion\Run"
set "UPGRADE="
set "AUTO="
set "FAIL="
echo.
echo  ====================================================
echo    NexaKord v3  -  build / install / upgrade
echo    Made by Caleb France  -  By Canexa Studios
echo  ====================================================
echo.

echo [1/6] Checking the files...
if not exist "%SRC%\Core.cs" goto :nosrc
if not exist "%TOOLS%\find-old.ps1" goto :nosrc
if not exist "%TOOLS%\shortcuts.ps1" goto :nosrc
if exist "%SRC%\App.cs" echo    removing a leftover V2 file: src\App.cs & del /f /q "%SRC%\App.cs"
if exist "%SRC%\GamepadMIDI.cs" echo    removing a leftover V1 file: src\GamepadMIDI.cs & del /f /q "%SRC%\GamepadMIDI.cs"
call :need AssemblyInfo.cs "AssemblyTitle"
call :need Core.cs "class Engine"
call :need Data.cs "static class Templates"
call :need Theory.cs "static class Theory"
call :need Controls.cs "class PadView"
call :need GuitarSolver.cs "static class GuitarSolver"
call :need GuitarView.cs "class GuitarView"
call :need LearnForm.cs "class LearnForm"
call :need UI.cs "static void Main"
if defined FAIL goto :badfiles
echo    all 9 source files look right.

echo [2/6] Looking for the C# compiler that comes with Windows...
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" goto :nocsc
echo    found it.

echo [3/6] Closing any running copy (NexaKord, or the older GamepadMIDI)...
taskkill /F /IM NexaKord.exe >nul 2>nul
taskkill /F /IM GamepadMIDI.exe >nul 2>nul
timeout /t 2 /nobreak >nul
if exist "%HERE%NexaKord.exe" set "UPGRADE=1"
if exist "%LOCALAPPDATA%\GamepadMIDI\GamepadMIDI.exe" set "UPGRADE=1"
if exist "%OLDDATA%\settings.txt" set "UPGRADE=1"
if exist "%NEWDATA%\settings.txt" set "UPGRADE=1"
reg query "%RUNKEY%" /v GamepadMIDI >nul 2>nul
if not errorlevel 1 set "UPGRADE=1" & set "AUTO=1"
reg query "%RUNKEY%" /v NexaKord >nul 2>nul
if not errorlevel 1 set "UPGRADE=1" & set "AUTO=1"
for /f "usebackq delims=" %%P in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%TOOLS%\find-old.ps1"`) do set "UPGRADE=1"
if defined UPGRADE echo    Upgrading from previous version...
if not defined UPGRADE echo    no older version found: fresh install.

echo [4/6] Building NexaKord v3. Your old program is only replaced if this succeeds...
if exist "%WORK%" rmdir /s /q "%WORK%" >nul 2>nul
mkdir "%WORK%" >nul 2>nul
"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /out:"%WORK%\NexaKord.exe" /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "%SRC%\*.cs" > "%LOG%" 2>&1
if errorlevel 1 goto :buildfail
if not exist "%WORK%\NexaKord.exe" goto :buildfail
echo    build OK.

:: Ensure the DLLs are next to the exe
copy /y "%HERE%NAudio.dll" "%HERE%" >nul 2>&1

echo [5/6] Installing...
copy /y "%WORK%\NexaKord.exe" "%HERE%NexaKord.exe" >nul
if errorlevel 1 goto :copyfail
for /f "usebackq delims=" %%P in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%TOOLS%\find-old.ps1"`) do call :oldexe "%%P"
if exist "%LOCALAPPDATA%\GamepadMIDI" rmdir /s /q "%LOCALAPPDATA%\GamepadMIDI" >nul 2>nul
if exist "%OLDDATA%" if not exist "%NEWDATA%" xcopy "%OLDDATA%" "%NEWDATA%\" /E /I /Y /Q >nul
if exist "%NEWDATA%\profiles" echo    your profiles and settings carried over.
reg delete "%RUNKEY%" /v GamepadMIDI /f >nul 2>nul
if defined AUTO reg add "%RUNKEY%" /v NexaKord /t REG_SZ /d "\"%HERE%NexaKord.exe\" /minimized" /f >nul 2>nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%TOOLS%\shortcuts.ps1" -Action cleanold >nul 2>nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%TOOLS%\shortcuts.ps1" -Action create -Target "%HERE%NexaKord.exe" >nul 2>nul
rmdir /s /q "%WORK%" >nul 2>nul
echo    NexaKord.exe is in this folder. Shortcuts added to your Desktop and Start menu.

echo [6/6] Starting NexaKord...
start "" "%HERE%NexaKord.exe"
echo.
echo  ====================================================
echo    DONE. NexaKord v3 is running.
echo    If its window did not appear, use the NexaKord icon
echo    on your Desktop. You can close this window.
echo  ====================================================
goto :eof

:need
if not exist "%SRC%\%~1" echo    MISSING FILE: %~1 & set "FAIL=1" & goto :eof
findstr /C:%2 "%SRC%\%~1" >nul 2>nul
if errorlevel 1 echo    WRONG CONTENT: %~1 does not contain what it should & set "FAIL=1"
goto :eof

:oldexe
echo    removing older program: %~1
if exist "%~1" del /f /q "%~1" >nul 2>nul
goto :eof

:nosrc
echo.
echo    The install files are not all here. Extract the WHOLE zip into a folder first
echo    (right-click the zip, Extract All), then double-click BUILD.bat in that folder.
echo    Nothing was changed.
pause
goto :eof

:badfiles
echo.
echo    One or more source files are missing or mixed up. Delete this folder, extract the
echo    zip again and run BUILD.bat from the fresh folder. Do not rename or move files.
echo    Nothing was changed.
pause
goto :eof

:nocsc
echo.
echo    Could not find the .NET Framework 4 compiler (csc.exe). It normally comes with
echo    Windows 10 and 11. Install Windows updates, then run this again.
pause
goto :eof

:buildfail
echo.
echo    THE BUILD FAILED. The error is shown below and saved in build-log.txt.
echo    Your previous NexaKord.exe (if any) was not touched.
echo.
type "%LOG%"
echo.
pause
goto :eof

:copyfail
echo.
echo    Could not write NexaKord.exe into this folder. Close NexaKord if it is open
echo    (tray icon near the clock, Quit), make sure the folder is not read-only, run again.
pause
goto :eof

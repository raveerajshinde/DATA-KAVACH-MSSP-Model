@echo off
REM Data Kavach - installer
REM Double-click, or right-click and choose "Run as administrator".
setlocal EnableExtensions
title Data Kavach - installer
cd /d "%~dp0"
set "DK_DIR=%~dp0"
set "DK_ARGS=%*"

net session >nul 2>&1
if errorlevel 1 goto :elevate
goto :run

:elevate
echo Requesting administrator rights...
echo If nothing happens, right-click this file and choose "Run as administrator".
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath $env:ComSpec -ArgumentList ('/k ""' + $env:DK_DIR + '%~nx0"" ' + $env:DK_ARGS) -Verb RunAs"
if errorlevel 1 echo Could not elevate. Right-click this file and choose "Run as administrator".
echo.
pause
exit /b

:run
echo Package folder: %DK_DIR%
if not exist "%DK_DIR%install.ps1" goto :noscript
if not exist "%DK_DIR%app\DataKavach.Api.dll" goto :noapp
echo Unblocking downloaded files...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath $env:DK_DIR -Recurse | Unblock-File" >nul 2>&1
echo Starting install.ps1 ...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%DK_DIR%install.ps1" %DK_ARGS%
set "RC=%errorlevel%"
echo.
if "%RC%"=="0" goto :ok
echo *** Finished with errors (code %RC%). See the log under C:\DataKavach\logs ***
goto :end

:ok
echo Installation completed.
goto :end

:noscript
echo ERROR: install.ps1 was not found in %DK_DIR%
echo Put this file in the extracted DataKavach_1.0.0 package folder.
set "RC=1"
goto :end

:noapp
echo ERROR: the "app" folder (app\DataKavach.Api.dll) was not found in %DK_DIR%
echo Put this file in the extracted DataKavach_1.0.0 package folder.
set "RC=1"
goto :end

:end
echo.
pause
exit /b %RC%

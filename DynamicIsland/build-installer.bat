@echo off
setlocal
cd /d "%~dp0"

rem ===== 1. .NET SDK: if missing, download it automatically =====
set "DOTNET=dotnet"
where dotnet >nul 2>nul
if not errorlevel 1 goto :have_dotnet
if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe" & goto :have_dotnet

echo .NET SDK not found - installing...
where winget >nul 2>nul
if not errorlevel 1 winget install -e --id Microsoft.DotNet.SDK.8 --silent --accept-package-agreements --accept-source-agreements
if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe" & goto :have_dotnet

echo winget unavailable or failed - downloading the official installer script...
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile '%TEMP%\dotnet-install.ps1'; & '%TEMP%\dotnet-install.ps1' -Channel 8.0 -InstallDir '%LocalAppData%\Microsoft\dotnet'"
if exist "%LocalAppData%\Microsoft\dotnet\dotnet.exe" set "DOTNET=%LocalAppData%\Microsoft\dotnet\dotnet.exe" & goto :have_dotnet
goto :no_dotnet

:have_dotnet

rem ===== 2. Publish (framework-dependent: small exe, installer fetches .NET Runtime itself) =====
echo [1/2] Publishing application...
"%DOTNET%" publish DynamicIsland.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o publish
if errorlevel 1 goto :publish_failed

rem ===== 3. Inno Setup: if missing, download it automatically =====
call :find_inno
if exist "%ISCC%" goto :have_inno

echo Inno Setup not found - installing...
where winget >nul 2>nul
if not errorlevel 1 winget install -e --id JRSoftware.InnoSetup --silent --accept-package-agreements --accept-source-agreements
call :find_inno
if exist "%ISCC%" goto :have_inno

echo winget unavailable or failed - downloading Inno Setup directly...
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest https://jrsoftware.org/download.php/is.exe -OutFile '%TEMP%\innosetup.exe'"
if exist "%TEMP%\innosetup.exe" "%TEMP%\innosetup.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER
call :find_inno
if not exist "%ISCC%" goto :no_inno

:have_inno
echo [2/2] Building installer...
"%ISCC%" installer\DynamicIsland.iss
if errorlevel 1 goto :iss_failed

copy /y "publish\DynamicIsland.exe" "installer\Output\DynamicIsland.exe" >nul

echo.
echo Done. In installer\Output:
echo   DinamicIslandsetap.exe  - installer (downloads .NET 8 Runtime by itself if needed)
echo   DynamicIsland.exe       - portable exe (needs .NET 8 Desktop Runtime)
pause
exit /b 0

:find_inno
set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
exit /b 0

:no_dotnet
echo Could not install .NET SDK automatically. Download: https://dotnet.microsoft.com/download/dotnet/8.0
pause
exit /b 1

:publish_failed
echo Publish error.
pause
exit /b 1

:no_inno
echo Could not install Inno Setup automatically. Download: https://jrsoftware.org/isdl.php
pause
exit /b 1

:iss_failed
echo Installer build error.
pause
exit /b 1

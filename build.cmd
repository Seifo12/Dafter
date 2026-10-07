@echo off
setlocal EnableExtensions
set ROOT=%~dp0
dotnet restore CourtDesk.sln --source "%ROOT%vendor\packages" || exit /b 1
dotnet test tests\CourtDesk.Tests\CourtDesk.Tests.csproj -c Release --no-restore || exit /b 1
dotnet publish src\CourtDesk\CourtDesk.csproj -c Release --no-restore -o dist || exit /b 1
set SELFTEST_DIR=%TEMP%\courtdesk-selftest
if exist "%SELFTEST_DIR%" rmdir /s /q "%SELFTEST_DIR%"
mkdir "%SELFTEST_DIR%"
copy /y dist\CourtDesk.exe "%SELFTEST_DIR%\CourtDesk.exe" >nul || exit /b 1
pushd "%SELFTEST_DIR%"
CourtDesk.exe --selftest --home "%SELFTEST_DIR%\home"
set ST=%ERRORLEVEL%
popd
if not "%ST%"=="0" exit /b %ST%
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%tools\Make-Dist.ps1" || exit /b 1

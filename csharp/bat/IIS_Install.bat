@echo off
(reg query "hku\S-1-5-19">NUL 2>&1) || powershell -Command "Start-Process -FilePath '%~f0' -ArgumentList '::' -Verb RunAs" && exit
pushd %~dp0
for /f %%i in ('dir *Server.exe /b') do set name=%%~ni
set domain=http://*:80
set path=%path%;%windir%\System32\inetsrv
appcmd list apppool "%name%" >nul
set lastcode=%ERRORLEVEL%
if [%lastcode%]==[0] (
    echo "已存在应用程序池：%name%"
    goto :end
)
appcmd list site "%name%" >nul
set lastcode=%ERRORLEVEL%
if [%lastcode%]==[0] (
    echo "已存在网站：%name%"
    goto :end
)
appcmd add apppool -name:"%name%"
appcmd set apppool "%name%" -managedRuntimeVersion: -startMode:AlwaysRunning -recycling.periodicRestart.time:00:00:00 -recycling.disallowOverlappingRotation:true -processModel.idleTimeout:00:00:00 -failure.rapidFailProtectionMaxCrashes:99
icacls %cd% /grant "IIS APPPOOL\%name%":(OI)(CI)(F) /Q
appcmd add site -name:"%name%" -bindings:%domain% -physicalPath:"%CD%"
appcmd set site "%name%" -applicationDefaults.preloadEnabled:true
appcmd set app "%name%/" -applicationPool:"%name%"
appcmd set config "%name%" /section:system.webServer/applicationInitialization -doAppInitAfterRestart:true
:end
popd
pause > nul
exit /b %lastcode%
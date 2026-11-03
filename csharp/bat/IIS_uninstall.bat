@echo off
(reg query "hku\S-1-5-19">NUL 2>&1) || powershell -Command "Start-Process -FilePath '%~f0' -ArgumentList '::' -Verb RunAs" && exit
pushd %~dp0
for /f %%i in ('dir *Server.exe /b') do set name=%%~ni
set path=%path%;%windir%\System32\inetsrv
appcmd list site "%name%" >nul
set lastcode=%ERRORLEVEL%
if [%lastcode%]==[0] (
    appcmd delete site "%name%"
) else (    
    echo 未找到站点： %name%
)
appcmd list apppool "%name%" >nul
set lastcode=%ERRORLEVEL%
if [%lastcode%]==[0] (
    appcmd delete apppool "%name%"
) else (    
    echo 未找到应用程序池： %name%
)
:end
popd
pause > nul
exit /b %lastcode%
@echo off
(reg query "hku\S-1-5-19">NUL 2>&1) || powershell -Command "Start-Process -FilePath '%~f0' -ArgumentList 'rem' -Verb RunAs" && exit
pushd %~dp0
set lastcode=0
set binPath=
for /f %%i in ('dir *Server.exe /b') do set binPath=%%~fi
if [%binPath%]==[] (
    set lastcode=1
    echo 未发现有效的服务程序
    goto :end
)
for %%i in ("%binPath%") do set serverName=%%~ni
netsh advfirewall firewall show rule name="%serverName%" >nul 2>&1
set lastcode=%ERRORLEVEL%
if [%lastcode%]==[0] (
    echo 删除防火墙规则： %serverName%
    netsh advfirewall firewall delete rule name=%serverName%
) else (
    echo 未找到防火墙规则： %serverName%
)
schtasks /query /TN "\%serverName%\%serverName%" >nul 2>&1
set lastcode=%ERRORLEVEL%
if [%lastcode%]==[0] (
    echo 删除任务计划：\%serverName%\%serverName%
    schtasks /delete /TN "\%serverName%\%serverName%" /F
) else (
    echo 未找到任务计划： \%serverName%\%serverName%
)
sc qc "%serverName%" >nul 2>&1
set lastcode=%ERRORLEVEL%
if [%lastcode%]==[0] (
    echo 删除服务： %serverName%
    sc delete "%serverName%"
    sc stop "%serverName%"
) else ( 
    echo 未找到服务： %serverName%
)
:end
popd
pause >nul 2>&1
exit /b %lastcode%
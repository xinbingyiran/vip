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
set serverDisplayName=%serverName%
set serverDescription=%serverName%
sc qc "%serverName%" >nul 2>&1
set lastcode=%ERRORLEVEL%
if [%lastcode%]==[0] (
    echo 更新服务： %serverName%
    sc config "%serverName%" binpath= "\"%binPath%\"" displayname= "%serverDisplayName%" start= auto
) else ( 
    echo 安装服务： %serverName%
    sc create "%serverName%" binpath= "\"%binPath%\"" displayname= "%serverDisplayName%" start= auto
)
sc description "%serverName%" "%serverDescription%"
sc failure "%serverName%" reset= 300 actions= restart/10000/restart/60000//
schtasks /query /TN "\%serverName%\%serverName%" >nul 2>&1
set lastcode=%ERRORLEVEL%
if [%lastcode%]==[0] (
    echo 更新任务计划：\%serverName%\%serverName%
    schtasks /change /TN "\%serverName%\%serverName%" /TR "sc.exe start \"%serverName%\"" /RU SYSTEM
) else (
    echo 添加任务计划： \%serverName%\%serverName%
    schtasks /create /TN "\%serverName%\%serverName%" /SC MINUTE /MO 1 /TR "sc.exe start \"%serverName%\"" /RU SYSTEM
)
netsh advfirewall firewall show rule name="%serverName%" >nul 2>&1
set lastcode=%ERRORLEVEL%
if [%lastcode%]==[0] (
    echo 更新防火墙规则： %serverName%
    netsh advfirewall firewall set rule name=%serverName% dir=in new action=allow program="%binPath%"
) else (
    echo 添加防火墙规则： %serverName%
    netsh advfirewall firewall add rule name=%serverName% dir=in action=allow program="%binPath%"
)
:end
popd
pause >nul 2>&1
exit /b %lastcode%
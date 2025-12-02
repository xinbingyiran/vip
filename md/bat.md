# 延迟变量

```
setlocal EnableDelayedExpansion
```

# 请求以管理员权限运行

无参数
```
%1 mshta vbscript:CreateObject("Shell.Application").ShellExecute("cmd.exe","/c ""%~f0"" ::","%cd%","runas",1)(window.close)&&exit
1参数
%2 mshta vbscript:CreateObject("Shell.Application").ShellExecute("cmd.exe","/c ""%~f0"" ""%~1"" ::","%cd%","runas",1)(window.close)&&exit
```
```
(fltmc >nul 2>&1) || powershell -Command "Start-Process -Verb RunAs -FilePath '%~f0' -ArgumentList '%*'" && exit /b
```

# 运行powershell

无参数
```
@(set "0=%~f0"^)#) & powershell -nop -c iex([io.file]::ReadAllText($env:0)) & exit/b
```
2参数 powershell通过$env:1获取参数1
```
@(echo off% <#%) &color 07 &title Quick 11 iso esd wim TPM toggle by AveYo - with SendTo menu entry
set "0=%~f0" &set "1=%~1"&set "2=%~2"& powershell -nop -c iex ([io.file]::ReadAllText($env:0)) &pause &exit/b ||#>)[1]
```
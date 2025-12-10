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


# 按键模拟

```
const autoKeyboard = (c) => {
    let lastTime = [0, 0];
    function gameLoop(ts) {
        if (c.stop) { return; }
        if (lastTime[1] <= 0) {
            lastTime[0] = ts + Math.random() * (c.maxDelay - c.minDelay) + c.minDelay;
            lastTime[1] = lastTime[0] + Math.random() * (c.maxDown - c.minDown) + c.minDown;
        }
        else if (lastTime[0] > 0 && ts > lastTime[0]) {
            document.dispatchEvent(new KeyboardEvent("keydown", c));
            console.log("keydown");
            lastTime[0] = 0;
        }
        else if (lastTime[1] > 0 && ts > lastTime[1]) {
            document.dispatchEvent(new KeyboardEvent("keyup", c));
            console.log("keyup");
            lastTime[1] = 0;
        }
        requestAnimationFrame(gameLoop);
    }
    requestAnimationFrame(gameLoop);
};
var ctrl = { stop: 0, keyCode: 40, minDelay: 3000, maxDelay: 30000, minDown: 100,maxDown: 500 };
autoKeyboard(ctrl);
```
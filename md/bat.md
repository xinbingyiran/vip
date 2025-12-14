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
    const actionTime = {
        actions: [],
        next: 0
    };
    function gameLoop(ts) {
        if (c.stop) {
            return;
        }
        if (ts > actionTime.next) {
            const actions = [];
            actions.push({ type: 'keydown', ts: ts, repeat: false });
            const nextTs = ts + c.downMs + Math.random() * c.downMSRand;
            let currentTs = ts + c.repeatDelay;
            while (currentTs < nextTs) {
                actions.push({ type: 'keydown', ts: currentTs, repeat: true });
                currentTs += c.repeatRate;
            }
            actions.push({ type: 'keyup', ts: nextTs, repeat: false });
            actionTime.actions = actions;
            actionTime.next = nextTs + c.upMS + Math.random() * c.upMSRand;
        }
        if (actionTime.actions && actionTime.actions.length && ts > actionTime.actions[0].ts) {
            const actionItem = actionTime.actions.shift();
            document.dispatchEvent(new KeyboardEvent(actionItem.type, { key: c.key, code: c.code, keyCode: c.keyCode, repeat: actionItem.repeat }));
            c.log && console.log(`${actionItem.type} key:${c.key} code:${c.code}`);
        }
        requestAnimationFrame(gameLoop);
    }
    requestAnimationFrame(gameLoop);
    return c;
};
var ctrl = {
    stop: 0,
    log: 0,
    key: "z",
    code: "KeyZ",
    keyCode: 90,
    repeatDelay: 510,
    repeatRate: 35,
    downMs: 2100,
    downMSRand: 1200,
    upMS: 1100,
    upMSRand: 1100
};
autoKeyboard(ctrl);
```
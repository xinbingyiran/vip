
// 按键模拟
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

//# 间断性点按 ArrowDown
var d = {
    stop: 0,
    log: 0,
    key: "ArrowDown",
    code: "ArrowDown",
    keyCode: 40,
    repeatDelay: 510,
    repeatRate: 35,
    downMs: 200,
    downMSRand: 200,
    upMS: 1100,
    upMSRand: 23000
};
autoKeyboard(d);


// 间断性长按z
var z = {
    stop: 0,
    log: 0,
    key: "z",
    code: "KeyZ",
    keyCode: 90,
    repeatDelay: 510,
    repeatRate: 35,
    downMs: 1000,
    downMSRand: 500,
    upMS: 30100,
    upMSRand: 10300
};
autoKeyboard(z);

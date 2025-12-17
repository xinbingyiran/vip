!function () {
    if (globalThis._douyin_) {
        globalThis._douyin_.stoped = false;
        return;
    }
    const douyin = globalThis._douyin_ = {
        stoped: false,
        log: (message) => console.info(message)
    };
    douyin.start = () => {
        douyin.stoped = false;
        douyin.items.forEach(keyItem => delete keyItem.__actionItem__);
        requestAnimationFrame(douyin.gameLoop);
    }
    douyin.stop = () => {
        douyin.stoped = true;
    }
    douyin.addScript = (url, useCallback) => {
        var script = globalThis.document.createElement('script');
        script.setAttribute('type', 'text/javascript'), script.setAttribute('src', url), script.onload = useCallback, globalThis.document.getElementsByTagName('head')[0].appendChild(script);
    }
    douyin.checkAction = (keyItem, ts) => {
        const actionItem = keyItem.__actionItem__ ??= {
            actions: [],
            next: ts + keyItem.upMS + Math.random() * keyItem.upMSRand
        };
        if (!keyItem.check()) {
            actionItem.next = ts + keyItem.upMS + Math.random() * keyItem.upMSRand;
            return;
        }
        if (ts > actionItem.next) {
            const actions = actionItem.actions = [];
            actions.push({
                type: 'keydown',
                ts: ts,
                repeat: false
            });
            const nextTs = ts + keyItem.downMs + Math.random() * keyItem.downMSRand;
            let currentTs = ts + keyItem.repeatDelay;
            while (currentTs < nextTs) {
                actions.push({
                    type: 'keydown',
                    ts: currentTs,
                    repeat: true
                });
                currentTs += keyItem.repeatRate;
            }
            actions.push({
                type: 'keyup',
                ts: nextTs,
                repeat: false
            });
            actionItem.next = nextTs + keyItem.upMS + Math.random() * keyItem.upMSRand;
        }
        if (actionItem.actions && actionItem.actions.length && ts > actionItem.actions[0].ts) {
            const currentAction = actionItem.actions.shift();
            const eventInit = {
                key: keyItem.key,
                code: keyItem.code,
                keyCode: keyItem.keyCode,
                repeat: currentAction.repeat
            };
            globalThis.document.dispatchEvent(new KeyboardEvent(currentAction.type, eventInit));
            if (douyin.log && douyin.log.constructor == Function) {
                douyin.log(`${currentAction.type} key:${keyItem.key} code:${keyItem.code}`);
            }
        }
    }

    douyin.gameLoop = (ts) => {
        if (douyin.stoped) {
            return;
        }
        douyin.items.forEach(keyItem => douyin.checkAction(keyItem, ts));
        requestAnimationFrame(douyin.gameLoop);
    }

    //# 间断性点按 ArrowDown
    douyin.keyArrowDown = {
        check: () => !!globalThis.document.querySelector("[data-e2e=slideList]  video[autoplay]"),
        key: "ArrowDown",
        code: "ArrowDown",
        keyCode: 40,
        repeatDelay: 510,
        repeatRate: 35,
        downMs: 200,
        downMSRand: 200,
        upMS: 5100,
        upMSRand: 23000
    };

    // 间断性长按z
    douyin.keyZ = {
        check: () => !!globalThis.document.querySelector("[data-e2e=living-container]  video[autoplay]"),
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

    //控制台
    douyin.items = [douyin.keyArrowDown, douyin.keyZ]

    //执行
    douyin.start();
}();

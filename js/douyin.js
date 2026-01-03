!function () {
    if (globalThis._douyin_) {
        globalThis._douyin_.stoped = false;
        return;
    }
    const douyin = globalThis._douyin_ = {
        stoped: false,
        log: (message) => console.info(message)
    };
    douyin.addScript = (url, useCallback) => {
        var script = globalThis.document.createElement('script');
        script.setAttribute('type', 'text/javascript'), script.setAttribute('src', url), script.onload = useCallback, globalThis.document.getElementsByTagName('head')[0].appendChild(script);
    }
    douyin.checkAction = (keyItem, ts) => {
        if (!keyItem.check()) {
            keyItem._actions = [];
            return;
        }
        const actions = keyItem._actions ??= [];
        if (actions.length == 0) {
            let downTs = ts + keyItem.upMS + Math.random() * keyItem.upMSRand;
            actions.push({
                type: 'keydown',
                ts: downTs,
                repeat: false
            });
            const upTs = downTs + keyItem.downMs + Math.random() * keyItem.downMSRand;
            downTs += keyItem.repeatDelay;
            while (downTs < upTs) {
                actions.push({
                    type: 'keydown',
                    ts: downTs,
                    repeat: true
                });
                downTs += keyItem.repeatRate;
            }
            actions.push({
                type: 'keyup',
                ts: upTs,
                repeat: false
            });
        }
        else if (ts > actions[0].ts) {
            const currentAction = actions.shift();
            const eventInit = {
                key: keyItem.key,
                code: keyItem.code,
                keyCode: keyItem.keyCode,
                repeat: currentAction.repeat,
                bubbles: true
            };
            try {
                globalThis.document.body.dispatchEvent(new KeyboardEvent(currentAction.type, eventInit));
                if (typeof douyin.log == "function") {
                    douyin.log(`${currentAction.type} key:${keyItem.key} code:${keyItem.code}`);
                }
            }
            catch (e) {
                if (typeof douyin.log == "function") {
                    douyin.log(`${currentAction.type} key:${keyItem.key} code:${keyItem.code} 【Error】${e}`);
                }
            }
        }
    }

    douyin.mainLoop = (ts) => {
        if (douyin.stoped) {
            douyin.items.forEach(keyItem => delete keyItem._actions);
            return;
        }
        douyin.items.forEach(keyItem => douyin.checkAction(keyItem, ts));
        requestAnimationFrame(douyin.mainLoop);
    }

    //# 间断性点按 ArrowDown
    douyin.keyArrowDown = {
        check: () => !!globalThis.document.querySelector("#slidelist[data-active=true] video[autoplay]"),// || [data-e2e=feed-active-video]  || [data-e2e=live-slider]
        key: "ArrowDown",
        code: "ArrowDown",
        keyCode: 40,
        repeatDelay: 510,
        repeatRate: 35,
        downMs: 200,
        downMSRand: 200,
        get upMS() {
            const e = globalThis.document.querySelector("#slidelist[data-active=true] video[autoplay]").buffered; //  [data-e2e=live-slider]
            if (e.length == 0) {
                return 500 + Math.random() * 3000;
            }
            else {
                const endTs = e.end(0);
                return Math.min(2000 + Math.random() * 20000, endTs * 500 + Math.random() * endTs * 200);
            }
        },
        upMSRand: 0
    };

    // 间断性长按z
    douyin.keyZ = {
        check: () => !!globalThis.document.querySelector("main video[autoplay]"),
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

    douyin.start = () => {
        douyin.stoped = false;
        requestAnimationFrame(douyin.mainLoop);
    }

    douyin.stop = () => {
        douyin.stoped = true;
    }

    //控制台
    douyin.items = [douyin.keyArrowDown, douyin.keyZ]

    //执行
    douyin.start();
}();

!function () {
    if (globalThis._douyin_) {
        globalThis._douyin_.start();
        return;
    }
    const douyin = globalThis._douyin_ = {
        stoped: false,
        log: (msg) => console.info(`%c %s`, 'color: green; font-size: 2em;', msg),
        get liveRoom() {

            //[data-e2e=feed-active-video]
            return globalThis.document.querySelector("main.live-main video[autoplay]");
        },
        get slideVideo() {
            //[data-e2e=live-slider]
            return globalThis.document.querySelector("#slidelist[data-active=true] video[autoplay]");
        }
    };
    douyin.addScript = (url, useCallback) => {
        const script = globalThis.document.createElement('script');
        script.setAttribute('type', 'text/javascript');
        script.setAttribute('src', url);
        script.onload = useCallback;
        globalThis.document.getElementsByTagName('head')[0].appendChild(script);
    }
    douyin.checkAction = (keyItem, ts) => {
        const ci = keyItem.check;
        if (ci != keyItem._actionCI) {
            keyItem._actionCI = ci;
            if (keyItem._actions?.length && typeof douyin.log == "function") {
                douyin.log(`【取消】${keyItem._actions[0].type} ${keyItem.key}【${keyItem.code}】`);
            }
            keyItem._actions = [];
        }
        if (!keyItem._actionCI) {
            return;
        }
        const actions = keyItem._actions ??= [];
        if (actions.length == 0) {
            let downTs = ts + keyItem.upMS + Math.random() * keyItem.upMSRand;
            const fa = {
                type: 'keydown',
                ts: downTs,
                repeat: false
            };
            actions.push(fa);
            if (typeof douyin.log == "function") {
                douyin.log(`【部署】${fa.type} ${keyItem.key}【${keyItem.code}】 延迟: ${~~(downTs - ts)}`);
            }
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
            actions.push({ type: 'keyup', ts: upTs, repeat: false });
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
                const dv = new KeyboardEvent(currentAction.type, eventInit);
                globalThis.document.body.dispatchEvent(dv);
                if (typeof douyin.log == "function") {
                    douyin.log(`【触发】${currentAction.type} ${keyItem.key}【${keyItem.code}】`);
                }
            }
            catch (e) {
                if (typeof douyin.log == "function") {
                    douyin.log(`【异常】${currentAction.type} ${keyItem.key}【${keyItem.code}】 异常: ${e}`);
                }
            }
        }
    }

    douyin.mainLoop = (ts) => {
        if (douyin.stoped) {
            for (const keyItem of douyin.items) {
                delete keyItem._actions;
            }
            return;
        }
        for (const keyItem of douyin.items) {
            douyin.checkAction(keyItem, ts);
        }
        requestAnimationFrame(douyin.mainLoop);
    }

    //# 间断性点按 ArrowDown
    douyin.keyArrowDown = {
        get check() {
            return douyin.liveRoom ? null : douyin.slideVideo;
        },
        key: "ArrowDown",
        code: "ArrowDown",
        keyCode: 40,
        repeatDelay: 510,
        repeatRate: 35,
        downMs: 200,
        downMSRand: 200,
        get upMS() {
            const v = douyin.slideVideo;
            if (!v) {
                return 100;
            }
            const e = v.duration;
            if (isFinite(e)) { // Infinity is live
                return Math.min(2000 + Math.random() * 20000, e * (Math.random() * 200 + 500));
            }
            else {
                return 2000 + Math.random() * 3000;
            }
        },
        upMSRand: 0
    };

    // 间断性长按z
    douyin.keyZ = {
        get check() {
            return douyin.slideVideo ? null : douyin.liveRoom;
        },
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
    douyin.items = [
        douyin.keyArrowDown,
        douyin.keyZ
    ];

    //执行
    douyin.start();
}();

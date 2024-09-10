!function () {
    const maxWidth = document.body.clientWidth - 20;
    const blockSizeCalc = ~~(maxWidth / 17);
    const blockSize = blockSizeCalc < 10 ? 10 : blockSizeCalc > 40 ? 40 : blockSizeCalc;
    const mainCols = 10;
    const mainRowsCalc = ~~((document.body.clientHeight - 20) / blockSize);
    const mainRows = mainRowsCalc < 15 ? 15 : mainRowsCalc > 25 ? 25 : mainRowsCalc;

    const mainDiv = document.querySelector('#game');
    mainDiv.style.width = `${maxWidth}px`;
    mainDiv.style.height = `${blockSize * mainRows}px`;

    const mainCanvas = document.querySelector('#gameCanvas');
    mainCanvas.width = blockSize * mainCols;
    mainCanvas.height = blockSize * mainRows;
    mainCanvas.style.width = `${mainCanvas.width}px`;
    mainCanvas.style.height = `${mainCanvas.height}px`;
    const mainCtx = mainCanvas.getContext('2d');

    const subCols = 4;
    const subRows = 4;
    const subCanvas = document.querySelector('#nextShapeCanvas');
    subCanvas.width = blockSize * subCols;
    subCanvas.height = blockSize * subRows;
    subCanvas.style.width = `${subCanvas.width}px`;
    subCanvas.style.height = `${subCanvas.height}px`;
    const subCtx = subCanvas.getContext('2d');

    const spanScore = document.querySelector('#score');
    const spanLevel = document.querySelector('#level');
    const spanSpeed = document.querySelector('#speed');

    const speeds = [1000, 900, 800, 700, 600, 500, 450, 400, 350, 300, 250, 200, 150, 100, 50, 25];
    const downActions = new Set();
    let currentActions;
    let cells;
    let freezeCallbacks = new Set(), pause = false, currentInstance = undefined;
    let gameTime = 0;
    let freezeTime = 0;
    let lastTime = 0;
    let initSpeed = 0;
    let loading = false;
    function createStatus(speed) {
        let level = 0;
        let life = subRows;
        const status = {
            score: 0,
            get speed() { return speed; },
            get level() { return level; },
            get life() { return life; },
            over: false,
        };

        status.updateLife = (maxLife, isAdd) => {
            life += isAdd ? 1 : -1;
            if (life > maxLife) {
                life = maxLife;
                return false;
            }
            if (life < 0) {
                life = 0;
                status.over = true;
                return false;
            }
            return true;
        }

        status.updateGrade = (maxLevel) => {
            level += 1;
            if (level >= maxLevel) {
                level = maxLevel;
                return false;
            }
            return true;
        }

        status.updateSpeed = (maxSpeed) => {
            speed += 1;
            if (speed >= maxSpeed) {
                speed = 0;
                return false;
            }
            return true;
        }
        return status;
    }

    const keys = {
        KEY_SELECT: "select",
        KEY_START: "start",
        KEY_EXTEND: "extend",
        KEY_UP: "up",
        KEY_DOWN: "down",
        KEY_LEFT: "left",
        KEY_RIGHT: "right",
        KEY_ACTION: "action"
    };

    //按钮控制

    const inputMap = {
        "#select": keys.KEY_SELECT,
        "#start": keys.KEY_START,
        "#extend": keys.KEY_EXTEND,
        "#up": keys.KEY_UP,
        "#down": keys.KEY_DOWN,
        "#left": keys.KEY_LEFT,
        "#right": keys.KEY_RIGHT,
        "#action": keys.KEY_ACTION
    }

    const createStartEvent = keyvalue => () => updateDownActions(keyvalue, true);
    const createEndEvent = keyvalue => () => updateDownActions(keyvalue, false);

    for (const key in inputMap) {
        const ele = document.querySelector(key);
        ['mousedown', 'touchstart'].forEach(startEvent => ele.addEventListener(startEvent, createStartEvent(inputMap[key])));
        ['mouseup', "mouseleave", 'touchend'].forEach(endEvent => ele.addEventListener(endEvent, createEndEvent(inputMap[key])));
    }

    //键盘控制
    const keymap = {
        ArrowLeft: keys.KEY_LEFT,
        ArrowRight: keys.KEY_RIGHT,
        ArrowDown: keys.KEY_DOWN,
        ArrowUp: keys.KEY_UP,
        ' ': keys.KEY_ACTION,
        Enter: keys.KEY_ACTION,
        z: keys.KEY_SELECT,
        x: keys.KEY_START,
        c: keys.KEY_EXTEND,
        a: keys.KEY_LEFT,
        d: keys.KEY_RIGHT,
        s: keys.KEY_DOWN,
        w: keys.KEY_UP,
        j: keys.KEY_ACTION,
        k: keys.KEY_ACTION,
        u: keys.KEY_SELECT,
        i: keys.KEY_START,
        o: keys.KEY_EXTEND
    }

    const updateKeys = (event, down) => {
        const newAction = keymap[event.key] ?? keymap[event.key.toLowerCase()];
        if (newAction) {
            event.preventDefault();
            updateDownActions(newAction, down);
        }
    }

    document.addEventListener('keydown', event => updateKeys(event, true));
    document.addEventListener('keyup', event => updateKeys(event, false));


    //手柄控制
    // standard 17-4
    // ""       15-10-MGV2170

    //	Button	17		15		
    //	0		A		A
    //	1		B		B
    //	2		X		C
    //	3		Y		X
    //	4		LT		Y
    //	5		RT		Z
    //	6		LB		LT
    //	7		RB		RT
    //	8		Select	LB
    //	9		Start	RB
    //	10		LStick	Select
    //	11		RStick	Start
    //	12		Up		Home
    //	13		Down	LStick
    //	14		Left	RStick
    //	15		Right
    //	16		Menu
    //	axes	4       10
    //	0	l-lr		l-lr
    //	1	l-ud		l-ud
    //	2	r-lr		r-lr
    //	3	r-ud        -1   1:RB
    //	4               -1   1:LB
    //	5
    //	6		        r-ud
    //	7
    //	8
    //	9               8/7 or 16/7 -7/7:lu  -3/7:lr  1/7:ld 5/7:ll

    const axe9map = {
        [-7]: keys.KEY_UP,
        [-3]: keys.KEY_RIGHT,
        [1]: keys.KEY_DOWN,
        [5]: keys.KEY_LEFT,
    };
    const controlMap = {
        "standard":
        {
            buttons:
            {
                0: keys.KEY_ACTION,
                1: keys.KEY_ACTION,
                2: keys.KEY_ACTION,
                3: keys.KEY_ACTION,
                4: keys.KEY_SELECT,
                5: keys.KEY_START,
                6: keys.KEY_SELECT,
                7: keys.KEY_START,
                8: keys.KEY_SELECT,
                9: keys.KEY_START,
                10: keys.KEY_EXTEND,
                11: keys.KEY_EXTEND,
                12: keys.KEY_UP,
                13: keys.KEY_DOWN,
                14: keys.KEY_LEFT,
                15: keys.KEY_RIGHT,
                16: keys.KEY_EXTEND
            },
            axes:
            {
                0: v => v < -0.75 ? keys.KEY_LEFT : v > 0.75 ? keys.KEY_RIGHT : undefined,
                1: v => v < -0.75 ? keys.KEY_UP : v > 0.75 ? keys.KEY_DOWN : undefined,
                2: v => v < -0.75 ? keys.KEY_LEFT : v > 0.75 ? keys.KEY_RIGHT : undefined,
                3: v => v < -0.75 ? keys.KEY_UP : v > 0.75 ? keys.KEY_DOWN : undefined
            }
        },
        "":
        {
            buttons: {
                0: keys.KEY_ACTION,
                1: keys.KEY_ACTION,
                2: keys.KEY_ACTION,
                3: keys.KEY_ACTION,
                4: keys.KEY_ACTION,
                5: keys.KEY_ACTION,
                6: keys.KEY_SELECT,
                7: keys.KEY_START,
                8: keys.KEY_SELECT,
                9: keys.KEY_START,
                10: keys.KEY_SELECT,
                11: keys.KEY_START,
                12: keys.KEY_START,
                13: keys.KEY_EXTEND,
                14: keys.KEY_EXTEND
            },
            axes: {
                0: v => v < -0.75 ? keys.KEY_LEFT : v > 0.75 ? keys.KEY_RIGHT : undefined,
                1: v => v < -0.75 ? keys.KEY_UP : v > 0.75 ? keys.KEY_DOWN : undefined,
                2: v => v < -0.75 ? keys.KEY_LEFT : v > 0.75 ? keys.KEY_RIGHT : undefined,
                3: v => v > 0.75 ? keys.KEY_SELECT : undefined,
                4: v => v > 0.75 ? keys.KEY_START : undefined,
                5: v => v < -0.75 ? keys.KEY_UP : v > 0.75 ? keys.KEY_DOWN : undefined,
                9: v => axe9map[~~(v * 7.001)]
            }
        },
    }

    const updateDownActions = (newAction, add) => add ? downActions.add(newAction) : downActions.delete(newAction);
    function checkGamepads() {
        const actions = new Set();
        const gamepads = navigator.getGamepads ? navigator.getGamepads() : undefined;
        if (!gamepads) {
            return actions;
        }
        for (let gamepad of gamepads) {
            if (!gamepad) {
                continue;
            }
            const mapping = (gamepad.mapping == "standard" || gamepad.axes?.length == 4) ? "standard" : "";
            const gamepadMap = controlMap[mapping];
            if (!gamepadMap) {
                continue;
            }
            const gamepadButtons = gamepadMap.buttons;
            for (let j = 0; j < gamepad.buttons.length; j++) {
                let button = gamepad.buttons[j];
                if (button.value > 0.75) {
                    const newAction = gamepadButtons[j];
                    newAction && actions.add(newAction);
                }
            }
            const gamepadaxes = gamepadMap.axes;
            for (let j = 0; j < gamepad.axes.length; j++) {
                const gamepadAxe = gamepadaxes[j];
                if (!gamepadAxe) {
                    continue;
                }
                const newAction = gamepadAxe(gamepad.axes[j]);
                newAction && actions.add(newAction);
            }
        }
        return actions;
    }

    function createFlashCallback(callback, delay) {
        delay = ~~delay > 0 ? ~~delay : 20;
        let lastEts = 0;
        let scells = [...cells].sort(() => Math.random() - 0.5);
        const flashArray = [
            ...Array.from({ length: mainRows }, (_, i) => () => {
                for (let j = 0; j < mainCols; j++) {
                    mainBoard[mainRows - i - 1][j] = scells[Math.min(Math.min(i, j), Math.min(mainCols - j - 1, mainRows - i - 1)) % scells.length];
                }
            }),
            ...Array.from({ length: mainRows }, (_, i) => () => {
                for (let j = 0; j < mainCols; j++) {
                    mainBoard[i][j] = emptyCell;
                }
            })
        ];
        return newTs => {
            const ets = ~~(newTs / delay);
            while (lastEts < ets && lastEts < flashArray.length) {
                flashArray[lastEts]();
                lastEts++;
            }
            if (ets >= flashArray.length) {
                typeof callback == 'function' && callback();
                return false;
            }
            return true;
        }
    }

    const cellSpace = ~~(blockSize / 10);
    const lineSize = blockSize - cellSpace * 2;
    const perline = lineSize / 8;
    const perline2 = lineSize / 4;
    const perline4 = lineSize / 2;

    const crateColorCellMaker = (color) => ({
        color: color,
        draw: (ctx, cols, rows) => {
            ctx.shadowOffsetX = cellSpace;
            ctx.shadowOffsetY = cellSpace;
            ctx.shadowBlur = cellSpace * 2;
            ctx.shadowColor = color;
            ctx.fillStyle = color;
            ctx.fillRect(cols * blockSize, rows * blockSize, blockSize - cellSpace * 2, blockSize - cellSpace * 2);

            ctx.shadowColor = 'transparent';
            ctx.fillStyle = 'white';
            ctx.fillRect(cols * blockSize + perline, rows * blockSize + perline, blockSize - perline2 - cellSpace * 2, blockSize - perline2 - cellSpace * 2);

            ctx.shadowColor = ctx.fillStyle;
            ctx.fillStyle = color;
            ctx.fillRect(cols * blockSize + perline2, rows * blockSize + perline2, blockSize - perline4 - cellSpace * 2, blockSize - perline4 - cellSpace * 2);
        }
    });

    const defaultEmptyCell = crateColorCellMaker('lightGray');

    let emptyCell = defaultEmptyCell;
    const mainBoard = Array(mainRows).fill(0).map(() => Array(mainCols).fill(emptyCell));
    const subBoard = Array(subRows).fill(0).map(() => Array(subCols).fill(emptyCell));


    let imageCells = async () => {
        const canvas = document.createElement('canvas');
        canvas.width = blockSize;
        canvas.height = blockSize;
        const context = canvas.getContext('2d');
        const result = new Array(10).fill(0).map((_, i) => new Promise(r => {
            const img = new Image(blockSize - cellSpace * 2, blockSize - cellSpace * 2);
            img.src = `./imgs/${i + 1}.png`;
            let shadowColor = undefined;
            const item = {
                color: shadowColor,
                draw: (ctx, cols, rows) => {
                    ctx.shadowOffsetX = cellSpace;
                    ctx.shadowOffsetY = cellSpace;
                    ctx.shadowBlur = cellSpace * 2;
                    ctx.fillStyle = shadowColor;
                    ctx.strokeStyle = shadowColor;
                    ctx.shadowColor = shadowColor;
                    ctx.strokeRect(cols * blockSize, rows * blockSize, blockSize - cellSpace * 2, blockSize - cellSpace * 2);
                    ctx.drawImage(img, cols * blockSize, rows * blockSize, blockSize - cellSpace * 2, blockSize - cellSpace * 2);
                }
            }
            img.onload = () => {
                context.drawImage(img, 0, 0, blockSize - cellSpace * 2, blockSize - cellSpace * 2);
                const imageData = context.getImageData(0, 0, blockSize - cellSpace * 2, blockSize - cellSpace * 2);
                const datas = [0, 0, 0, 0];
                imageData.data.forEach((v, i) => {
                    datas[i % 4] += v;
                });
                const total = imageData.width * imageData.height;
                item.color = shadowColor = `rgba(${~~(datas[0] / total)}, ${~~(datas[1] / total)},${~~(datas[2] / total)},${~~(datas[3] / total)})`;
                r(item);
            }
        }));
        return await Promise.all(result);
    };

    let colorCells = () => ["red", "green", "blue", "purple", "orange"].map(color => crateColorCellMaker(color));

    const cellTypes = {
        image: async () => {
            cells = typeof imageCells == 'function' ? (imageCells = await imageCells()) : imageCells;
            emptyCell = defaultEmptyCell;
        },
        color: () => {
            cells = typeof colorCells == 'function' ? (colorCells = colorCells()) : colorCells;
            emptyCell = defaultEmptyCell;
        }
    }

    let cellType = 'image';
    async function initCells() {
        await (cellTypes[cellType] ?? Object.values(cellTypes)[0])();
        mainBoard.forEach(row => row.fill(emptyCell));
        subBoard.forEach(row => row.fill(emptyCell));
    }

    function addFreezeCallback(callback) {
        if (callback && 'function' == typeof callback) {
            freezeCallbacks.add({
                time: freezeTime,
                callback: callback,
            });
        }
    }

    function addFlashCallback(callback, delay) {
        addFreezeCallback(createFlashCallback(callback, delay));
    }

    function createVoice() {
        if (!globalThis.AudioContext) {
            return {};
        }
        const urlSource = {};
        let context = undefined;
        const sources = new Set();
        const getAudioFromUrl = async url => {
            const resp = await fetch(url);
            if(!resp.ok){
                throw resp;
            }
            const buffer = await resp.arrayBuffer();
            return await context.decodeAudioData(buffer);
        }
        const play = async (url, offset, duration) => {
            context ??= new AudioContext();
            try {
                url = new URL(url, globalThis.location.href).href;
                const audioData = await (urlSource[url] ??= getAudioFromUrl(url));
                const source = context.createBufferSource();
                source.buffer = audioData;
                source.connect(context.destination);
                source.onended = () => sources.delete(source);
                source.start(0, offset, duration);
                sources.add(source);
            }
            catch (e) { }
        }
        const stop = async ()=>{
            for (const source of sources) {
                source.stop();
            }
            sources.clear();
        }
        return {
            get play() { return play; },
            get stop() { return stop; },
            // clear: () => play("./static/music.mp3", 0, 0.7675),
            // fall: () => play("./static/music.mp3", 1.2558, 0.3546),
            // rotate: () => play("./static/music.mp3", 2.2471, 0.0807),
            // move: () => play("./static/music.mp3", 2.9088, 0.1437),
            // gamestart: () => play("./static/music.mp3", 3.7202, 3.6224),
            // gameover: () => play("./static/music.mp3", 8.1276, 1.1437),
        };
    }

    const voice = createVoice();

    const app = {
        get mainBoard() { return mainBoard; },
        get subBoard() { return subBoard; },
        get mainRows() { return mainRows; },
        get mainCols() { return mainCols; },
        get subRows() { return subRows; },
        get subCols() { return subCols; },
        get cells() { return cells; },
        get emptyCell() { return emptyCell; },
        get speeds() { return speeds; },
        get keys() { return keys; },
        get voice() { return voice; },
        get addFreezeCallback() { return addFreezeCallback; },
        get addFlashCallback() { return addFlashCallback; },
    }


    async function createGame() {
        await initCells();
        clearGame();
        const main = await gameList[selectGameList.value]();
        currentInstance = {
            init: false,
            main,
            status: createStatus(initSpeed),
        };        
        //await app.voice.gamestart?.();
        addFlashCallback(undefined, 30);
    }

    const filterKeys = new Set();
    function collectNewAction() {
        const actions = new Set(downActions);
        checkGamepads().forEach(s => actions.add(s));
        return actions;
    }

    function drawItem(ctx, item, cols, rows) {
        if (item.draw && typeof item.draw == 'function') {
            ctx.save();
            item.draw(ctx, cols, rows);
            ctx.restore();
            return;
        }
    }

    function drawMainText(text, offset) {
        const fontSize = ~~(blockSize);
        mainCtx.font = mainCtx.font.replace(/\d+(?=px)/, fontSize);
        const measure = mainCtx.measureText(text);
        const textWidth = measure.width;
        if (textWidth > fontSize * mainCols) {
            textWidth = fontSize * mainCols;
        }
        const x = (fontSize * mainCols - textWidth) / 2;
        const y = ~~(fontSize * mainRows / 2) + offset * fontSize;
        mainCtx.fillStyle = 'white';
        mainCtx.fillRect(x, y - measure.actualBoundingBoxAscent, textWidth, fontSize);
        mainCtx.fillStyle = 'black';
        mainCtx.fillText(text, x, y, textWidth);
    }

    function drawMenu() {
        let gameName = selectGameList.value;
        mainCtx.font = mainCtx.font.replace(/\d+(?=px)/, blockSize);
        const fontSize = ~~(mainCols * blockSize * blockSize / mainCtx.measureText(gameName).width);
        mainCtx.fillStyle = 'white';
        mainCtx.font = mainCtx.font.replace(/\d+(?=px)/, fontSize);
        const measure = mainCtx.measureText(gameName);
        const textWidth = measure.width;
        const x = (mainCols * blockSize - textWidth) / 2;
        const y = (mainRows * blockSize - fontSize) / 2;
        mainCtx.fillRect(x, y, textWidth, fontSize);
        const ty = y + measure.actualBoundingBoxAscent;
        mainCtx.fillStyle = 'black';
        mainCtx.fillText(gameName, x, ty, textWidth);
    }

    function drawBoard() {
        mainCtx.reset();
        //mainCtx.clearRect(0, 0, mainCanvas.width, mainCanvas.height);
        for (let r = 0; r < mainRows; r++) {
            for (let c = 0; c < mainCols; c++) {
                drawItem(mainCtx, mainBoard[r][c], c, r);
            }
        }
        if (loading) {
            drawMainText("==>资源加载中<==", -1);
        }
        else if (!currentInstance) {
            drawMenu();
        }
        else if (currentInstance.status.over) {
            drawMainText("==>游戏结束<==", -1);
        }
        else if (pause) {
            drawMainText("==>游戏暂停<==", -1);
        }

        subCtx.beginPath();
        subCtx.clearRect(0, 0, subCanvas.width, subCanvas.height);
        for (let r = 0; r < subRows; r++) {
            for (let c = 0; c < subCols; c++) {
                drawItem(subCtx, subBoard[r][c], c, r);
            }
        }
        spanScore.innerText = currentInstance ? currentInstance.status.score : 0;
        spanLevel.innerText = currentInstance ? currentInstance.status.level : 0;
        spanSpeed.innerText = currentInstance ? currentInstance.status.speed : initSpeed;
    }

    function clearGame() {
        if (currentInstance) {
            initSpeed = 0;
        }
        downActions.clear();
        currentActions = {};
        freezeCallbacks = new Set();
        currentInstance = undefined;
        pause = false;
    }

    function selectMenu(offset) {
        let newOffset = selectGameList.options.selectedIndex + offset;
        if (newOffset < 0) {
            newOffset = selectGameList.options.length - 1;
        }
        if (newOffset >= selectGameList.options.length) {
            newOffset = 0;
        }
        selectGameList.options.selectedIndex = newOffset;
    }

    function selectSpeed(offset) {
        initSpeed = (initSpeed + speeds.length + offset) % speeds.length;
    }

    const systemKeyMap = {
        [keys.KEY_SELECT]: () => currentInstance ? clearGame() : selectMenu(1),
        [keys.KEY_START]: async () => currentInstance ? (pause = !pause) : await createGame(),
        [keys.KEY_UP]: () => !currentInstance && selectMenu(-1),
        [keys.KEY_LEFT]: () => !currentInstance && selectSpeed(-1),
        [keys.KEY_DOWN]: () => !currentInstance && selectMenu(1),
        [keys.KEY_RIGHT]: () => !currentInstance && selectSpeed(1)
    }

    function getSystemAction(actions) {
        for (let key in systemKeyMap) {
            if (actions.has(key)) {
                if (!filterKeys.has(key)) {
                    filterKeys.add(key);
                    return systemKeyMap[key];
                }
            }
            else {
                filterKeys.delete(key);
            }
        }
        return undefined;
    }

    function updateGame(ts, keys) {
        if (currentInstance.main.keyMap) {
            for (let key in currentActions) {
                if (keys.has(key)) {
                    const keyItem = currentActions[key];
                    if (!keyItem.allow || ts - keyItem.ts < (keyItem.ticks == 0 ? keyItem.fdelay : keyItem.odelay)) {
                        continue;
                    }
                    keyItem.allow = keyItem.callback(ts);
                    keyItem.ts = ts;
                    keyItem.ticks++;
                }
                else {
                    delete currentActions[key];
                }
            }
            for (let key of keys) {
                if (currentActions[key]) {
                    continue;
                }
                const findMap = currentInstance.main.keyMap[key];
                if (!findMap) {
                    continue;
                }
                if (findMap.length == 3) {
                    currentActions[key] = { fdelay: findMap[0], odelay: findMap[1], callback: findMap[2], ts: ts, ticks: 0, allow: findMap[2](ts) };
                }
            }
        }
        currentInstance.main.update(ts);
    }

    function checkSystem(ts, newActions) {
        if (loading) {
            return true;
        }
        const action = getSystemAction(newActions);
        if (typeof action == "function") {
            loading = true;
            drawBoard();
            Promise.resolve(action(gameTime)).finally(() => {
                loading = false;
                drawBoard();
            });
            return true;
        }
        return pause;
    }

    function checkFreeze(ts) {
        if (freezeCallbacks.size <= 0) {
            return false;
        }
        freezeTime += ts - lastTime;
        for (let freezeCallback of freezeCallbacks) {
            if (!freezeCallback.callback(freezeTime - freezeCallback.time)) {
                freezeCallbacks.delete(freezeCallback);
            }
        }
        drawBoard();
        return true;
    }

    function checkCurrentInstance(ts, newActions) {
        if (!currentInstance || currentInstance.status.over) {
            return false;
        }
        gameTime += ts - lastTime;
        if (!currentInstance.init) {
            currentInstance.main.init(gameTime, currentInstance.status);
            currentInstance.init = true;
        }
        updateGame(gameTime, newActions);
        drawBoard();
        // if(currentInstance.status.over){
        //     app.voice.gameover?.();
        // }
        return true;
    }

    function gameLoop(ts) {
        const newActions = collectNewAction();
        checkSystem(ts, newActions) || checkFreeze(ts) || checkCurrentInstance(ts, newActions);
        lastTime = ts;
        requestAnimationFrame(gameLoop);
    }

    const createLoader = (path, option) => async () => (await import(path)).default(app, option);

    const gameList = {
        '标准方块': createLoader('./fk.js', { hasExtend: false, hasHelper: false }),
        '扩展方块': createLoader('./fk.js', { hasExtend: true, hasHelper: false }),
        '方块带辅助': createLoader('./fk.js', { hasExtend: false, hasHelper: true }),
        '扩展方块带辅助': createLoader('./fk.js', { hasExtend: true, hasHelper: true }),
        '贪吃蛇': createLoader('./tcs.js', { loop: false }),
        '疯狂贪吃蛇': createLoader('./tcs.js', { loop: true }),
        '疯狂射击': createLoader('./sx.js', { isAddtion: false }),
        '疯狂垒墙': createLoader('./sx.js', { isAddtion: true }),
        '坦克大战': createLoader('./tk.js', { tankCount: 25, bossLife: 1 }),
        '坦克大战领主': createLoader('./tk.js', { tankCount: 1, bossLife: 5 }),
        '躲避敌人': createLoader('./fly.js', {}),
        '窄道通行': createLoader('./fly2.js', {}),
        '飞行射击': createLoader('./fly3.js', {}),
    }

    const selectGameList = document.querySelector('#gameList');
    for (let v in gameList) {
        selectGameList.options.add(new Option(v, v));
    }
    selectGameList.addEventListener('change', event => {
        clearGame();
        drawBoard();
    });
    drawBoard();
    requestAnimationFrame(gameLoop);
}();
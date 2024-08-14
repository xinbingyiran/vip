import keyboard from './keyboard.js';
import fkgame from './fk.js';
import tcsgame from './tcs.js';
import sxgame from './sx.js';
import tkgame from './tk.js';
import flygame from './fly.js';
import fly2game from './fly2.js';
import fly3game from './fly3.js';

!function () {

    const gameList = {
        '标准方块': fkgame({ hasExtend: false, hasHelper: false }),
        '扩展方块': fkgame({ hasExtend: true, hasHelper: false }),
        '方块带辅助': fkgame({ hasExtend: false, hasHelper: true }),
        '扩展方块带辅助': fkgame({ hasExtend: true, hasHelper: true }),
        '贪吃蛇': tcsgame({ loop: false }),
        '疯狂贪吃蛇': tcsgame({ loop: true }),
        '疯狂射击': sxgame({ isAddtion: false }),
        '疯狂垒墙': sxgame({ isAddtion: true }),
        '坦克大战': tkgame({ tankCount: 25, bossLife: 1 }),
        '坦克大战领主': tkgame({ tankCount: 1, bossLife: 5 }),
        '躲避敌人': flygame({}),
        '窄道通行': fly2game({}),
        '飞行射击': fly3game({}),
    }

    const selectGameList = document.querySelector('#gameList');
    for (let v in gameList) {
        selectGameList.options.add(new Option(v, v));
    }
    selectGameList.addEventListener('change', event => {
        clearGame();
        drawBoard();
    });

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
    let emptyCell;
    const mainBoard = Array(mainRows).fill(undefined).map(() => Array(mainCols).fill(undefined));
    const subBoard = Array(subRows).fill(undefined).map(() => Array(subCols).fill(undefined));
    const downActions = new Set();
    let currentActions;
    let cells;
    let freezeCallbacks = new Set(), pause = false, currentInstance = undefined;
    let gameTime = 0;
    let freezeTime = 0;
    let lastTime = 0;
    let initSpeed = 0;
    function createStatus(speed) {
        const status = {
            score: 0,
            speed: speed,
            level: 0,
            life: subRows,
            over: false,
        };

        status.updateLife = (maxLife, isAdd) => {
            status.life += isAdd ? 1 : -1;
            if (status.life > maxLife) {
                status.life = maxLife;
                return false;
            }
            if (status.life < 0) {
                status.life = 0;
                status.over = true;
                return false;
            }
            return true;
        }

        status.updateGrade = (maxLevel) => {
            status.level += 1;
            if (status.level >= maxLevel) {
                status.level = maxLevel;
                return false;
            }
            return true;
        }

        status.updateSpeed = (maxSpeed) => {
            status.speed += 1;
            if (status.speed >= maxSpeed) {
                status.speed = 0;
                return false;
            }
            return true;
        }
        return status;
    }

    //按钮控制

    const inputMap = {
        "#select": keyboard.KEY_SELECT,
        "#start": keyboard.KEY_START,
        "#extend": keyboard.KEY_EXTEND,
        "#up": keyboard.KEY_UP,
        "#down": keyboard.KEY_DOWN,
        "#left": keyboard.KEY_LEFT,
        "#right": keyboard.KEY_RIGHT,
        "#action": keyboard.KEY_ACTION
    }

    const createStartEvent = keyvalue => {
        return (event) => {
            updateDownActions(keyvalue, true);
        }
    }
    const createEndEvent = keyvalue => {
        return (event) => {
            updateDownActions(keyvalue, false);
        }
    }
    for (const key in inputMap) {
        const ele = document.querySelector(key);
        ['mousedown', 'touchstart'].forEach(startEvent => ele.addEventListener(startEvent, createStartEvent(inputMap[key])));
        ['mouseup', "mouseleave", 'touchend'].forEach(endEvent => ele.addEventListener(endEvent, createEndEvent(inputMap[key])));
    }

    //键盘控制
    const keymap = {
        ArrowLeft: keyboard.KEY_LEFT,
        ArrowRight: keyboard.KEY_RIGHT,
        ArrowDown: keyboard.KEY_DOWN,
        ArrowUp: keyboard.KEY_UP,
        ' ': keyboard.KEY_ACTION,
        Enter: keyboard.KEY_ACTION,
        z: keyboard.KEY_SELECT,
        x: keyboard.KEY_START,
        c: keyboard.KEY_EXTEND,
        a: keyboard.KEY_LEFT,
        d: keyboard.KEY_RIGHT,
        s: keyboard.KEY_DOWN,
        w: keyboard.KEY_UP,
        j: keyboard.KEY_ACTION,
        k: keyboard.KEY_ACTION,
        u: keyboard.KEY_SELECT,
        i: keyboard.KEY_START,
        o: keyboard.KEY_EXTEND
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
        [-7]: keyboard.KEY_UP,
        [-3]: keyboard.KEY_RIGHT,
        [1]: keyboard.KEY_DOWN,
        [5]: keyboard.KEY_LEFT,
    };
    const controlMap = {
        "standard":
        {
            buttons:
            {
                0: keyboard.KEY_ACTION,
                1: keyboard.KEY_ACTION,
                2: keyboard.KEY_ACTION,
                3: keyboard.KEY_ACTION,
                4: keyboard.KEY_SELECT,
                5: keyboard.KEY_START,
                6: keyboard.KEY_SELECT,
                7: keyboard.KEY_START,
                8: keyboard.KEY_SELECT,
                9: keyboard.KEY_START,
                10: keyboard.KEY_EXTEND,
                11: keyboard.KEY_EXTEND,
                12: keyboard.KEY_UP,
                13: keyboard.KEY_DOWN,
                14: keyboard.KEY_LEFT,
                15: keyboard.KEY_RIGHT,
                16: keyboard.KEY_EXTEND
            },
            axes:
            {
                0: v => v < -0.75 ? keyboard.KEY_LEFT : v > 0.75 ? keyboard.KEY_RIGHT : undefined,
                1: v => v < -0.75 ? keyboard.KEY_UP : v > 0.75 ? keyboard.KEY_DOWN : undefined,
                2: v => v < -0.75 ? keyboard.KEY_LEFT : v > 0.75 ? keyboard.KEY_RIGHT : undefined,
                3: v => v < -0.75 ? keyboard.KEY_UP : v > 0.75 ? keyboard.KEY_DOWN : undefined
            }
        },
        "":
        {
            buttons: {
                0: keyboard.KEY_ACTION,
                1: keyboard.KEY_ACTION,
                2: keyboard.KEY_ACTION,
                3: keyboard.KEY_ACTION,
                4: keyboard.KEY_ACTION,
                5: keyboard.KEY_ACTION,
                6: keyboard.KEY_SELECT,
                7: keyboard.KEY_START,
                8: keyboard.KEY_SELECT,
                9: keyboard.KEY_START,
                10: keyboard.KEY_SELECT,
                11: keyboard.KEY_START,
                12: keyboard.KEY_START,
                13: keyboard.KEY_EXTEND,
                14: keyboard.KEY_EXTEND
            },
            axes: {
                0: v => v < -0.75 ? keyboard.KEY_LEFT : v > 0.75 ? keyboard.KEY_RIGHT : undefined,
                1: v => v < -0.75 ? keyboard.KEY_UP : v > 0.75 ? keyboard.KEY_DOWN : undefined,
                2: v => v < -0.75 ? keyboard.KEY_LEFT : v > 0.75 ? keyboard.KEY_RIGHT : undefined,
                3: v => v > 0.75 ? keyboard.KEY_SELECT : undefined,
                4: v => v > 0.75 ? keyboard.KEY_START : undefined,
                5: v => v < -0.75 ? keyboard.KEY_UP : v > 0.75 ? keyboard.KEY_DOWN : undefined,
                9: v => axe9map[~~(v * 7.001)]
            }
        },
    }

    const updateDownActions = (newAction, add) => {
        if (add) {
            downActions.add(newAction);
        }
        else {
            downActions.delete(newAction);
        }
    }

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
        return newTs => {
            const ets = ~~(newTs / delay);
            if (ets > mainRows * 2) {
                typeof callback == "function" && callback();
                return false;
            }
            if (ets > mainRows) {
                const overEts = ets - mainRows;
                for (let i = 0; i < mainRows; i++) {
                    for (let j = 0; j < mainCols; j++) {
                        mainBoard[i][j] = i < overEts ? emptyCell : cells[(mainRows - i - 1) % cells.length];
                    }
                }
            }
            else {
                for (let i = 0; i < ets; i++) {
                    for (let j = 0; j < mainCols; j++) {
                        mainBoard[mainRows - i - 1][j] = cells[i % cells.length];
                    }
                }
            }
            return true;
        }
    }

    const perline = blockSize / 8;
    const perline2 = blockSize / 4;
    const perline4 = blockSize / 2;
    const defaultEmptyCell = {
        draw: (ctx, cols, rows) => {
            ctx.fillStyle = 'lightGray';
            ctx.fillRect(cols * blockSize + 1, rows * blockSize + 1, blockSize - 2, blockSize - 2);

            ctx.fillStyle = 'white';
            ctx.fillRect(cols * blockSize + 1 + perline, rows * blockSize + perline + 1, blockSize - perline2 - 2, blockSize - perline2 - 2);

            ctx.fillStyle = 'lightGray';
            ctx.fillRect(cols * blockSize + perline2 + 1, rows * blockSize + perline2 + 1, blockSize - perline4 - 2, blockSize - perline4 - 2);
        }
    };

    const imageCells = new Array(10).fill(undefined).map((_, i) => {
        const img = new Image(blockSize - 2, blockSize - 2);
        img.src = `./imgs/${i + 1}.png`;
        return {
            draw: (ctx, cols, rows) => {
                ctx.drawImage(img, cols * blockSize + 1, rows * blockSize + 1, blockSize - 2, blockSize - 2);
            }
        };
    });

    const colorCells = ["red", "green", "blue", "purple", "orange"].map(color => ({
        draw: (ctx, cols, rows) => {
            ctx.fillStyle = color;
            ctx.fillRect(cols * blockSize + 1, rows * blockSize + 1, blockSize - 2, blockSize - 2);

            ctx.fillStyle = 'white';
            ctx.fillRect(cols * blockSize + 1 + perline, rows * blockSize + perline + 1, blockSize - perline2 - 2, blockSize - perline2 - 2);

            ctx.fillStyle = color;
            ctx.fillRect(cols * blockSize + perline2 + 1, rows * blockSize + perline2 + 1, blockSize - perline4 - 2, blockSize - perline4 - 2);
        }
    }));

    const cellTypes = {
        image: () => {
            cells = imageCells;
            emptyCell = defaultEmptyCell;
        },
        color: () => {
            cells = colorCells;
            emptyCell = defaultEmptyCell;
        }
    }

    let cellType = 'image';

    function initCells() {
        (cellTypes[cellType] ?? Object.values(cellTypes)[0])();
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

    function createGame() {
        initCells();
        clearGame();
        currentInstance = {
            init: false,
            main: gameList[selectGameList.value],
            status: createStatus(initSpeed),
            mainBoard,
            subBoard,
            mainRows,
            mainCols,
            subRows,
            subCols,
            cells,
            emptyCell,
            speeds,
            initSpeed,
            addFreezeCallback,
            addFlashCallback,
        };
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
            item.draw(ctx, cols, rows);
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
        mainCtx.beginPath();
        mainCtx.clearRect(0, 0, mainCanvas.width, mainCanvas.height);
        for (let r = 0; r < mainRows; r++) {
            for (let c = 0; c < mainCols; c++) {
                drawItem(mainCtx, mainBoard[r][c], c, r);
            }
        }
        if (!currentInstance) {
            drawMenu();
        }
        else if (currentInstance.status.over) {
            drawMainText("游戏结束", -1);
        }
        else if (pause) {
            drawMainText("游戏暂停", -1);
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
        //downActions.clear();
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
        [keyboard.KEY_SELECT]: () => {
            if (currentInstance) {
                clearGame();
            }
            else {
                selectMenu(1);
            }
        },
        [keyboard.KEY_START]: () => {
            if (currentInstance) {
                pause = !pause;
            }
            else {
                createGame();
            }
        },
        [keyboard.KEY_UP]: () => !currentInstance && selectMenu(-1),
        [keyboard.KEY_LEFT]: () => !currentInstance && selectSpeed(-1),
        [keyboard.KEY_DOWN]: () => !currentInstance && selectMenu(1),
        [keyboard.KEY_RIGHT]: () => !currentInstance && selectSpeed(1)
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

    //{ fdelay,odelay,callback,ts,ticks,allow }

    function updateGame(ts, keys) {
        if (!currentInstance || currentInstance.status.over) {
            return false;
        }
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
        return true;
    }

    function gameLoop(ts) {
        const newActions = collectNewAction();
        const action = getSystemAction(newActions);
        if (action) {
            action(gameTime);
            drawBoard();
        }
        else if (pause) {
            //do nothing
        }
        else if (freezeCallbacks.size) {
            freezeTime += ts - lastTime;
            for (let freezeCallback of freezeCallbacks) {
                if (!freezeCallback.callback(freezeTime - freezeCallback.time)) {
                    freezeCallbacks.delete(freezeCallback);
                }
            }
            drawBoard();
        }
        else if (currentInstance && !currentInstance.init) {
            gameTime += ts - lastTime;
            currentInstance.main.init(gameTime, currentInstance);
            currentInstance.init = true;
        }
        else if (currentInstance && !currentInstance.status.over) {
            gameTime += ts - lastTime;
            if (updateGame(gameTime, newActions)) {
                drawBoard();
            }
        }
        lastTime = ts;
        requestAnimationFrame(gameLoop);
    }
    initCells();
    drawBoard();
    requestAnimationFrame(gameLoop);
}();
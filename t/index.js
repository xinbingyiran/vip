
import keyboard from './keyboard.js';
import fkgame from './fk.js';
import tcsgame from './tcs.js';

!function () {

    const gameList = {
        '方块标准': fkgame({ hasExtend: false, hasHelper: false }),
        '方块扩展': fkgame({ hasExtend: true, hasHelper: false }),
        '方块辅助': fkgame({ hasExtend: false, hasHelper: true }),
        '方块扩展辅助': fkgame({ hasExtend: true, hasHelper: true }),
        '贪吃蛇': tcsgame({ loop: false }),
        '贪吃蛇穿墙': tcsgame({ loop: true })
    }

    const selectGameList = document.querySelector('#gameList');
    for (let v in gameList) {
        selectGameList.options.add(new Option(v, v));
    }
    let game = undefined;

    const boardEmptyColor = 'gray';
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

    const dialogScore = document.querySelector('#dscore');
    const dialogLevel = document.querySelector('#dlevel');
    const dialogSpeed = document.querySelector('#dspeed');
    const dialogClose = document.querySelector('#dclose');

    const dialogGameOver = document.querySelector("#gameOverDialog");
    const gameOver = () => {
        if (!dialogGameOver.open) {
            dialogScore.innerText = game ? game.status.score : 0;
            dialogLevel.innerText = game ? game.status.level : 0;
            dialogSpeed.innerText = game ? game.status.speed : 0;
            dialogGameOver.showModal();
        }
    };

    dialogClose.addEventListener("click", () => {
        if (dialogGameOver.open) {
            dialogGameOver.close();
            game = undefined;
        }
    });


    const mainBoard = Array(mainRows).fill(undefined).map(() => Array(mainCols).fill(boardEmptyColor));
    const subBoard = Array(subRows).fill(undefined).map(() => Array(subCols).fill(boardEmptyColor));
    const downActions = new Set();
    let oldActions = new Set();

    //按钮控制

    const inputMap = {
        "#select": keyboard.KEY_SELECT,
        "#start": keyboard.KEY_START,
        "#pause": keyboard.KEY_PAUSE,
        "#extend": keyboard.KEY_EXTEND,
        "#reset": keyboard.KEY_RESET,
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
        c: keyboard.KEY_PAUSE,
        v: keyboard.KEY_RESET,
        b: keyboard.KEY_EXTEND,
        a: keyboard.KEY_LEFT,
        d: keyboard.KEY_RIGHT,
        s: keyboard.KEY_DOWN,
        w: keyboard.KEY_UP,
        j: keyboard.KEY_ACTION,
        k: keyboard.KEY_ACTION,
        y: keyboard.KEY_SELECT,
        u: keyboard.KEY_START,
        i: keyboard.KEY_PAUSE,
        o: keyboard.KEY_RESET,
        p: keyboard.KEY_EXTEND,
    }

    const updateKeys = (event, down) => {
        const newAction = keymap[event.key];
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
                2: keyboard.KEY_PAUSE,
                3: keyboard.KEY_RESET,
                4: keyboard.KEY_SELECT,
                5: keyboard.KEY_START,
                6: keyboard.KEY_PAUSE,
                7: keyboard.KEY_RESET,
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
                3: keyboard.KEY_PAUSE,
                4: keyboard.KEY_RESET,
                5: keyboard.KEY_EXTEND,
                6: keyboard.KEY_SELECT,
                7: keyboard.KEY_START,
                8: keyboard.KEY_PAUSE,
                9: keyboard.KEY_RESET,
                10: keyboard.KEY_SELECT,
                11: keyboard.KEY_START,
                12: keyboard.KEY_RESET,
                13: keyboard.KEY_EXTEND,
                14: keyboard.KEY_EXTEND
            },
            axes: {
                0: v => v < -0.75 ? keyboard.KEY_LEFT : v > 0.75 ? keyboard.KEY_RIGHT : undefined,
                1: v => v < -0.75 ? keyboard.KEY_UP : v > 0.75 ? keyboard.KEY_DOWN : undefined,
                2: v => v < -0.75 ? keyboard.KEY_LEFT : v > 0.75 ? keyboard.KEY_RIGHT : undefined,
                3: v => v > 0.75 ? keyboard.KEY_RESET : undefined,
                4: v => v > 0.75 ? keyboard.KEY_PAUSE : undefined,
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
        for (let i in gamepads) {
            const gamepad = gamepads[i];
            if (!gamepad) {
                continue;
            }
            const mapping = (gamepad.mapping == "standard" || gamepad.axes.length == 4) ? "standard" : "";
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

    function startGame(ts) {
        resetAll();
        game = gameList[selectGameList.value];
        game.init(ts, mainBoard, subBoard);
    }

    function resetAll(ts) {
        game = undefined;
        pause = false;
        for (let r = 0; r < mainRows; r++) {
            for (let c = 0; c < mainCols; c++) {
                mainBoard[r][c] = boardEmptyColor;
            }
        }
        for (let r = 0; r < subRows; r++) {
            for (let c = 0; c < subCols; c++) {
                subBoard[r][c] = boardEmptyColor;
            }
        }
        drawBoard();
    }


    const filterKeys = new Set();
    let pause = false;
    function collectNewAction(ts) {
        const actions = new Set(downActions);
        checkGamepads().forEach(s => actions.add(s));
        if (dialogGameOver.open) {
            if (actions.has(keyboard.KEY_SELECT) || actions.has(keyboard.KEY_START)) {
                game = undefined;
                dialogGameOver.close();
            }
            return actions;
        }
        if (actions.has(keyboard.KEY_SELECT)) {
            if (!filterKeys.has(keyboard.KEY_SELECT)) {
                filterKeys.add(keyboard.KEY_SELECT);
                if (selectGameList.options.selectedIndex < 0 || selectGameList.options.selectedIndex >= selectGameList.options.length - 1) {
                    selectGameList.options.selectedIndex = 0;
                }
                else {
                    selectGameList.options.selectedIndex++;
                }
            }
        }
        else {
            filterKeys.delete(keyboard.KEY_SELECT);
        }
        if (actions.has(keyboard.KEY_START)) {
            if (!filterKeys.has(keyboard.KEY_START)) {
                filterKeys.add(keyboard.KEY_START);
                startGame(ts);
            }
        }
        else {
            filterKeys.delete(keyboard.KEY_START);
        }
        if (actions.has(keyboard.KEY_PAUSE)) {
            if (!filterKeys.has(keyboard.KEY_PAUSE)) {
                filterKeys.add(keyboard.KEY_PAUSE);
                pause = !pause;
            }
        }
        else {
            filterKeys.delete(keyboard.KEY_PAUSE);
        }
        if (actions.has(keyboard.KEY_RESET)) {
            if (!filterKeys.has(keyboard.KEY_RESET)) {
                filterKeys.add(keyboard.KEY_RESET);
                selectGameList.selectedIndex = 0;
                resetAll(ts);
            }
        }
        else {
            filterKeys.delete(keyboard.KEY_RESET);
        }
        return actions;
    }

    function drawBoard() {
        mainCtx.clearRect(0, 0, mainCanvas.width, mainCanvas.height);
        mainCtx.beginPath();
        const perline = blockSize / 8;
        const perline2 = blockSize / 4;
        const perline4 = blockSize / 2;
        for (let r = 0; r < mainRows; r++) {
            for (let c = 0; c < mainCols; c++) {
                mainCtx.fillStyle = mainBoard[r][c];
                mainCtx.fillRect(c * blockSize + 0.5, r * blockSize + 0.5, blockSize - 1, blockSize - 1);

                mainCtx.fillStyle = boardEmptyColor;
                mainCtx.fillRect(c * blockSize + perline, r * blockSize + perline, blockSize - perline2, blockSize - perline2);

                mainCtx.fillStyle = mainBoard[r][c];
                mainCtx.fillRect(c * blockSize + perline2, r * blockSize + perline2, blockSize - perline4, blockSize - perline4);
            }
        }
        subCtx.clearRect(0, 0, subCanvas.width, subCanvas.height);
        subCtx.beginPath();
        for (let r = 0; r < subRows; r++) {
            for (let c = 0; c < subCols; c++) {
                subCtx.fillStyle = subBoard[r][c];
                subCtx.fillRect(c * blockSize + 0.5, r * blockSize + 0.5, blockSize - 1, blockSize - 1);

                subCtx.fillStyle = boardEmptyColor;
                subCtx.fillRect(c * blockSize + perline, r * blockSize + perline, blockSize - perline2, blockSize - perline2);

                subCtx.fillStyle = subBoard[r][c];
                subCtx.fillRect(c * blockSize + perline2, r * blockSize + perline2, blockSize - perline4, blockSize - perline4);
            }
        }
        spanScore.innerText = game ? game.status.score : 0;
        spanLevel.innerText = game ? game.status.level : 0;
        spanSpeed.innerText = game ? game.status.speed : 0;

    }


    function setDifference(left, right) {
        const result = new Set();
        for (let e of left) {
            if (!right.has(e)) {
                result.add(e);
            }
        }
        return result;
    }

    let gameTime = 0;
    let lastts = 0;

    function gameLoop(ts) {
        if (!pause) {
            gameTime += ts - lastts;
        }
        lastts = ts;
        const newActions = collectNewAction(gameTime);
        if (!pause) {
            if (game && !game.status.over) {
                game.update(gameTime, newActions, setDifference(oldActions, newActions), setDifference(newActions, oldActions));
                oldActions = newActions;
                drawBoard();
            }
            if (game && game.status.over) {
                gameOver();
            }
        }
        requestAnimationFrame(gameLoop);
    }
    drawBoard();
    oldActions = collectNewAction();
    gameLoop(performance.now());
}();
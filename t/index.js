
import keyboard from './keyboard.js';
import fkgame from './fk.js';
import tcsgame from './tcs.js';

!function () {

    const gameList = {
        '方块标准': fkgame({ hasExtend: false, hasHelper: false }),
        '方块扩展': fkgame({ hasExtend: true, hasHelper: false }),
        '方块辅助': fkgame({ hasExtend: false, hasHelper: true }),
        '方块扩展辅助': fkgame({ hasExtend: true, hasHelper: true }),
        '贪吃蛇': tcsgame()
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
    const mainRows = ~~((document.body.clientHeight - 20) / blockSize);

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
        "#rotate": keyboard.KEY_ROTATE
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
        ['mouseup', 'touchend'].forEach(endEvent => ele.addEventListener(endEvent, createEndEvent(inputMap[key])));
    }

    //键盘控制
    const keymap = {
        ArrowLeft: keyboard.KEY_LEFT,
        ArrowRight: keyboard.KEY_RIGHT,
        ArrowDown: keyboard.KEY_DOWN,
        ArrowUp: keyboard.KEY_UP,
        ' ': keyboard.KEY_ROTATE,
        Enter: keyboard.KEY_ROTATE,
        z: keyboard.KEY_SELECT,
        x: keyboard.KEY_START,
        c: keyboard.KEY_PAUSE,
        v: keyboard.KEY_RESET,
        b: keyboard.KEY_EXTEND,
        a: keyboard.KEY_LEFT,
        d: keyboard.KEY_RIGHT,
        s: keyboard.KEY_DOWN,
        w: keyboard.KEY_UP,
        j: keyboard.KEY_ROTATE,
        k: keyboard.KEY_ROTATE,
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

    // Button
    // 0	Bottom button in right cluster   A
    // 1	Right button in right cluster    B
    // 2	Left button in right cluster     X
    // 3	Top button in right cluster      Y
    // 4	Top left front button            LT
    // 5	Top right front button           RT
    // 6	Bottom left front button         LB
    // 7	Bottom right front button        RB
    // 8	Left button in center cluster    Select
    // 9	Right button in center cluster   Start
    // 10	Left stick pressed button        LStick
    // 11	Right stick pressed button       RStick
    // 12	Top button in left cluster       Up
    // 13	Bottom button in left cluster    Down
    // 14	Left button in left cluster      Left
    // 15	Right button in left cluster     Right
    // 16	Center button in center cluster  Menu
    // axes	
    // 0	Horizontal axis for left stick (negative left/positive right)
    // 1	Vertical axis for left stick (negative up/positive down)
    // 2	Horizontal axis for right stick (negative left/positive right)
    // 3	Vertical axis for right stick (negative up/positive down)

    const bmap = {
        0: keyboard.KEY_ROTATE,
        1: keyboard.KEY_ROTATE,
        2: keyboard.KEY_ROTATE,
        3: keyboard.KEY_ROTATE,
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
        if (navigator.getGamepads) {
            const gamepads = navigator.getGamepads();
            for (let i = 0; i < gamepads.length; i++) {
                let gamepad = gamepads[i];
                if (!gamepad) {
                    continue;
                }
                for (let j = 0; j < gamepad.buttons.length; j++) {
                    let button = gamepad.buttons[j];
                    if (button.pressed) {
                        actions.add(bmap[j]);
                    }
                }
                for (let j = 0; j < gamepad.axes.length; j++) {
                    if (gamepad.axes[j] < -0.75) {
                        actions.add(j % 2 == 0 ? keyboard.KEY_LEFT : keyboard.KEY_UP);
                    }
                    else if (gamepad.axes[j] > 0.75) {
                        actions.add(j % 2 == 0 ? keyboard.KEY_RIGHT : keyboard.KEY_DOWN);
                    }
                }
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
            if (actions.has(keyboard.KEY_PAUSE)) {
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
        for (let r = 0; r < mainRows; r++) {
            for (let c = 0; c < mainCols; c++) {
                mainCtx.fillStyle = mainBoard[r][c];
                mainCtx.fillRect(c * blockSize, r * blockSize, blockSize, blockSize);
                mainCtx.strokeStyle = '#fff';
                mainCtx.strokeRect(c * blockSize, r * blockSize, blockSize, blockSize);
            }
        }
        subCtx.clearRect(0, 0, subCanvas.width, subCanvas.height);
        subCtx.beginPath();
        for (let r = 0; r < subRows; r++) {
            for (let c = 0; c < subCols; c++) {
                subCtx.fillStyle = subBoard[r][c];
                subCtx.fillRect(c * blockSize, r * blockSize, blockSize, blockSize);
                subCtx.strokeStyle = '#fff';
                subCtx.strokeRect(c * blockSize, r * blockSize, blockSize, blockSize);
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
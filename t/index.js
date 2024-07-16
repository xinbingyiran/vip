
import fkgame from './fk.js';
import tcsgame from './tcs.js';

!function () {

    const boardEmptyColor = 'gray';
    const game = fkgame;

    const maxWidth = document.body.clientWidth - 20;

    let bs = ~~(maxWidth / 15);
    const blockSize = bs < 10 ? 10 : bs > 40 ? 40 : bs;


    const boardCols = 10;
    const boardRows = ~~((document.body.clientHeight - 20) / blockSize);



    const gameDiv = document.querySelector('#game');
    gameDiv.style.width = `${maxWidth}px`;
    gameDiv.style.height = `${blockSize * boardRows}px`;


    const canvas = document.querySelector('#gameCanvas');
    canvas.width = blockSize * boardCols;
    canvas.height = blockSize * boardRows;
    canvas.style.width = `${canvas.width}px`;
    canvas.style.height = `${canvas.height}px`;
    const ctx = canvas.getContext('2d');


    const ncanvas = document.querySelector('#nextShapeCanvas');
    const sboardCols = 4;
    const sboardRows = 4;
    ncanvas.width = blockSize * sboardCols;
    ncanvas.height = blockSize * sboardRows;
    ncanvas.style.width = `${ncanvas.width}px`;
    ncanvas.style.height = `${ncanvas.height}px`;
    const ctxNext = ncanvas.getContext('2d');

    const gameOverDialog = document.querySelector("#gameOverDialog");

    const checkboxExtend = document.querySelector('#extend');
    const checkboxHelper = document.querySelector('#helper');
    const checkboxFreeze = document.querySelector('#freeze');
    const checkboxReverse = document.querySelector('#reverse');
    const btnRestart = document.querySelector('#restart');
    const scoreSpan = document.querySelector('#score');
    const levelSpan = document.querySelector('#level');

    let hasExtend = checkboxExtend.checked;
    let hasHelper = checkboxHelper.checked;
    let isFreeze = checkboxFreeze.checked;
    let isReverse = checkboxReverse.checked;
    checkboxExtend.onchange = () => { hasExtend = checkboxExtend.checked; resetAllShapes(); }
    checkboxHelper.onchange = () => { hasHelper = checkboxHelper.checked; resetAllShapes(); }
    checkboxFreeze.onchange = () => { isFreeze = checkboxFreeze.checked; }
    checkboxReverse.onchange = () => { isReverse = checkboxReverse.checked; }


    gameOverDialog.addEventListener("click", () => {
        gameOverDialog.close();
    });

    let board, sboard, oldActions = [];
    function initBoard() {
        board = Array(boardRows).fill(undefined).map(() => Array(boardCols).fill(boardEmptyColor));
        sboard = Array(sboardRows).fill(undefined).map(() => Array(sboardCols).fill(boardEmptyColor));
    }
    btnRestart.onclick = () => {
        initBoard();
        game.init(performance.now(), board, sboard);
        drawBoard();
    }
    const btnX = document.querySelector('#X');
    const btnY = document.querySelector('#Y');
    const btnA = document.querySelector('#A');
    const btnB = document.querySelector('#B');
    const btnSpace = document.querySelector('#space');
    let startEvent, endEvent;

    if ('ontouchend' in document) {
        startEvent = 'touchstart';
        endEvent = 'touchend';
    }
    else {
        startEvent = 'mousedown';
        endEvent = 'mouseup';
    }
    btnX.addEventListener(startEvent, () => {
        action = 'a';
    })
    btnY.addEventListener(startEvent, () => {
        action = 'w';
    })
    btnA.addEventListener(startEvent, () => {
        action = 's';
    })
    btnB.addEventListener(startEvent, () => {
        action = 'd';
    })
    btnSpace.addEventListener(startEvent, () => {
        action = ' ';
    })
    btnX.addEventListener(endEvent, () => {
        action = undefined;
    })
    btnY.addEventListener(endEvent, () => {
        action = undefined;
    })
    btnA.addEventListener(endEvent, () => {
        action = undefined;
    })
    btnB.addEventListener(endEvent, () => {
        action = undefined;
    })
    btnSpace.addEventListener(endEvent, () => {
        action = undefined;
    })
    const gameOver = () => {
        gameOverDialog.innerText = '游戏结束了，你的最终得分是： ' + game.status.score;
        gameOverDialog.showModal();
    };


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
        0: 's',
        1: 'd',
        2: 'a',
        3: 'w',
        4: ' ',
        5: ' ',
        6: ' ',
        7: ' ',
        8: ' ',
        9: ' ',
        10: ' ',
        11: ' ',
        12: 'w',
        13: 's',
        14: 'a',
        15: 'd',
        16: ' '
    }

    function checkGamepads() {
        const actions = [];
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
                        const newAction = bmap[j];
                        !actions.includes(newAction) && actions.push(newAction);
                    }
                }
                for (let j = 0; j < gamepad.axes.length; j++) {
                    if (gamepad.axes[j] < -0.5) {
                        lastAlex = `${i}-${j}`;
                        const newAction = j % 2 == 0 ? 'a' : 'w';
                        !actions.includes(newAction) && actions.push(newAction);
                    }
                    else if (gamepad.axes[j] > 0.5) {
                        const newAction = j % 2 == 0 ? 'd' : 's';
                        !actions.includes(newAction) && actions.push(newAction);
                    }
                }
            }
        }
        return actions;
    }

    let downActions = [];

    const updateActions = (newAction, add) => {
        if (add) {
            if (!downActions.includes(newAction)) {
                downActions.push(newAction);
            }
        }
        else {
            const index = downActions.indexOf(newAction);
            if (index >= 0) {
                downActions.splice(index, 1);
            }
        }
    }

    const updateKeys = (event, down) => {
        let newAction = undefined;
        if (event.key === 'ArrowLeft' || event.key === 'a') {
            newAction = 'a';
            // 移动方块到左边
        } else if (event.key === 'ArrowRight' || event.key === 'd') {
            newAction = 'd';
            // 移动方块到右边
        } else if (event.key === 'ArrowDown' || event.key === 's') {
            newAction = 's';
            // 加速方块下落
        } else if (event.key === 'ArrowUp' || event.key === 'w') {
            newAction = 'w';
        } else if (event.key === 'Tab' || event.key === ' ') {
            newAction = ' ';
        }
        if (newAction) {
            event.preventDefault();
            updateActions(newAction, down);
        }
    }

    document.addEventListener('keydown', event => updateKeys(event, true));
    document.addEventListener('keyup', event => updateKeys(event, false));

    function checkAction() {
        const newActions = checkGamepads();
        return downActions.concat(newActions);
    }

    function drawBoard() {
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        ctx.beginPath();
        for (let r = 0; r < boardRows; r++) {
            for (let c = 0; c < boardCols; c++) {
                ctx.fillStyle = board[r][c];
                ctx.fillRect(c * blockSize, r * blockSize, blockSize, blockSize);
                ctx.strokeStyle = '#fff';
                ctx.strokeRect(c * blockSize, r * blockSize, blockSize, blockSize);
            }
        }
        ctxNext.clearRect(0, 0, ncanvas.width, ncanvas.height);
        ctxNext.beginPath();
        scoreSpan.innerText = game.status.score;
        levelSpan.innerText = game.status.level;
        for (let r = 0; r < sboardRows; r++) {
            for (let c = 0; c < sboardCols; c++) {
                ctxNext.fillStyle = sboard[r][c];
                ctxNext.fillRect(c * blockSize, r * blockSize, blockSize, blockSize);
                ctxNext.strokeStyle = '#fff';
                ctxNext.strokeRect(c * blockSize, r * blockSize, blockSize, blockSize);
            }
        }

    }



    function gameLoop(ts) {
        const newActions = checkAction();
        if (!game.status.over) {
            game.update(ts, oldActions.filter(s => !newActions.includes(s)), newActions.filter(s => !oldActions.includes(s)));
            oldActions = newActions;
            drawBoard();
        }
        requestAnimationFrame(gameLoop);
    }
    initBoard();
    drawBoard();
    oldActions = checkAction();
    gameLoop(performance.now());
}();
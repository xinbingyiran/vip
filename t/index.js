!function () {
    const canvas = document.querySelector('#gameCanvas');
    canvas.width = 300;
    canvas.height = 600;
    const ctx = canvas.getContext('2d');

    const ncanvas = document.querySelector('#nextShapeCanvas');
    ncanvas.width = 120;
    ncanvas.height = 120;
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

    const blockSize = 30;
    const boardRows = canvas.height / blockSize;
    const boardCols = 300 / blockSize;
    const boardEmptyColor = 'gray';

    gameOverDialog.addEventListener("click", () => {
        gameOverDialog.close();
    });
    gameOverDialog.onclose = () => {
        isGameOver = false;
        reset();
    };
    btnRestart.onclick = () => {
        reset();
    }

    function rotateItem(item, ignoreBoard) {
        const shape = item.shape;
        const rows = shape.length;
        const cols = shape[0].length;
        const rotated = [];
        if (item.cx + rows > boardCols) {
            return false;
        }
        if (item.cy + cols > boardRows) {
            return false;
        }
        for (let c = 0; c < cols; c++) {
            rotated[c] = [];
            for (let r = 0; r < rows; r++) {
                rotated[c][r] = isReverse ? shape[r][cols - 1 - c] : shape[rows - 1 - r][c];
            }
        }
        if (ignoreBoard) {
            item.shape = rotated;
            return;
        }
        const array = calcArray(rotated, item.cx, item.cy);
        if (allEmpty(array)) {
            item.shape = rotated;
            return;
        }
    }

    const gameOver = () => {
        isGameOver = true;
        gameOverDialog.innerText = '游戏结束了，你的最终得分是： ' + allScore;
        gameOverDialog.showModal();
    };

    const calcArray = (shape, x, y) => {
        const result = [];
        shape.forEach((row, r) => {
            row.forEach((value, c) => {
                if (value) {
                    result.push([x + c, y + r]);
                }
            });
        });
        return result;
    }

    const hasEmpty = (array) => {
        return array.some(([x, y]) => x >= 0 && x < boardCols && (y < 0 || (y < boardRows && board[y][x] === boardEmptyColor)));
    };

    const allEmpty = (array) => {
        return array.every(([x, y]) => x >= 0 && x < boardCols && (y < 0 || (y < boardRows && board[y][x] === boardEmptyColor)));
    };

    const fullBoardRow = row => board[row].every((value) => value !== boardEmptyColor);

    const emptyRow = Array.from({ length: boardCols }, () => boardEmptyColor);

    const scores = { 0: 0, 1: 100, 2: 300, 3: 700, 4: 1500 };
    const calcScore = (cy, length) => {
        let score = 0;
        for (let r = cy + length - 1; r >= 0; r--) {
            if (r < cy) {
                if (score == 0) {
                    return 0;
                }
                while (r >= 0) {
                    board[r + score] = [...board[r]];
                    r--;
                }
                break;
            }
            if (fullBoardRow(r)) {
                score++;
            }
            else if (score > 0) {
                board[r + score] = [...board[r]];
            }
        }
        if (score > 0) {
            allScore += scores[score];
            level = ~~(allScore / 10000);
            for (let i = score - 1; i >= 0; i--) {
                board[i] = [...emptyRow];
            }
        }
        return score;
    }

    const mergeItem = item => {
        for (let r = 0; r < item.shape.length; r++) {
            if (item.cy + r < 0) {
                continue;
            }
            for (let c = 0; c < item.shape[r].length; c++) {
                if (item.shape[r][c]) {
                    board[item.cy + r][item.cx + c] = item.color;
                }
            }
        }
        let ll = calcScore(item.cy, item.shape.length);
        if (item.cy < 0) {
            let overLine = item.cy;
            while (overLine < 0 && ll > 0) {
                overLine++; ll--;
                const row = item.shape[-overLine];
                for (var i = 0; i < row.length; i++) {
                    if (row[i]) {
                        board[ll][item.cx + i] = item.color;
                    }
                }
            }
            if (overLine < 0) {
                gameOver();
                return;
            }
        }
    }

    const moveItem = (item, x) => {
        var newx = item.cx + x;
        if (newx < 0 || newx + item.shape[0].length > boardCols) {
            return;
        }
        const array = calcArray(item.shape, newx, item.cy);
        if (allEmpty(array)) {
            item.cx = newx;
        }
    }

    const commonDown = item => {
        const newy = item.cy + 1;
        const array = calcArray(item.shape, item.cx, newy);
        if (allEmpty(array)) {
            item.cy = newy;
            return;
        }
        mergeItem(cshape);
        cshape.finished = true;
    }

    const dotDown = item => {
        const newy = item.cy + 1;
        const array = [];
        for (let r = newy; r < boardRows; r++) {
            array.push([item.cx, r]);
        }
        if (hasEmpty(array)) {
            item.cy = newy;
            return;
        }
        mergeItem(cshape);
        cshape.finished = true;
    }

    const cancelDown = item => {
        const newy = item.cy + 1;
        const array = calcArray(item.shape, item.cx, newy);
        if (allEmpty(array)) {
            item.cy = newy;
            return;
        }
        cshape.finished = true;
    }
    const cancelUp = item => {
        const x = item.cx;
        const y = item.cy + item.shape.length;
        for (let r = y; r < boardRows; r++) {
            if (board[r][x] !== boardEmptyColor) {
                board[r][x] = boardEmptyColor
                return;
            }
        }
    }

    const addtionDown = cancelDown;

    const addtionlUp = item => {
        const x = item.cx;
        const y = item.cy + item.shape.length;
        for (let r = y; r < boardRows; r++) {
            if ((r == boardRows - 1 || board[r + 1][x] !== boardEmptyColor)) {
                board[r][x] = item.color
                calcScore(r, 1);
                return;
            }
        }
    }
    const bomb = item => {
        var sx = item.cx - 1;
        var ex = item.cx + item.shape[0].length + 1;
        var sy = item.cy;
        var ey = item.cy + item.shape.length + 3;
        for (let r = sy; r < ey; r++) {
            if (r < 0 || r >= boardRows) {
                continue;
            }
            for (let c = sx; c < ex; c++) {
                if (c < 0 || c >= boardCols) {
                    continue;
                }
                if (board[r][c] !== boardEmptyColor) {
                    board[r][c] = boardEmptyColor
                }
            }
        }
    }
    const bombDown = item => {
        const newy = item.cy + 1;
        const array = calcArray(item.shape, item.cx, newy);
        if (allEmpty(array)) {
            item.cy = newy;
            return true;
        }
        bomb(item);
        item.finished = true;
    }

    const bombUp = item => {
        bomb(item);
    }

    const upItem = item => {
        item.upAction(item);
    }

    const shapes = [
        [
            [1, 1],
            [1, 1]
        ],
        [
            [1, 0, 0],
            [1, 1, 1],
        ],
        [
            [0, 0, 1],
            [1, 1, 1],
        ],
        [
            [0, 1, 0],
            [1, 1, 1],
        ],
        [
            [0, 1, 1],
            [1, 1, 0]
        ],
        [
            [1, 1, 0],
            [0, 1, 1]
        ],
        [
            [1, 1, 1, 1]
        ]
    ].map(s => ({ shape: s, downAction: commonDown, upAction: rotateItem }));


    const extShapes = [
        [
            [1, 0, 0],
            [1, 1, 1],
            [1, 0, 0],
        ],
        [
            [1, 0, 0],
            [1, 1, 1],
            [0, 0, 1],
        ],
        [
            [0, 1, 0],
            [1, 1, 1],
            [0, 1, 0],
        ],
        [
            [0, 0, 1],
            [1, 1, 1],
            [1, 0, 0],
        ],
        [
            [1, 0, 1],
            [1, 1, 1]
        ]
    ].map(s => ({ shape: s, downAction: commonDown, upAction: rotateItem }));

    const helpShapes = [
        {
            shape: [[1]],
            downAction: dotDown,
            upAction: rotateItem,
            isHelper: true
        },
        {
            shape: [[1], [1]],
            downAction: cancelDown,
            upAction: cancelUp,
            isHelper: true
        },
        {
            shape: [[1], [1], [1]],
            downAction: addtionDown,
            upAction: addtionlUp,
            isHelper: true
        },
        {
            shape: [[1, 0, 0, 1], [0, 1, 1, 0], [0, 1, 1, 0]],
            downAction: bombDown,
            upAction: bombUp,
            isHelper: true
        }
    ];

    //downaction   碰撞, 穿透，消失，爆炸
    //upaction     旋转，添加，消除，爆炸
    //moveation    移动，闪烁

    const colors = ["red", "green", "blue", "yellow", "purple", "orange"];

    let board = [];
    let allScore = 0;
    const reset = () => {
        action = undefined;
        allScore = 0;
        level = 0;
        for (let r = 0; r < boardRows; r++) {
            board[r] = [];
            for (let c = 0; c < boardCols; c++) {
                board[r][c] = boardEmptyColor;
            }
        }
        resetAllShapes();
        cshape = createShape();
        nshape = createShape();
    }

    function drawShape(ts) {
        cshape.shape.forEach((row, r) => {
            row.forEach((value, c) => {
                if (value) {
                    ctx.fillStyle = cshape.isHelper && (~~ts % 300) > 150 ? "transparent" : cshape.color;

                    ctx.fillRect((c + cshape.cx) * blockSize, (r + cshape.cy) * blockSize, blockSize, blockSize);
                    ctx.strokeStyle = '#fff';
                    ctx.strokeRect((c + cshape.cx) * blockSize, (r + cshape.cy) * blockSize, blockSize, blockSize);
                }
            });
        });
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
    }



    let allShapes = [];

    const resetAllShapes = () => {
        allShapes = [
            ...shapes,
            ...(hasExtend ? extShapes : []),
            ...(hasHelper ? helpShapes : [])]
    }

    function createShape() {
        // 创建新的方块
        const pickShape = allShapes[~~(Math.random() * allShapes.length)];
        const color = colors[~~(Math.random() * colors.length)];
        const shape = pickShape.shape.map(row => [...row]);
        const item = Object.assign({}, pickShape, { shape, color });
        item.isHelper || Array.from({ length: ~~(Math.random() * 4) }).map(() => rotateItem(item, true));
        Object.assign(item, { cx: ~~((boardCols - item.shape[0].length) / 2), cy: -item.shape.length })
        return item;
    }

    var gtime = 0;
    const levels = {
        0: 1000,
        1: 900,
        2: 800,
        3: 700,
        4: 600,
        5: 500,
        6: 450,
        7: 400,
        8: 350,
        9: 300,
        10: 250,
        11: 200,
        12: 150,
        13: 100,
        14: 50,
        15: 25,
        16: 10,
        17: 5,
        18: 2,
        19: 1,
        20: 0
    };

    const globalDown = () => {
        cshape.downAction(cshape);
    }

    let lastAction = undefined;
    let lastDelay = undefined;
    let repeatTimes = undefined;
    let freezeAction = undefined;

    let actionMap = {
        a: [100, 0, (ts) => {
            moveItem(cshape, -1);
        }],
        d: [100, 0, (ts) => {
            moveItem(cshape, 1);
        }],
        s: [0, 0, (ts) => {
            globalDown();
        }],
        w: [200, 50, (ts) => {
            upItem(cshape);
        }],
        " ": [100, 100, (ts) => {
            nshape = createShape();
        }]
    }

    function doAction(ts) {
        if (action) {
            if (freezeAction) {
                return;
            }
            const [fdelay, odelay, actionCallback] = actionMap[action] ?? [undefined, undefined, undefined];
            if (!actionCallback) {
                return;
            }
            if (!lastAction) {
                lastAction = action;
                lastDelay = ts;
                repeatTimes = 0;
            }
            else if (lastDelay) {
                if (ts - lastDelay < (repeatTimes == 0 ? fdelay : odelay)) {
                    return;
                }
                lastDelay = ts;
                repeatTimes++;;
            }
            actionCallback();
            if (cshape.finished) {
                lastAction = undefined;
                freezeAction = true;
            }
        }
        else {
            lastAction = undefined;
            freezeAction = undefined;
        }
    }

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
                        return bmap[j];
                    }
                }
                for (let j = 0; j < gamepad.axes.length; j++) {
                    if (gamepad.axes[j] < -0.5) {
                        lastAlex = `${i}-${j}`;
                        return j % 2 == 0 ? 'a' : 'w';
                    }
                    else if (gamepad.axes[j] > 0.5) {
                        return j % 2 == 0 ? 'd' : 's';
                    }
                }
            }
        }
        return undefined;
    }

    let action = undefined;
    let gpAction = undefined;
    document.addEventListener('keydown', (event) => {
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
        if (newAction && action != newAction) {
            action = newAction;
            event.preventDefault();
        }
    });
    document.addEventListener('keyup', (event) => {
        if (action) {
            event.preventDefault();
            action = undefined;
        }
    });

    function checkAction(ts) {
        const newAction = checkGamepads();
        if (gpAction != newAction) {
            action = gpAction = newAction;
        }
        if (isGameOver) {
            if (action = ' ') {
                gameOverDialog.close();
            }
            return;
        }
        doAction(ts);
        if (ts - gtime > (levels[level] ?? 1) || ts < gtime) {
            gtime = ts;
            if (!isFreeze) {
                globalDown();
            }
        }
    }

    const drawInfo = () => {
        ctxNext.clearRect(0, 0, ncanvas.width, ncanvas.height);
        ctxNext.beginPath();
        ctxNext.fillStyle = 'red';
        var sr = nshape.shape.length > 2 ? 0 : 1;
        var sc = nshape.shape[0].length > 2 ? 0 : 1;
        scoreSpan.innerText = allScore;
        levelSpan.innerText = level;
        for (let r = 0; r < 4; r++) {
            for (let c = 0; c < 4; c++) {
                ctxNext.fillStyle = r >= sr && nshape.shape.length > r - sr && c >= sc && nshape.shape[0].length > c - sc && nshape.shape[r - sr][c - sc] ? nshape.color : 'gray';
                ctxNext.fillRect(c * blockSize, r * blockSize, blockSize, blockSize);
                ctxNext.strokeStyle = '#fff';
                ctxNext.strokeRect(c * blockSize, r * blockSize, blockSize, blockSize);
            }
        }

    }
    let cshape = undefined;
    let nshape = undefined;
    let isGameOver = false;
    let level = 0;

    function gameLoop(ts) {
        checkAction(ts);
        if (!isGameOver) {
            if (cshape.finished) {
                cshape = nshape;
                nshape = createShape();
            }
            drawBoard();
            drawShape(ts);
            drawInfo();
        }
        requestAnimationFrame(gameLoop);
    }
    reset();
    gameLoop();
}();
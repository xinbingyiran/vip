!function () {
    const canvas = document.querySelector('#gameCanvas');
    canvas.width = 480;
    canvas.height = 600;
    const ctx = canvas.getContext('2d');

    const blockSize = 30;
    const boardRows = canvas.height / blockSize;
    const boardCols = 300 / blockSize;
    const boardEmptyColor = 'gray';

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
                rotated[c][rows - 1 - r] = shape[r][c];
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
        alert('Game Over ' + allScore);
        reset();
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

    const calcScore = () => {
        let score = 0;
        for (let r = boardRows - 1; r >= 0; r--) {
            let rowFull = true;
            for (let c = 0; c < boardCols; c++) {
                if (board[r][c] === boardEmptyColor) {
                    rowFull = false;
                    break;
                }
            }
            if (rowFull) {
                score++;
            }
            else if (score > 0) {
                board[r + score] = board[r];
            }
        }
        let getScore = { 0: 0, 1: 100, 2: 300, 3: 700, 4: 1500 }[score];

        while (score > 0) {
            board[--score] = Array.from({ length: boardCols }, () => boardEmptyColor);
        }
        allScore += getScore;
    }

    const mergeItem = item => {
        if (item.cy < 0) {
            gameOver();
            return;
        }
        for (let r = 0; r < item.shape.length; r++) {
            for (let c = 0; c < item.shape[r].length; c++) {
                if (item.shape[r][c]) {
                    board[item.cy + r][item.cx + c] = item.color;
                }
            }
        }
        calcScore();
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
        for (let r = item.cy + 1; r < boardRows; r++) {
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
        for (let r = y; r <= boardRows; r++) {
            if ((r == boardRows || board[r][x] !== boardEmptyColor) && r > y) {
                board[r - 1][x] = item.color
                calcScore();
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
        item.finished = true;
    }
    const bombDown = item => {
        const newy = item.cy + 1;
        const array = calcArray(item.shape, item.cx, newy);
        if (allEmpty(array)) {
            item.cy = newy;
            return true;
        }
        bomb(item);
        return false;
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
        for (let r = 0; r < boardRows; r++) {
            board[r] = [];
            for (let c = 0; c < boardCols; c++) {
                board[r][c] = boardEmptyColor;
            }
        }
        cshape = createShape();
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
        ctx.clearRect(0,0,canvas.width,canvas.height);
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

    let action = undefined;
    document.addEventListener('keydown', (event) => {
        if (event.key === 'ArrowLeft' || event.key === 'a') {
            event.preventDefault();
            action = 'a';
            // 移动方块到左边
        } else if (event.key === 'ArrowRight' || event.key === 'd') {
            event.preventDefault();
            action = 'd';
            // 移动方块到右边
        } else if (event.key === 'ArrowDown' || event.key === 's') {
            event.preventDefault();
            action = 's';
            // 加速方块下落
        } else if (event.key === 'ArrowUp' || event.key === 'w') {
            event.preventDefault();
            action = 'w';
        } else if (event.key === 'Tab' || event.key === ' ') {
            event.preventDefault();
            action = ' ';
        }
    });
    document.addEventListener('keyup', (event) => {
        if (action) {
            event.preventDefault();
            action = undefined;
        }
    });

    const allShapes = [
        ...shapes,
        //...extShapes, 
        ...helpShapes];

    function createShape() {
        // 创建新的方块
        const pickShape = allShapes[~~(Math.random() * allShapes.length)];
        const color = colors[~~(Math.random() * colors.length)];
        const shape = pickShape.shape.map(row => row.map(s => s));
        const item = Object.assign({}, pickShape, { shape, color, cx: ~~((boardCols - shape[0].length) / 2), cy: -shape.length });
        item.isHelper || Array.from({ length: ~~(Math.random() * 4) }).map(() => rotateItem(item, true));
        return item;
    }

    var gtime = 0;

    const globalDown = () => {
        cshape.downAction(cshape);
    }

    function calc(ts) {
        if (action) {
            switch (action) {
                case 'a':
                    moveItem(cshape, -1);
                    break;
                case 'd':
                    moveItem(cshape, 1);
                    break;
                case 's':
                    globalDown();
                    break;
                case 'w':
                    upItem(cshape);
                    break;
                case " ":
                    nshape = createShape();
                    break;
            }
            action = undefined;
            return;
        }
        if (ts - gtime > 1000 || ts < gtime) {
            gtime = ts;
            globalDown();
        }
    }

    const drawInfo = () => {        
        ctx.fillStyle = 'red';
        ctx.font = '20px Arial';
        ctx.fillText('分数:' + allScore, 320, 20);
        var sr = nshape.shape.length > 2 ? 0 : 1;
        var sc = nshape.shape[0].length > 2 ? 0 : 1;
        for (let r = 0; r < 4; r++) {
            for (let c = 0; c < 4; c++) {
                ctx.fillStyle = r >= sr && nshape.shape.length > r - sr && c >= sc && nshape.shape[0].length > c - sc && nshape.shape[r - sr][c - sc] ? nshape.color : 'gray';
                ctx.fillRect(320 + c * blockSize, 40 + r * blockSize, blockSize, blockSize);
                ctx.strokeStyle = '#fff';
                ctx.strokeRect(320 + c * blockSize, 40 + r * blockSize, blockSize, blockSize);
            }
        }

    }
    let cshape = undefined;
    let nshape = createShape();

    function gameLoop(ts) {
        calc(ts);
        if (cshape.finished) {
            cshape = nshape;
            nshape = createShape();
        }
        // 更新游戏状态 
        drawBoard();
        drawShape(ts);
        drawInfo();
        // 绘制游戏界面
        requestAnimationFrame(gameLoop);
    }
    reset();
    gameLoop();
}();
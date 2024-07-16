const status = {
    score: 0,
    level: 0,
    over: true,
};

const options = { hasExtend: false, hasHelper: false, isFreeze: false, isReverse: false }
const boardEmptyColor = 'gray';
const scores = { 0: 0, 1: 100, 2: 300, 3: 700, 4: 1500 };
const levels = { 0: 1000, 1: 900, 2: 800, 3: 700, 4: 600, 5: 500, 6: 450, 7: 400, 8: 350, 9: 300, 10: 250, 11: 200, 12: 150, 13: 100, 14: 50, 15: 25, 16: 10, 17: 5, 18: 2, 19: 1, 20: 0 };
const colors = ["red", "green", "blue", "yellow", "purple", "orange"];

let cshape, nshape, allShapes, baseBoard, mBoard, sBoard, boardRows, boardCols, action;

function rotateItem(item, outofBoard) {
    const shape = item.shape;
    const rows = shape.length;
    const cols = shape[0].length;
    const fix = ~~((rows - cols) / 2);
    const ry = item.cy + fix;
    let rx = item.cx - fix;
    // if (item.cx - fix + rows > boardCols) {
    //     return false;
    // }
    if (ry + cols > boardRows) {
        return false;
    }
    const rotated = [];
    for (let c = 0; c < cols; c++) {
        rotated[c] = [];
        for (let r = 0; r < rows; r++) {
            rotated[c][r] = options.isReverse ? shape[r][cols - 1 - c] : shape[rows - 1 - r][c];
        }
    }
    if (outofBoard) {
        item.shape = rotated;
        return;
    }
    const addtion = rows > cols ? ((~~((rows + 1 - cols) / 2)) * 2) : (cols - rows);
    for (let i = 0; i <= addtion; i++) {
        rx += i * ((i % 2 == 0) ? -1 : 1);
        const array = calcArray(rotated, rx, ry);
        if (allEmpty(array)) {
            item.shape = rotated;
            item.cx = rx;
            item.cy = ry;
            return;
        }
    }
}


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
    return array.some(([x, y]) => x >= 0 && x < boardCols && (y < 0 || (y < boardRows && baseBoard[y][x] === boardEmptyColor)));
};

const allEmpty = (array) => {
    return array.every(([x, y]) => x >= 0 && x < boardCols && (y < 0 || (y < boardRows && baseBoard[y][x] === boardEmptyColor)));
};
const fullBoardRow = row => baseBoard[row].every((value) => value !== boardEmptyColor);
const calcScore = (cy, length) => {
    let score = 0;
    for (let r = cy + length - 1; r >= 0; r--) {
        if (r < cy) {
            if (score == 0) {
                return 0;
            }
            while (r >= 0) {
                for (let c = 0; c < boardCols; c++) {
                    sBoard[r + score][c] = sBoard[r][c];
                }
                r--;
            }
            break;
        }
        if (fullBoardRow(r)) {
            score++;
        }
        else if (score > 0) {
            for (let c = 0; c < boardCols; c++) {
                sBoard[r + score][c] = sBoard[r][c];
            }
        }
    }
    if (score > 0) {
        status.score += scores[score];
        status.level = ~~(status.score / 10000);
        for (let i = score - 1; i >= 0; i--) {
            for (let c = 0; c < boardCols; c++) {
                sBoard[i][c] = boardEmptyColor;
            }
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
                baseBoard[item.cy + r][item.cx + c] = item.color;
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
                    baseBoard[ll][item.cx + i] = item.color;
                }
            }
        }
        if (overLine < 0) {
            status.over = true;
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

const shapes = [
    [[1, 1], [1, 1]],
    [[1, 0, 0], [1, 1, 1],],
    [[0, 0, 1], [1, 1, 1],],
    [[0, 1, 0], [1, 1, 1],],
    [[0, 1, 1], [1, 1, 0]],
    [[1, 1, 0], [0, 1, 1]],
    [[1, 1, 1, 1]]
].map(s => ({ shape: s, downAction: commonDown, upAction: rotateItem }));


const extShapes = [
    [[1, 0, 0], [1, 1, 1], [1, 0, 0],],
    [[1, 0, 0], [1, 1, 1], [0, 0, 1],],
    [[0, 1, 0], [1, 1, 1], [0, 1, 0],],
    [[0, 0, 1], [1, 1, 1], [1, 0, 0],],
    [[1, 0, 1], [1, 1, 1]]
].map(s => ({ shape: s, downAction: commonDown, upAction: rotateItem }));

const helpShapes = [
    { shape: [[1]], downAction: dotDown, upAction: rotateItem, isHelper: true },
    { shape: [[1], [1]], downAction: cancelDown, upAction: cancelUp, isHelper: true },
    { shape: [[1], [1], [1]], downAction: addtionDown, upAction: addtionlUp, isHelper: true },
    { shape: [[1, 0, 0, 1], [0, 1, 1, 0], [0, 1, 1, 0]], downAction: bombDown, upAction: bomb, isHelper: true }
];


function updateBoard() {
    for (let r = 0; r < boardRows; r++) {
        for (let c = 0; c < boardCols; c++) {
            mBoard[r][c] = baseBoard[r][c];
        }
    }
    cshape.shape.forEach((row, r) => {
        row.forEach((value, c) => {
            if (value && r + cshape.cy >= 0 && cshape.cx + c >= 0) {
                mBoard[r + cshape.cy][cshape.cx + c] = cshape.isHelper && (~~ts % 300) > 150 ? "transparent" : cshape.color;
            }
        });
    });
    const srow = nshape.shape.length;
    const scol = nshape.shape[0].length;
    const brow = sBoard.length;
    const bcol = sBoard[0].length;
    const sr = ~~((brow - srow) / 2);
    const sc = ~~((bcol - scol) / 2);
    for (let r = 0; r < brow; r++) {
        for (let c = 0; c < bcol; c++) {
            sBoard[r][c] = r < sr || r - sr >= srow || c < sc || c - sc >= scol || nshape.shape[r - sr][c - sc] == 0 ? boardEmptyColor : nshape.color;
        }
    }
}

let oldOptions;
function createShape() {
    if (!oldOptions || oldOptions.hasExtend !== options.hasExtend || oldOptions.hasHelper !== options.hasHelper) {
        allShapes = [
            ...shapes,
            ...(options.hasExtend ? extShapes : []),
            ...(options.hasHelper ? helpShapes : [])]
        oldOptions = { ...options };
    }
    // 创建新的方块
    const pickShape = allShapes[~~(Math.random() * allShapes.length)];
    const color = colors[~~(Math.random() * colors.length)];
    const shape = pickShape.shape.map(row => [...row]);
    const item = Object.assign({}, pickShape, { shape, color });
    item.isHelper || Array.from({ length: ~~(Math.random() * 4) }).map(() => rotateItem(item, true));
    Object.assign(item, { cx: ~~((boardCols - item.shape[0].length) / 2), cy: -item.shape.length })
    return item;
}

const globalDown = () => {
    cshape.downAction(cshape);
}

let lastAction = undefined;
let lastDelay = undefined;
let repeatTimes = undefined;

let actionMap = {
    a: [100, 0, () => {
        moveItem(cshape, -1);
    }],
    d: [100, 0, () => {
        moveItem(cshape, 1);
    }],
    s: [100, 0, () => {
        globalDown();
    }],
    w: [200, 50, () => {
        cshape.upAction(cshape);
    }],
    " ": [100, 100, () => {
        updateNextShape();
    }]
}

function checkKeys(ts, addKeys, removeKeys) {
    if (removeKeys && removeKeys.some(s => s == action)) {
        action = undefined;
    }
    if (addKeys && addKeys.length) {
        action = addKeys[0];
    }
    if (action) {
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
        }
    }
    else {
        lastAction = undefined;
    }
}


const init = (ts, mainBoard, subBoard) => {
    mBoard = mainBoard;
    baseBoard = mainBoard.map(s => [...s]);
    sBoard = subBoard;
    boardRows = baseBoard.length;
    boardCols = baseBoard[0].length;
    action = undefined;
    for (let r = 0; r < boardRows; r++) {
        for (let c = 0; c < boardCols; c++) {
            mBoard[r][c] = baseBoard[r][c] = boardEmptyColor;
        }
    }
    Object.assign(status, { score: 0, level: 0, over: false });
    cshape = createShape();
    nshape = createShape();
    updateBoard();
}

let gtime = 0;
const update = (ts, addKeys, removeKeys) => {
    if (!status.over) {
        checkKeys(ts, addKeys, removeKeys);
    }
    if (!status.over) {
        if (ts - gtime > (levels[status.level] ?? 1) || ts < gtime) {
            gtime = ts;
            if (!options.isFreeze) {
                globalDown();
            }
        }
    }
    if (!status.over) {
        if (cshape.finished) {
            cshape = nshape;
            nshape = createShape();
        }
    }
    updateBoard();
}


export default { status, options, init, update }
import keys from './keyboard.js';

function game(options) {
    const status = {
        score: 0,
        speed: 0,
        level: 0,
        over: true,
    };

    const maxLevel = 10000;

    const dhItems = {
        timeActions: undefined,
        postAction: undefined
    };

    options = Object.assign({ hasExtend: false, hasHelper: false, isFreeze: false, isReverse: false }, options ?? {});
    const boardEmptyColor = 'gray';
    const scores = { 0: 0, 1: 100, 2: 300, 3: 700, 4: 1500 };
    const speeds = { 0: 1000, 1: 900, 2: 800, 3: 700, 4: 600, 5: 500, 6: 450, 7: 400, 8: 350, 9: 300, 10: 250, 11: 200, 12: 150, 13: 100, 14: 50, 15: 25, 16: 10 };
    const colors = ["red", "green", "blue", "purple", "orange"];

    let cshape, nshape, allShapes, baseBoard, mBoard, sBoard, boardRows, emptyRows, boardCols, emptyCols, currentAction;

    function rotateItem(ts, item, outofBoard) {
        const shape = item.shape;
        const rows = shape.length;
        const cols = shape[0].length;
        const fix = ~~((rows - cols) / 2);
        const ry = item.cy + fix;
        let rx = item.cx - fix;
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
            if (isAllAllowed(array)) {
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

    const isAllAllowed = (array) => {
        return array.every(([x, y]) => x >= 0 && x < boardCols && (y < 0 || (y < boardRows && baseBoard[y][x] === boardEmptyColor)));
    };

    function createPostAction(lines, overLines) {
        return (ts) => {
            lines.forEach(line => baseBoard.splice(line, 1))
            overLines.forEach(line => baseBoard.unshift(line));
            for (let r = 0; r < lines.length - overLines.length; r++) {
                baseBoard.unshift([...emptyCols]);
            }
            status.score += scores[lines.length];
            const result = ~~(status.score / 10000);
            const speedLength = Object.keys(speeds).length;
            status.speed = result % speedLength;
            status.level = ~~(result / speedLength);
            if (status.level > maxLevel) {
                status.over = true;
            }
        }
    }

    const calcScore = (ts, fromLine, toLine, overLines) => {
        const lines = [];
        for (let r = toLine; r >= fromLine; r--) {
            if (baseBoard[r].every((value) => value !== boardEmptyColor)) {
                (lines ??= []).push(r);
            }
        }
        if (overLines.length > lines) {
            status.over = true;
            return 0;
        }
        if (lines.length > 0) {
            dhItems.postAction = createPostAction(lines, overLines);
            dhItems.timeActions = [
                ...emptyCols.map((_, i) => ({
                    time: ts + 20 + i * 20,
                    mapping: lines.map(r => [i, r, boardEmptyColor])
                }))
            ]
        }
        return lines.length;
    }

    const mergeItem = (ts, item) => {
        let count = 0 - item.cy;
        const overLines = [];
        while (count-- > 0) {
            overLines.push([...emptyCols]);
        }
        for (let r = 0; r < item.shape.length; r++) {
            if (item.cy + r < 0) {
                for (let c = 0; c < item.shape[r].length; c++) {
                    if (item.shape[r][c]) {
                        overLines[r][item.cx + c] = item.color;
                    }
                }
            }
            else {
                for (let c = 0; c < item.shape[r].length; c++) {
                    if (item.shape[r][c]) {
                        baseBoard[item.cy + r][item.cx + c] = item.color;
                    }
                }
            }
        }
        const fromLine = item.cy > 0 ? item.cy : 0;
        const toLine = item.cy + item.shape.length - 1;
        calcScore(ts, fromLine, toLine, overLines);
        cshape.finished = true;
    }

    const moveItem = (ts, item, x) => {
        var newx = item.cx + x;
        if (newx < 0 || newx + item.shape[0].length > boardCols) {
            return;
        }
        const array = calcArray(item.shape, newx, item.cy);
        if (isAllAllowed(array)) {
            item.cx = newx;
        }
    }

    const commonDown = (ts, item) => {
        const newy = item.cy + 1;
        const array = calcArray(item.shape, item.cx, newy);
        if (isAllAllowed(array)) {
            item.cy = newy;
            return;
        }
        mergeItem(ts, cshape);
    }
    const dotDown = (ts, item) => {
        if (item.cy > 0 && baseBoard[item.cy][item.cx] == boardEmptyColor && (item.cy == boardRows - 1 || baseBoard[item.cy + 1][item.cx] != boardEmptyColor)) {
            baseBoard[item.cy][item.cx] = item.color;
            if (calcScore(ts, item.cy, item.cy, []) || item.cy == boardRows - 1) {
                item.finished = true;
                return;
            }
            baseBoard[item.cy][item.cx] = boardEmptyColor;
        }
        const array = [];
        const newy = item.cy + 1;
        for (let r = newy; r < boardRows; r++) {
            array.push([item.cx, r]);
        }
        if (array.some(([x, y]) => x >= 0 && x < boardCols && (y < 0 || (y < boardRows && baseBoard[y][x] === boardEmptyColor)))) {
            item.cy = newy;
            return;
        }
        baseBoard[item.cy][item.cx] = item.color;
        item.finished = true;
    }

    const cancelDown = (ts, item) => {
        const newy = item.cy + 1;
        const array = calcArray(item.shape, item.cx, newy);
        if (isAllAllowed(array)) {
            item.cy = newy;
            return;
        }
        cshape.finished = true;
    }
    const cancelUp = (ts, item) => {
        const x = item.cx;
        const y = item.cy + item.shape.length;
        for (let r = y; r < boardRows; r++) {
            if (baseBoard[r][x] !== boardEmptyColor) {
                baseBoard[r][x] = boardEmptyColor
                return;
            }
        }
    }

    const addtionDown = cancelDown;

    const addtionlUp = (ts, item) => {
        const x = item.cx;
        const y = item.cy + item.shape.length >= 0 ? item.cy + item.shape.length : 0;
        for (let r = y; r < boardRows; r++) {
            if ((r == boardRows - 1 || baseBoard[r + 1][x] !== boardEmptyColor)) {
                baseBoard[r][x] = item.color
                calcScore(ts, r, r, []);
                return;
            }
        }
    }

    const bomb = (ts, item) => {
        var sx = item.cx - 1;
        var ex = item.cx + item.shape[0].length + 1;
        var sy = item.cy;
        var ey = item.cy + item.shape.length + 3;
        if(ey > boardRows){
            ey = boardRows;
            if(ey - sy < 4){
                sy = ey - 4;
            }
        }

        const bombItems = [];
        for (let r = sy; r < ey; r++) {
            if (r < 0 || r >= boardRows) {
                continue;
            }
            for (let c = sx; c < ex; c++) {
                if (c < 0 || c >= boardCols) {
                    continue;
                }
                if (baseBoard[r][c] !== boardEmptyColor) {
                    bombItems.push([c, r]);
                }
            }
        }
        bombItems.forEach(([c, r]) => baseBoard[r][c] = boardEmptyColor);

        const bombMaping = [];
        const bombMaping2 = [];
        const bombY = ~~((sy + ey) / 2) - 2;
        for (let rows = 0; rows < 4; rows++) {
            if (bombY + rows < 0 || bombY + rows >= boardRows) {
                continue;
            }
            for (var cols = 0; cols < 4; cols++) {
                let match = (cols == rows) || (cols + rows == 3);
                bombMaping.push([item.cx + cols, bombY + rows, (match ? item.color : boardEmptyColor)]);
                bombMaping2.push([item.cx + cols, bombY + rows, (!match ? item.color : boardEmptyColor)]);
            }
        }

        dhItems.timeActions = [
            {
                time: ts,
                mapping: bombMaping2
            },
            {
                time: ts + 100,
                mapping: bombMaping
            },
            {
                time: ts + 200,
                mapping: bombMaping2
            },
            {
                time: ts + 300,
                mapping: bombMaping
            },
            {
                time: ts + 400,
                mapping: bombMaping2
            }
        ]

    }
    const bombUp = (ts, item) => {
        bomb(ts, item);
        item.finished = true;
    }
    const bombDown = (ts, item) => {
        const newy = item.cy + 1;
        const array = calcArray(item.shape, item.cx, newy);
        if (isAllAllowed(array)) {
            item.cy = newy;
            return true;
        }
        bomb(ts, item);
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
        { shape: [[1, 0, 0, 1], [0, 1, 1, 0], [0, 1, 1, 0]], downAction: bombDown, upAction: bombUp, isHelper: true }
    ];

    function updateBoard(ts) {
        for (let r = 0; r < boardRows; r++) {
            for (let c = 0; c < boardCols; c++) {
                mBoard[r][c] = baseBoard[r][c];
            }
        }
        cshape && !cshape.finished && cshape.shape.forEach((row, r) => {
            row.forEach((value, c) => {
                if (value && r + cshape.cy >= 0 && cshape.cx + c >= 0) {
                    mBoard[r + cshape.cy][cshape.cx + c] = cshape.isHelper && (~~ts % 300) > 150 ? boardEmptyColor : cshape.color;
                }
            });
        });
        if (dhItems.timeActions) {
            let actionIndex = 0;
            for (actionIndex = 0; actionIndex < dhItems.timeActions.length; actionIndex++) {
                const timeAction = dhItems.timeActions[actionIndex];
                if (timeAction.time > ts) {
                    break;
                }
                timeAction.mapping.forEach(([x, y, v]) => {
                    mBoard[y][x] = v;
                });
            }
            if (actionIndex >= dhItems.timeActions.length) {
                if (dhItems.postAction) {
                    dhItems.postAction(ts);
                    dhItems.postAction = undefined;
                }
                dhItems.timeActions = undefined;
            }
            gtime = ts;
        }
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
    allShapes = [
        ...shapes,
        ...(options.hasExtend ? extShapes : []),
        ...(options.hasHelper ? helpShapes : [])]

    function createShape(ts) {
        const pickShape = allShapes[~~(Math.random() * allShapes.length)];
        const color = colors[~~(Math.random() * colors.length)];
        const shape = pickShape.shape.map(row => [...row]);
        const item = Object.assign({}, pickShape, { shape, color });
        item.isHelper || Array.from({ length: ~~(Math.random() * 4) }).map(() => rotateItem(ts, item, true));
        Object.assign(item, { cx: ~~((boardCols - item.shape[0].length) / 2), cy: -item.shape.length })
        return item;
    }

    const globalDown = (ts) => {
        cshape.downAction(ts, cshape);
        gtime = ts;
    }

    let lastAction = undefined;
    let lastDelay = undefined;
    let repeatTimes = undefined;
    let freezeAction = undefined;

    const actionMap = {
        [keys.KEY_LEFT]: [100, 0, (ts) => moveItem(ts, cshape, -1)],
        [keys.KEY_RIGHT]: [100, 0, (ts) => moveItem(ts, cshape, 1)],
        [keys.KEY_DOWN]: [100, 0, (ts) => globalDown(ts)],
        [keys.KEY_UP]: [200, 50, (ts) => cshape.upAction(ts, cshape)],
        [keys.KEY_ACTION]: [200, 50, (ts) => cshape.upAction(ts, cshape)],
        [keys.KEY_EXTEND]: [100, 100, (ts) => nshape = createShape(ts)]
    };

    function checkKeys(ts, keys, addKeys, removeKeys) {
        if (currentAction && (!keys.size || !keys.has(currentAction))) {
            freezeAction = currentAction = undefined;
        }
        if (!currentAction && keys.size) {
            Object.keys(actionMap).some(key => {
                keys.has(key) && (currentAction = key)
            });
        }
        if (currentAction && currentAction != freezeAction) {
            const [fdelay, odelay, actionCallback] = actionMap[currentAction] ?? [undefined, undefined, undefined];
            if (!actionCallback) {
                return;
            }
            if (!lastAction) {
                lastAction = currentAction;
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
            actionCallback(ts);
            if (cshape.finished) {
                freezeAction = currentAction;
                lastAction = undefined;
            }
        }
        else {
            lastAction = undefined;
        }
    }


    const init = (ts, mainBoard, subBoard) => {
        dhItems.timeActions = undefined;
        dhItems.postAction = undefined;
        gtime = ts;
        mBoard = mainBoard;
        baseBoard = mainBoard.map(s => [...s]);
        sBoard = subBoard;
        boardRows = baseBoard.length;
        emptyRows = Array(boardRows).fill(undefined);
        boardCols = baseBoard[0].length;
        emptyCols = Array(boardCols).fill(boardEmptyColor);
        currentAction = undefined;
        for (let r = 0; r < boardRows; r++) {
            for (let c = 0; c < boardCols; c++) {
                baseBoard[r][c] = boardEmptyColor;
            }
        }
        Object.assign(status, { score: 0, level: 0, speed: 0, over: false });
        cshape = createShape(ts);
        nshape = createShape(ts);
        updateBoard(ts);
    }

    let gtime = 0;
    const update = (ts, keys, addKeys, removeKeys) => {
        if (!dhItems.timeActions) {
            if (!status.over) {
                checkKeys(ts, keys, addKeys, removeKeys);
            }
            if (options.isFreeze) {
                gtime = ts;
            }
            else if (ts > gtime && !status.over) {
                if (ts - gtime > (speeds[status.speed] ?? 1)) {
                    gtime = ts;
                    globalDown(ts);
                }
            }
            if (!status.over) {
                if (!cshape || cshape.finished) {
                    cshape = nshape;
                    nshape = createShape(ts);
                }
            }
        }
        updateBoard(ts);
    }
    return { status, init, update };
}


export { game as default };
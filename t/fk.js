import keys from './keyboard.js';

function game(options) {
    const status = {
        score: 0,
        speed: 0,
        level: 0,
        over: true,
    };

    const maxLevel = 10000;
    let lastTagTime = 0;
    let shapeCallbacks = new Set();

    options = Object.assign({ hasExtend: false, hasHelper: false, isFreeze: false, isReverse: false }, options ?? {});
    const scores = { 0: 0, 1: 100, 2: 300, 3: 700, 4: 1500 };
    const speeds = { 0: 1000, 1: 900, 2: 800, 3: 700, 4: 600, 5: 500, 6: 450, 7: 400, 8: 350, 9: 300, 10: 250, 11: 200, 12: 150, 13: 100, 14: 50, 15: 25, 16: 10 };

    let app, cshape, nshape, allShapes, baseBoard;

    function rotateItem(ts, item, outofBoard) {
        const shape = item.shape;
        const rows = shape.length;
        const cols = shape[0].length;
        const fix = ~~((rows - cols) / 2);
        const ry = item.cy + fix;
        let rx = item.cx - fix;
        if (ry + cols > app.mainRows) {
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
        return array.every(([x, y]) => x >= 0 && x < app.mainCols && (y < 0 || (y < app.mainRows && baseBoard[y][x] === app.emptyCell)));
    };

    function createScorePauseCallback(ts, lines, overLines) {
        app.addPauseCallback((newTs) => {
            let ets = ~~((newTs - ts) / 20);
            if (ets > 10) {
                lines.forEach(line => baseBoard.splice(line, 1))
                overLines.forEach(line => baseBoard.unshift(line));
                for (let r = 0; r < lines.length - overLines.length; r++) {
                    let emptyRow = [];
                    for (let c = 0; c < app.mainCols; c++) {
                        emptyRow.push(app.emptyCell);
                    }
                    baseBoard.unshift(emptyRow);
                }
                status.score += scores[lines.length];
                const result = ~~(status.score / 10000);
                const speedLength = Object.keys(speeds).length;
                status.speed = result % speedLength;
                status.level = ~~(result / speedLength);
                if (status.level > maxLevel) {
                    status.over = true;
                }
                lastTagTime = newTs;
                updateBoard();
                return false;
            }
            else {
                updateBoard();
                while (--ets >= 0) {
                    lines.forEach((line, i) => {
                        const index = i % 2 ? app.mainCols - 1 - ets : ets;
                        app.mainBoard[line][index] = app.emptyCell;
                    });
                }
                return true;
            }
        });
    }

    const calcScore = (ts, fromLine, toLine, overLines) => {
        const lines = [];
        for (let r = toLine; r >= fromLine; r--) {
            if (baseBoard[r].every((value) => value !== app.emptyCell)) {
                (lines ??= []).push(r);
            }
        }
        if (overLines.length > lines) {
            status.over = true;
            return 0;
        }
        if (lines.length > 0) {
            createScorePauseCallback(ts, lines, overLines);
        }
        return lines.length;
    }

    const mergeItem = (ts, item) => {
        let count = 0 - item.cy;
        const overLines = [];
        while (count-- > 0) {
            let emptyRow = [];
            for (let c = 0; c < app.mainCols; c++) {
                emptyRow.push(app.emptyCell);
            }
            overLines.push(emptyRow);
        }
        for (let r = 0; r < item.shape.length; r++) {
            if (item.cy + r < 0) {
                for (let c = 0; c < item.shape[r].length; c++) {
                    if (item.shape[r][c]) {
                        overLines[r][item.cx + c] = item.cell;
                    }
                }
            }
            else {
                for (let c = 0; c < item.shape[r].length; c++) {
                    if (item.shape[r][c]) {
                        baseBoard[item.cy + r][item.cx + c] = item.cell;
                    }
                }
            }
        }
        const fromLine = item.cy > 0 ? item.cy : 0;
        const toLine = item.cy + item.shape.length - 1;
        calcScore(ts, fromLine, toLine, overLines);
        item.finished = true;
    }

    const globalMove = (ts, x) => {
        var newx = cshape.cx + x;
        if (newx < 0 || newx + cshape.shape[0].length > app.mainCols) {
            return false;
        }
        const array = calcArray(cshape.shape, newx, cshape.cy);
        if (isAllAllowed(array)) {
            cshape.cx = newx;
            return true;
        }
        return false;
    }

    const commonDown = (ts, item) => {
        const newy = item.cy + 1;
        const array = calcArray(item.shape, item.cx, newy);
        if (isAllAllowed(array)) {
            item.cy = newy;
            return;
        }
        mergeItem(ts, item);
    }
    const dotDown = (ts, item) => {
        if (item.cy > 0 && baseBoard[item.cy][item.cx] == app.emptyCell && (item.cy == app.mainRows - 1 || baseBoard[item.cy + 1][item.cx] != app.emptyCell)) {
            baseBoard[item.cy][item.cx] = item.cell;
            if (calcScore(ts, item.cy, item.cy, []) || item.cy == app.mainRows - 1) {
                item.finished = true;
                return;
            }
            baseBoard[item.cy][item.cx] = app.emptyCell;
        }
        const array = [];
        const newy = item.cy + 1;
        for (let r = newy; r < app.mainRows; r++) {
            array.push([item.cx, r]);
        }
        if (array.some(([x, y]) => x >= 0 && x < app.mainCols && (y < 0 || (y < app.mainRows && baseBoard[y][x] == app.emptyCell)))) {
            item.cy = newy;
            return;
        }
        if (item.cy < 0) {
            status.over = true;
            return;
        }
        baseBoard[item.cy][item.cx] = item.cell;
        item.finished = true;
    }

    const cancelDown = (ts, item) => {
        const newy = item.cy + 1;
        const array = calcArray(item.shape, item.cx, newy);
        if (isAllAllowed(array)) {
            item.cy = newy;
            return;
        }
        item.finished = true;
    }
    const cancelUp = (ts, item) => {
        const x = item.cx;
        let sy = item.cy + item.shape.length >= 0 ? item.cy + item.shape.length : 0;
        if (sy >= app.mainRows) {
            return;
        }
        const newCell = item.cell;
        shapeCallbacks.add(newTs => {
            let ets = ~~((newTs - ts) / 5);
            let i = 0;
            for (i = 0; i < ets; i++) {
                let calcY = sy + i;
                if (calcY >= app.mainRows) {
                    return false;
                }
                if (baseBoard[calcY][x] != app.emptyCell) {
                    baseBoard[calcY][x] = app.emptyCell;
                    return false;
                }
            }
            if (sy + i < app.mainRows) {
                app.mainBoard[sy + i][x] = newCell;
            }
            return true;
        });
    }

    const addtionDown = cancelDown;
    const addtionUp = (ts, item) => {
        const x = item.cx;
        let sy = item.cy + item.shape.length >= 0 ? item.cy + item.shape.length : 0;
        if (sy >= app.mainRows || baseBoard[sy][x] != app.emptyCell) {
            return;
        }
        const newCell = item.cell;
        if (sy + 1 >= app.mainRows || baseBoard[sy + 1][x] != app.emptyCell) {
            baseBoard[sy][x] = newCell;
            calcScore(ts, sy, sy, []);
            return;
        }
        shapeCallbacks.add(newTs => {
            let ets = ~~((newTs - ts) / 5);
            let i = 0;
            for (i = 0; i < ets; i++) {
                let calcY = sy + i + 1;
                if (calcY >= app.mainRows || baseBoard[calcY][x] != app.emptyCell) {
                    const actionY = calcY - 1;
                    baseBoard[actionY][x] = newCell;
                    calcScore(ts, actionY, actionY, []);
                    return false;
                }
            }
            app.mainBoard[sy + i][x] = newCell;
            return true;
        });
    }

    const bomb = (ts, item) => {
        var sx = item.cx - 1;
        var ex = item.cx + item.shape[0].length + 1;
        var sy = item.cy;
        var ey = item.cy + item.shape.length + 3;
        if (ey > app.mainRows) {
            ey = app.mainRows;
            if (ey - sy < 4) {
                sy = ey - 4;
            }
        }

        const bombItems = [];
        for (let r = sy; r < ey; r++) {
            if (r < 0 || r >= app.mainRows) {
                continue;
            }
            for (let c = sx; c < ex; c++) {
                if (c < 0 || c >= app.mainCols) {
                    continue;
                }
                if (baseBoard[r][c] !== app.emptyCell) {
                    bombItems.push([c, r]);
                }
            }
        }
        bombItems.forEach(([c, r]) => baseBoard[r][c] = app.emptyCell);

        const newCell = item.cell;
        const bombMaping = [[], []];
        const bombY = ~~((sy + ey) / 2) - 2;
        for (let rows = 0; rows < 4; rows++) {
            if (bombY + rows < 0 || bombY + rows >= app.mainRows) {
                continue;
            }
            for (var cols = 0; cols < 4; cols++) {
                let match = (cols == rows) || (cols + rows == 3);
                bombMaping[match ? 0 : 1].push([item.cx + cols, bombY + rows, newCell]);
            }
        }
        app.addPauseCallback((newTs) => {
            const ets = ~~((newTs - ts) / 100);
            if (ets > 4) {
                return false;
            }
            updateBoard();
            bombMaping[ets % 2].forEach(([x, y, cell]) => {
                app.mainBoard[y][x] = cell;
            })
            return true;
        });
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
        { shape: [[1], [1], [1]], downAction: addtionDown, upAction: addtionUp, isHelper: true },
        { shape: [[1, 0, 0, 1], [0, 1, 1, 0], [0, 1, 1, 0]], downAction: bombDown, upAction: bombUp, isHelper: true }
    ];

    function updateBoard(ts) {
        for (let r = 0; r < app.mainRows; r++) {
            for (let c = 0; c < app.mainCols; c++) {
                app.mainBoard[r][c] = baseBoard[r][c];
            }
        }
        cshape && !cshape.finished && cshape.shape.forEach((row, r) => {
            row.forEach((value, c) => {
                if (value && r + cshape.cy >= 0 && cshape.cx + c >= 0) {
                    app.mainBoard[r + cshape.cy][cshape.cx + c] = cshape.isHelper && (~~ts % 300) > 150 ? app.emptyCell : cshape.cell;
                }
            });
        });
    }


    function updateSubBoard(ts) {
        const srow = nshape.shape.length;
        const scol = nshape.shape[0].length;
        const sr = ~~((app.subRows - srow) / 2);
        const sc = ~~((app.subCols - scol) / 2);
        for (let r = 0; r < app.subRows; r++) {
            for (let c = 0; c < app.subCols; c++) {
                app.subBoard[r][c] = r < sr || r - sr >= srow || c < sc || c - sc >= scol || nshape.shape[r - sr][c - sc] == 0 ? app.emptyCell : nshape.cell;
            }
        }
    }

    allShapes = [
        ...shapes,
        ...(options.hasExtend ? extShapes : []),
        ...(options.hasHelper ? helpShapes : [])]

    function updateNextShape(ts, refresh) {
        const nextShape = nshape;
        const pickShape = allShapes[~~(Math.random() * allShapes.length)];
        const cell = app.cells[~~(Math.random() * app.cells.length)];
        const shape = pickShape.shape.map(row => [...row]);
        const item = Object.assign({}, pickShape, { shape, cell });
        item.isHelper || Array.from({ length: ~~(Math.random() * 4) }).map(() => rotateItem(ts, item, true));
        Object.assign(item, { cx: ~~((app.mainCols - item.shape[0].length) / 2), cy: -item.shape.length })
        nshape = item;
        refresh && updateSubBoard(ts);
        return nextShape;
    }

    const globalDown = (ts) => {
        cshape.downAction(ts, cshape);
        lastTagTime = ts;
        return !cshape.finished;
    }

    const globalUp = (ts) => {
        cshape.upAction(ts, cshape);
        return true;
    }

    const globalExtend = (ts) => {
        updateNextShape(ts, true);
        return true;
    }

    const keyMap = {
        [keys.KEY_LEFT]: [100, 0, (ts) => globalMove(ts, -1)],
        [keys.KEY_RIGHT]: [100, 0, (ts) => globalMove(ts, 1)],
        [keys.KEY_DOWN]: [100, 0, (ts) => globalDown(ts)],
        [keys.KEY_UP]: [200, 50, (ts) => globalUp(ts)],
        [keys.KEY_ACTION]: [200, 50, (ts) => globalUp(ts)],
        [keys.KEY_EXTEND]: [100, 100, (ts) => globalExtend(ts)]
    };


    const init = (ts, mainApp) => {
        lastTagTime = ts;
        app = mainApp;
        baseBoard = [];
        shapeCallbacks.clear();
        for (let r = 0; r < app.mainRows; r++) {
            let emptyRow = [];
            for (let c = 0; c < app.mainCols; c++) {
                emptyRow.push(app.emptyCell);
            }
            baseBoard.push(emptyRow);
        }
        Object.assign(status, { score: 0, level: 0, speed: 0, over: false });
        updateNextShape(ts, false);
        cshape = updateNextShape(ts, true);
        updateBoard(ts);
    }
    const update = (ts) => {
        if (!options.isFreeze && ts > lastTagTime && !status.over) {
            if (ts - lastTagTime > (speeds[status.speed] ?? 1)) {
                lastTagTime = ts;
                globalDown(ts);
            }
        }
        if (!status.over) {
            if (!cshape || cshape.finished) {
                cshape = updateNextShape(ts, true);
            }
        }
        updateBoard(ts);
        for (let callback of [...shapeCallbacks]) {
            if (!callback(ts)) {
                shapeCallbacks.delete(callback);
                break;
            }
        }
    }
    return { status, keyMap, init, update };
}


export { game as default };
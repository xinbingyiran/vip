import keys from './keyboard.js';

function game(options) {
    const status = {
        score: 0,
        speed: 0,
        level: 0,
        over: true,
    };

    const maxLevel = 10000;
    const scorePerSpeed = 1000;
    let lastTagTime = 0;
    let shapeCallbacks = new Set();

    options = Object.assign({ hasExtend: false, hasHelper: false, isFreeze: false, isReverse: false }, options ?? {});
    const scores = { 0: 0, 1: 100, 2: 300, 3: 700, 4: 1500 };
    const speeds = { 0: 1000, 1: 900, 2: 800, 3: 700, 4: 600, 5: 500, 6: 450, 7: 400, 8: 350, 9: 300, 10: 250, 11: 200, 12: 150, 13: 100, 14: 50, 15: 25, 16: 10 };

    let app, cshape, nshape, allShapes, baseBoard;

    function createEmptyRow() {
        let emptyRow = [];
        for (let c = 0; c < app.mainCols; c++) {
            emptyRow.push(app.emptyCell);
        }
        return emptyRow;
    }
    function createScorePauseCallback(ts, lines, overLines) {
        app.addPauseCallback((newTs) => {
            let ets = ~~((newTs - ts) / 20);
            if (ets > 10) {
                lines.forEach(line => baseBoard.splice(line, 1))
                overLines.forEach(line => baseBoard.unshift(line));
                for (let r = 0; r < lines.length - overLines.length; r++) {
                    baseBoard.unshift(createEmptyRow());
                }
                status.score += scores[lines.length];
                const totalSpeed = ~~(status.score / scorePerSpeed);
                const speedLength = Object.keys(speeds).length;
                status.speed = totalSpeed % speedLength;
                status.level = ~~(totalSpeed / speedLength);
                if (status.level > maxLevel) {
                    status.over = true;
                }
                fixTagTime(newTs - ts);
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
    function rotateArray(shape) {
        const rotated = [];
        const rows = shape.length;
        const cols = shape[0].length;

        for (let c = 0; c < cols; c++) {
            rotated[c] = [];
            for (let r = 0; r < rows; r++) {
                rotated[c][r] = options.isReverse ? shape[r][cols - 1 - c] : shape[rows - 1 - r][c];
            }
        }
        return rotated;
    }
    function rotateItem(ts) {
        const rotated = rotateArray(cshape.shape);
        const rows = rotated.length;
        const cols = rotated[0].length;
        const colsGrow = ~~((cols - rows) / 2);
        const newy = cshape.cy + colsGrow;
        let newx = cshape.cx - colsGrow;
        if (newy + cols > app.mainRows) {
            return;
        }
        const addtion = cols > rows ? ((~~((cols + 1 - rows) / 2)) * 2) : (rows - cols);
        for (let i = 0; i <= addtion; i++) {
            newx += i * ((i % 2 == 0) ? -1 : 1);
            if (canFitToMain(rotated, cols, rows, newx, newy)) {
                cshape.shape = rotated;
                cshape.cx = newx;
                cshape.cy = newy;
                return;
            }
        }
    }
    function canFitToMain(shape, cols, rows, cx, cy) {
        if (cx < 0 || cx + cols > app.mainCols || cy + rows > app.mainRows) {
            return false;
        }
        for (let r = 0; r < rows; r++) {
            if (cy + r < 0) {
                continue;
            }
            for (let c = 0; c < cols; c++) {
                if (shape[r][c] && baseBoard[cy + r][cx + c] != app.emptyCell) {
                    return false;
                }
            }
        }
        return true;
    }

    function mergeToMain(ts, shape, cols, rows, cx, cy, cell) {
        const lines = [];
        const emptyRows = [];
        for (let r = rows - 1; r >= 0; r--) {
            if (cy + r < 0) {
                const emptyRow = createEmptyRow();
                for (let c = 0; c < cols; c++) {
                    shape[r][c] && (emptyRow[cx + c] = cell);
                }
                emptyRows.push(emptyRow);
                continue;
            }
            let fullRow = true;
            for (let c = 0; c < app.mainCols; c++) {
                if (baseBoard[r + cy][c] == app.emptyCell) {
                    if (c - cx < 0 || c - cx > cols || !shape[r][c - cx]) {
                        fullRow = false;
                        continue;
                    }
                    baseBoard[r + cy][c] = cell;
                }
            }
            fullRow && lines.push(cy + r);
        }
        if (lines.length < emptyRows.length) {
            status.over = true;
        }
        else if (lines.length) {
            createScorePauseCallback(ts, lines, emptyRows);
        }
    }


    const globalMove = (ts, step) => {
        var newx = cshape.cx + step[0];
        var newy = cshape.cy + step[1];
        if (canFitToMain(cshape.shape, cshape.shape[0].length, cshape.shape.length, newx, cshape.cy)) {
            cshape.cx = newx;
            cshape.cy = newy;
            return true;
        }
        return false;
    }
    const mergeCurrent = (ts) => {
        mergeToMain(ts, cshape.shape, cshape.shape[0].length, cshape.shape.length, cshape.cx, cshape.cy, cshape.cell);
        cshape.finished = true;
        lastTagTime = ts;
    }
    const commonDown = (ts) => {
        if (canFitToMain(cshape.shape, cshape.shape[0].length, cshape.shape.length, cshape.cx, cshape.cy + 1)) {
            cshape.cy = cshape.cy + 1;
            return true;
            // if (canFitToMain(cshape.shape, cshape.shape[0].length, cshape.shape.length, cshape.cx, cshape.cy + 1)) {
            //     return true;
            // }
            // if (canFitToMain(cshape.shape, cshape.shape[0].length, cshape.shape.length, cshape.cx - 1, cshape.cy)
            //     || canFitToMain(cshape.shape, cshape.shape[0].length, cshape.shape.length, cshape.cx + 1, cshape.cy)) {
            //     return true;
            // }
        }
        mergeCurrent(ts);
        return false;
    }

    const dotInFull = (x, y) => {
        if (x < 0 || x >= app.mainCols || y < 0 || y >= app.mainRows) {
            return false;
        }
        for (let c = 0; c < app.mainCols; c++) {
            if (c == x ? baseBoard[y][c] != app.emptyCell : baseBoard[y][c] == app.emptyCell) {
                return false;
            }
        }
        return y + 1 >= app.mainRows || baseBoard[y + 1][x] != app.emptyCell;
    }
    const dotItem = [[1]];

    const dotDown = (ts) => {
        if (dotInFull(cshape.cx, cshape.cy)) {
            mergeCurrent(ts);
            return false;
        }
        let newy = cshape.cy + 1;
        for (; newy < app.mainRows; newy++) {
            if (baseBoard[newy][cshape.cx] == app.emptyCell) {
                cshape.cy++;
                break;
            }
        }
        if (newy == app.mainRows) {
            mergeCurrent(ts);
            return false;
        }
        // if (dotInFull(cshape.cx, cshape.cy)) {
        //     mergeCurrent(ts);
        //     return false;
        // }
        return true;
    }

    const cancelDown = (ts) => {
        if (canFitToMain(cshape.shape, cshape.shape[0].length, cshape.shape.length, cshape.cx, cshape.cy + 1)) {
            cshape.cy = cshape.cy + 1;
            return true;
        }
        cshape.finished = true;
        return false;
    }
    const cancelUp = (ts) => {
        const x = cshape.cx;
        let sy = cshape.cy + cshape.shape.length >= 0 ? cshape.cy + cshape.shape.length : 0;
        if (sy >= app.mainRows) {
            return;
        }
        const newCell = cshape.cell;
        shapeCallbacks.add(newTs => {
            let ets = ~~((newTs - ts) / 5);
            let i = 0;
            for (i = 0; i < ets; i++) {
                let calcY = sy + i;
                if (calcY >= app.mainRows) {
                    return false;
                }
                if (baseBoard[calcY][x] != app.emptyCell) {
                    app.mainBoard[calcY][x] = baseBoard[calcY][x] = app.emptyCell;
                    return false;
                }
            }
            if (sy + i < app.mainRows) {
                app.mainBoard[sy + i][x] = newCell;
            }
            return true;
        });
        return true;
    }

    const addtionDown = cancelDown;
    const addtionUp = (ts) => {
        const x = cshape.cx;
        let sy = cshape.cy + cshape.shape.length >= 0 ? cshape.cy + cshape.shape.length : 0;
        if (sy >= app.mainRows || baseBoard[sy][x] != app.emptyCell) {
            return;
        }
        const newCell = cshape.cell;
        shapeCallbacks.add(newTs => {
            let ets = ~~((newTs - ts) / 5);
            let i = 0;
            for (i = 0; i < ets; i++) {
                let calcY = sy + i;
                if (calcY + 1 >= app.mainRows || baseBoard[calcY + 1][x] != app.emptyCell) {
                    mergeToMain(newTs, dotItem, 1, 1, x, calcY, newCell);
                    return false;
                }
            }
            app.mainBoard[sy + i][x] = newCell;
            return true;
        });
        return true;
    }

    const bomb = (ts) => {
        var sx = cshape.cx - 1;
        var ex = cshape.cx + cshape.shape[0].length + 1;
        var sy = cshape.cy;
        var ey = cshape.cy + cshape.shape.length + 3;
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

        const newCell = cshape.cell;
        const bombMaping = [[], []];
        const bombY = ~~((sy + ey) / 2) - 2;
        for (let rows = 0; rows < 4; rows++) {
            if (bombY + rows < 0 || bombY + rows >= app.mainRows) {
                continue;
            }
            for (var cols = 0; cols < 4; cols++) {
                let match = (cols == rows) || (cols + rows == 3);
                bombMaping[match ? 0 : 1].push([cshape.cx + cols, bombY + rows, newCell]);
            }
        }
        app.addPauseCallback((newTs) => {
            const ets = ~~((newTs - ts) / 100);
            if (ets > 4) {
                fixTagTime(newTs - ts);
                return false;
            }
            updateBoard();
            bombMaping[ets % 2].forEach(([x, y, cell]) => {
                app.mainBoard[y][x] = cell;
            })
            return true;
        });
        cshape.finished = true;
    }
    const bombUp = (ts) => {
        bomb(ts);
        return true;
    }
    const bombDown = (ts) => {
        if (canFitToMain(cshape.shape, cshape.shape[0].length, cshape.shape.length, cshape.cx, cshape.cy + 1)) {
            cshape.cy = cshape.cy + 1;
            return true;
        }
        bomb(ts);
        return false;
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
        const newShape = Object.assign({}, pickShape, { shape, cell });
        if (!newShape.isHelper) {
            let count = ~~(Math.random() * 4);
            while (count-- > 0) {
                newShape.shape = rotateArray(newShape.shape);
            }
        }
        Object.assign(newShape, { cx: ~~((app.mainCols - newShape.shape[0].length) / 2), cy: -newShape.shape.length })
        nshape = newShape;
        refresh && updateSubBoard(ts);
        return nextShape;
    }

    const globalDown = ts => {
        return cshape.downAction(ts);
    }

    const globalUp = (ts) => {
        cshape.upAction(ts);
        return true;
    }

    const globalExtend = (ts) => {
        updateNextShape(ts, true);
        return true;
    }

    const keyMap = {
        [keys.KEY_LEFT]: [100, 0, (ts) => globalMove(ts, [-1, 0])],
        [keys.KEY_RIGHT]: [100, 0, (ts) => globalMove(ts, [1, 0])],
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
            baseBoard.push(createEmptyRow());
        }
        Object.assign(status, { score: 0, level: 0, speed: 0, over: false });
        updateNextShape(ts, false);
        cshape = updateNextShape(ts, true);
        updateBoard(ts);
    }

    function fixTagTime(addtion) {
        lastTagTime += addtion;
    }
    const update = (ts) => {
        if (!options.isFreeze && ts > lastTagTime && !status.over) {
            if (ts - lastTagTime > (speeds[status.speed] ?? 1)) {
                globalDown(ts);
                lastTagTime = ts;
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
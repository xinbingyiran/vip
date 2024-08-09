import keys from './keyboard.js';

function game({ hasExtend = false, hasHelper = false, isFreeze = false, isReverse = false } = {}) {

    const maxLevel = 10000;
    const scorePerSpeed = 50000;
    let lastTagTime = 0;
    let actionCallbacks;
    let scoreItems;
    let overItems;

    const scores = { 0: 0, 1: 100, 2: 300, 3: 700, 4: 1500 };

    let app, curShape, nshape, allShapes, baseBoard;

    function createEmptyRow() {
        let emptyRow = [];
        for (let c = 0; c < app.mainCols; c++) {
            emptyRow.push(app.emptyCell);
        }
        return emptyRow;
    }
    function createOrUpdateScorePauseCallback(ts, lines, overLines) {
        scoreItems.add(lines);
        if (scoreItems.size > 1) {
            return;
        }
        app.addFreezeCallback(elapsedTime => {
            let ets = ~~(elapsedTime / 20);
            if (ets > 10) {
                const oldScore = app.status.score;
                const allLines = [];
                for (let scoreLines of scoreItems) {
                    app.status.score += scores[scoreLines.length];
                    scoreItems.delete(scoreLines);
                    allLines.push(...scoreLines);
                };
                allLines.sort().reverse().forEach(line => baseBoard.splice(line, 1));
                overLines.forEach(line => baseBoard.unshift(line));
                while (baseBoard.length < app.mainRows) {
                    baseBoard.unshift(createEmptyRow());
                }
                if (~~(app.status.score / scorePerSpeed) != ~~(oldScore / scorePerSpeed)) {
                    updateGrade(ts);
                }
                return false;
            }
            else {
                while (--ets >= 0) {
                    scoreItems.forEach(scoreLines => scoreLines.forEach(line => {
                        const index = line % 2 ? app.mainCols - 1 - ets : ets;
                        app.mainBoard[line][index] = app.emptyCell;
                    }));
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
                rotated[c][r] = isReverse ? shape[r][cols - 1 - c] : shape[rows - 1 - r][c];
            }
        }
        return rotated;
    }
    function rotateItem(ts) {
        const rotated = rotateArray(curShape.shape);
        const rows = rotated.length;
        const cols = rotated[0].length;
        const colsGrow = ~~((cols - rows) / 2);
        const newy = curShape.cy + colsGrow;
        let newx = curShape.cx - colsGrow;
        if (newy + cols > app.mainRows) {
            return;
        }
        const addtion = cols > rows ? ((~~((cols + 1 - rows) / 2)) * 2) : (rows - cols);
        for (let i = 0; i <= addtion; i++) {
            newx += i * ((i % 2 == 0) ? -1 : 1);
            if (canFitToMain(rotated, cols, rows, newx, newy)) {
                curShape.shape = rotated;
                curShape.cx = newx;
                curShape.cy = newy;
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
            app.status.over = true;
        }
        else if (lines.length) {
            createOrUpdateScorePauseCallback(ts, lines, emptyRows);
        }
    }


    const globalMove = (ts, step) => {
        var newx = curShape.cx + step[0];
        var newy = curShape.cy + step[1];
        if (canFitToMain(curShape.shape, curShape.shape[0].length, curShape.shape.length, newx, curShape.cy)) {
            curShape.cx = newx;
            curShape.cy = newy;
            return true;
        }
        return false;
    }
    const mergeCurrent = (ts) => {
        mergeToMain(ts, curShape.shape, curShape.shape[0].length, curShape.shape.length, curShape.cx, curShape.cy, curShape.cell);
        curShape.finished = true;
        lastTagTime = ts;
    }
    const commonDown = (ts) => {
        if (canFitToMain(curShape.shape, curShape.shape[0].length, curShape.shape.length, curShape.cx, curShape.cy + 1)) {
            curShape.cy = curShape.cy + 1;
            return true;
            // if (canFitToMain(curShape.shape, curShape.shape[0].length, curShape.shape.length, curShape.cx, curShape.cy + 1)) {
            //     return true;
            // }
            // if (canFitToMain(curShape.shape, curShape.shape[0].length, curShape.shape.length, curShape.cx - 1, curShape.cy)
            //     || canFitToMain(curShape.shape, curShape.shape[0].length, curShape.shape.length, curShape.cx + 1, curShape.cy)) {
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
        if (dotInFull(curShape.cx, curShape.cy)) {
            mergeCurrent(ts);
            return false;
        }
        let newy = curShape.cy + 1;
        for (; newy < app.mainRows; newy++) {
            if (baseBoard[newy][curShape.cx] == app.emptyCell) {
                curShape.cy++;
                break;
            }
        }
        if (newy == app.mainRows) {
            mergeCurrent(ts);
            return false;
        }
        // if (dotInFull(curShape.cx, curShape.cy)) {
        //     mergeCurrent(ts);
        //     return false;
        // }
        return true;
    }

    const cancelDown = (ts) => {
        if (canFitToMain(curShape.shape, curShape.shape[0].length, curShape.shape.length, curShape.cx, curShape.cy + 1)) {
            curShape.cy = curShape.cy + 1;
            return true;
        }
        curShape.finished = true;
        return false;
    }
    const cancelUp = (ts) => {
        const x = curShape.cx;
        let sy = curShape.cy + curShape.shape.length >= 0 ? curShape.cy + curShape.shape.length : 0;
        if (sy >= app.mainRows) {
            return;
        }
        const newCell = curShape.cell;
        const overItem = {
            x: x,
            y: sy,
            cell: newCell
        }
        overItems.add(overItem);
        actionCallbacks.add(newTs => {
            let ets = ~~((newTs - ts) / 5);
            let i = 0;
            for (i = 0; i <= ets; i++) {
                let calcY = sy + i;
                if (calcY >= app.mainRows) {
                    overItems.delete(overItem);
                    return false;
                }
                if (baseBoard[calcY][x] != app.emptyCell) {
                    baseBoard[calcY][x] = app.emptyCell;
                    overItems.delete(overItem);
                    return false;
                }
            }
            if (sy + i < app.mainRows) {
                overItem.y = sy + i;
            }
            return true;
        });
        return true;
    }

    const addtionDown = cancelDown;
    const addtionUp = (ts) => {
        const x = curShape.cx;
        let sy = curShape.cy + curShape.shape.length >= 0 ? curShape.cy + curShape.shape.length : 0;
        if (sy >= app.mainRows || baseBoard[sy][x] != app.emptyCell) {
            return;
        }
        const newCell = curShape.cell;
        const overItem = {
            x: x,
            y: sy,
            cell: newCell
        }
        overItems.add(overItem);
        actionCallbacks.add(newTs => {
            let ets = ~~((newTs - ts) / 5);
            let i = 0;
            for (i = 0; i <= ets; i++) {
                let calcY = sy + i;
                if (calcY + 1 >= app.mainRows || baseBoard[calcY + 1][x] != app.emptyCell) {
                    mergeToMain(newTs, dotItem, 1, 1, x, calcY, newCell);
                    overItems.delete(overItem);
                    return false;
                }
            }
            overItem.y = sy + i;
            return true;
        });
        return true;
    }

    const bomb = (ts) => {
        var sx = curShape.cx - 1;
        var ex = curShape.cx + curShape.shape[0].length + 1;
        var sy = curShape.cy;
        var ey = curShape.cy + curShape.shape.length + 3;
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

        const newCell = curShape.cell;
        const bombMaping = [[], []];
        const bombY = ~~((sy + ey) / 2) - 2;
        for (let rows = 0; rows < 4; rows++) {
            if (bombY + rows < 0 || bombY + rows >= app.mainRows) {
                continue;
            }
            for (var cols = 0; cols < 4; cols++) {
                let match = (cols == rows) || (cols + rows == 3);
                bombMaping[match ? 0 : 1].push([curShape.cx + cols, bombY + rows, newCell]);
                bombMaping[match ? 1 : 0].push([curShape.cx + cols, bombY + rows, app.emptyCell]);
            }
        }
        app.addFreezeCallback(elapsedTime => {
            let ets = ~~(elapsedTime / 100);
            if (ets > 4) {
                return false;
            }
            bombMaping[ets % 2].forEach(([x, y, cell]) => {
                app.mainBoard[y][x] = cell;
            })
            return true;
        });
        curShape.finished = true;
    }
    const bombUp = (ts) => {
        bomb(ts);
        return true;
    }
    const bombDown = (ts) => {
        if (canFitToMain(curShape.shape, curShape.shape[0].length, curShape.shape.length, curShape.cx, curShape.cy + 1)) {
            curShape.cy = curShape.cy + 1;
            return true;
        }
        bomb(ts);
        return false;
    }

    const shapes = [
        [[1, 1], [1, 1]],
        [[1, 0, 0], [1, 1, 1]],
        [[0, 0, 1], [1, 1, 1]],
        [[0, 1, 0], [1, 1, 1]],
        [[0, 1, 1], [1, 1, 0]],
        [[1, 1, 0], [0, 1, 1]],
        [[1, 1, 1, 1]]
    ].map(s => ({ shape: s, downAction: commonDown, upAction: rotateItem }));


    const extShapes = [
        [[1, 0], [1, 1]],
        [[1, 0, 0], [1, 1, 1], [1, 0, 0]],
        [[1, 0, 0], [1, 1, 1], [0, 0, 1]],
        [[0, 1, 0], [1, 1, 1], [0, 1, 0]],
        [[0, 0, 1], [1, 1, 1], [1, 0, 0]],
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
        curShape && !curShape.finished && curShape.shape.forEach((row, r) => {
            row.forEach((value, c) => {
                if (value && r + curShape.cy >= 0 && curShape.cx + c >= 0) {
                    app.mainBoard[r + curShape.cy][curShape.cx + c] = curShape.isHelper && (~~ts % 300) > 150 ? app.emptyCell : curShape.cell;
                }
            });
        });

        overItems.forEach(item => {
            app.mainBoard[item.y][item.x] = item.cell;
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
        ...(hasExtend ? extShapes : []),
        ...(hasHelper ? helpShapes : [])]

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
        return curShape.downAction(ts);
    }

    const globalUp = (ts) => {
        curShape.upAction(ts);
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



    const initLevel = ts => {
        baseBoard.forEach(row => row.fill(app.emptyCell));
        actionCallbacks = new Set();
        overItems = new Set();
        scoreItems = new Set();
        curShape = updateNextShape(ts, true);
        updateBoard(ts);
        lastTagTime = ts;
    }

    const init = (ts, mainApp) => {
        app = mainApp;
        baseBoard = [];
        for (let r = 0; r < app.mainRows; r++) {
            baseBoard.push(createEmptyRow());
        }
        updateNextShape(ts, false);
        initLevel(ts);
    }

    function updateGrade(ts) {
        if (!app.status.updateSpeed(app.speeds.length) && !app.status.updateGrade(maxLevel)) {
            return false;
        }
        app.addFlashCallback(() => initLevel(ts));
        return true;
    }

    const update = (ts) => {
        if (!isFreeze && ts > lastTagTime) {
            if (ts - lastTagTime > app.speeds[app.status.speed]) {
                globalDown(ts);
                lastTagTime = ts;
            }
        }
        if (actionCallbacks.size) {
            for (let callback of actionCallbacks) {
                if (!callback(ts)) {
                    actionCallbacks.delete(callback);
                }
            }
        }
        else if (!curShape || curShape.finished) {
            curShape = updateNextShape(ts, true);
        }
        updateBoard(ts);
    }
    return { keyMap, init, update };
}

export { game as default };
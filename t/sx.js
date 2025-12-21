function game(app, { isAddtion = false } = {}) {

    let maxLevel = 30;
    let scorePerSpeed = 50000;
    let scorePerDot = 10;

    let lastTagTime = 0;

    let actionCallbacks;
    let scoreItems;
    let overItems;

    let status, mainCell, actionItem, baseBoard;

    function createEmptyRow() {
        let emptyRow = [];
        for (let c = 0; c < app.mainCols; c++) {
            emptyRow.push(app.emptyCell);
        }
        return emptyRow;
    }

    function updateBoard(ts) {
        for (let r = 0; r < app.mainRows; r++) {
            for (let c = 0; c < app.mainCols; c++) {
                app.mainBoard[r][c] = baseBoard[r][c];
            }
        }
        app.mainBoard[app.mainRows - 2][actionItem.col] = actionItem.cell;
        app.mainBoard[app.mainRows - 1][actionItem.col] = actionItem.cell;
        actionItem.col > 0 && (app.mainBoard[app.mainRows - 1][actionItem.col - 1] = actionItem.cell);
        actionItem.col < app.mainCols - 1 && (app.mainBoard[app.mainRows - 1][actionItem.col + 1] = actionItem.cell);


        overItems.forEach(item => {
            app.mainBoard[item.y][item.x] = item.cell;
        });

        for (let i = 0; i < app.subRows; i++) {
            app.subBoard[i].fill(status.life > (app.subRows - 1 - i) ? actionItem.cell : app.emptyCell);
        }
    }

    function fillRow(row) {
        for (let i = 0; i < app.mainCols - 1; i++) {
            baseBoard[row][~~(Math.random() * app.mainCols)] = randomCell();
        }
    }

    function initLevel(ts) {
        lastTagTime = ts;
        for (let row = 0; row < app.mainRows; row++) {
            baseBoard[row].fill(app.emptyCell);
            if (app.mainRows - row <= status.level) {
                fillRow(row);
            }
        }
        actionItem.col = ~~(app.mainCols / 2);
        actionCallbacks = new Set();
        overItems = new Set();
        scoreItems = new Set();
        updateBoard(ts);
    }

    function updateGrade(ts) {
        if (!status.updateSpeed(app.speeds.length) && !status.updateGrade(maxLevel)) {
            return false;
        }
        status.updateLife(app.subRows, true);
        app.addFlashCallback(() => initLevel(ts));
        return true;
    }

    function subLife(ts) {
        if (!status.updateLife(app.subRows, false)) {
            return false;
        }
        app.addFlashCallback(() => initLevel(ts));
        return true;
    }

    const doMove = (ts, offset) => {
        const newx = actionItem.col + offset;
        if (newx < 0 || newx >= app.mainCols) {
            return false;
        }
        actionItem.col = newx;
        return true;
    };

    function createOrUpdateScorePauseCallback(ts, calcY) {
        scoreItems.add(calcY);
        if (scoreItems.size > 1) {
            return;
        }
        app.addFreezeCallback(elapsedTime => {
            let ets = ~~(elapsedTime / 10);
            if (ets > 10) {
                const oldScore = status.score;
                const allLines = [];
                for (let scoreLine of scoreItems) {
                    status.score += scorePerDot * 10;
                    scoreItems.delete(scoreLine);
                    allLines.push(scoreLine);
                };
                allLines.sort().reverse().forEach(line => baseBoard.splice(line, 1));
                while (baseBoard.length < app.mainRows) {
                    baseBoard.push(createEmptyRow());
                }
                if (~~(status.score / scorePerSpeed) != ~~(oldScore / scorePerSpeed)) {
                    updateGrade(ts);
                }
                return false;
            }
            else {
                while (--ets >= 0) {
                    scoreItems.forEach(line => {
                        const index = line % 2 ? app.mainCols - 1 - ets : ets;
                        app.mainBoard[line][index] = app.emptyCell;
                    });
                }
                return true;
            }
        });
    }


    const doAction = (ts) => {
        const x = actionItem.col;
        let sy = app.mainRows - 3;
        const newCell = actionItem.cell;
        const overItem = {
            x: x,
            y: sy,
            cell: newCell
        }
        if (isAddtion) {
            if (baseBoard[sy][x] != app.emptyCell) {
                return true;
            }
            overItems.add(overItem);
            actionCallbacks.add(newTs => {
                let ets = ~~((newTs - ts) / 5);
                let i = 0;
                for (i = 0; i < ets; i++) {
                    let calcY = sy - i;
                    if (calcY - 1 < 0 || baseBoard[calcY - 1][x] != app.emptyCell) {
                        baseBoard[calcY][x] = newCell;
                        if (baseBoard[calcY].every(s => s != app.emptyCell)) {
                            createOrUpdateScorePauseCallback(newTs, calcY);
                        }
                        overItems.delete(overItem);
                        return false;
                    }
                }
                overItem.y = sy - i;
                return true;
            })
        }
        else {
            overItems.add(overItem);
            actionCallbacks.add(newTs => {
                let ets = ~~((newTs - ts) / 5);
                let i = 0;
                for (i = 0; i <= ets; i++) {
                    let calcY = sy - i;
                    if (sy - i < 0) {
                        overItems.delete(overItem);
                        return false;
                    }
                    if (baseBoard[calcY][x] != app.emptyCell) {
                        baseBoard[calcY][x] = app.emptyCell;
                        const oldScore = status.score;
                        status.score = status.score + scorePerDot;
                        if (~~(status.score / scorePerSpeed) != ~~(oldScore / scorePerSpeed)) {
                            updateGrade(ts);
                        }
                        overItems.delete(overItem);
                        return false;
                    }
                }
                if (sy - i >= 0) {
                    overItem.y = sy - i;
                }
                return true;
            });
        }
        return true;
    };

    const doGrow = (ts) => {
        baseBoard.unshift(baseBoard.pop());
        fillRow(0);
        if (!baseBoard[app.mainRows - 1].every(s => s == app.emptyCell) || !baseBoard[app.mainRows - 2].every(s => s == app.emptyCell)) {
            subLife(ts);
            return false;
        }
        return true;
    };

    const keyMap = {
        [app.keys.KEY_LEFT]: [100, 50, (ts) => doMove(ts, -1)],
        [app.keys.KEY_RIGHT]: [100, 50, (ts) => doMove(ts, 1)],
        [app.keys.KEY_DOWN]: [100, 50, (ts) => doGrow(ts)],
        [app.keys.KEY_UP]: [100, 50, (ts) => doGrow(ts)],
        [app.keys.KEY_ACTION]: [100, 50, (ts) => doAction(ts)],
        [app.keys.KEY_EXTEND]: [3000, 1000, (ts) => updateGrade(ts)]
    };

    function randomCell() {
        if(app.cells.length == 1){
            return app.cells[0];
        }
        let retCell = mainCell;
        while((retCell = app.cells[~~(Math.random() * app.cells.length)]) == mainCell){}
        return retCell;
    }

    const init = (ts, mainStatus) => {
        status = mainStatus;
        maxLevel = app.mainRows - 5;
        baseBoard = [];
        for (let r = 0; r < app.mainRows; r++) {
            baseBoard.push(createEmptyRow());
        }
        mainCell = randomCell();
        actionItem = {
            col: ~~(app.mainCols / 2),
            cell: mainCell
        };
        initLevel(ts);
    }
    const update = (ts) => {
        if (ts - lastTagTime > app.speeds[status.speed] * 4) {
            lastTagTime = ts;
            doGrow(ts);
        }
        for (let callback of actionCallbacks) {
            if (!callback(ts)) {
                actionCallbacks.delete(callback);
            }
        }
        updateBoard(ts);
    }
    return { keyMap, init, update };
}

export { game as default };
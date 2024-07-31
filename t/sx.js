import keys from './keyboard.js';

function game(options) {

    let maxLevel = 30;
    let scorePerSpeed = 50000;
    let scorePerDot = 10;

    let lastTagTime = 0;

    let actionCallbacks = new Set();
    let scoreCallbackCreated;

    options = Object.assign({ fk: false }, options ?? {});

    let app, actionItem, baseBoard;

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

        for (let i = 0; i < app.subRows; i++) {
            app.subBoard[i].fill(app.status.life > (app.subRows - 1 - i) ? actionItem.cell : app.emptyCell);
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
            if (app.mainRows - row <= app.status.level) {
                fillRow(row);
            }
        }
        actionItem.col = ~~(app.mainCols / 2);
        actionCallbacks.clear();
        updateBoard(ts);
    }

    function updateGrade(ts) {
        if (!app.status.updateSpeed(app.speeds.length) && !app.status.updateGrade(maxLevel)) {
            return false;
        }
        app.status.updateLife(app.subRows, true);
        app.addFlashCallback(ts, (newTs) => initLevel(newTs));
        return true;
    }

    function subLife(ts) {
        if (!app.status.updateLife(app.subRows, false)) {
            return false;
        }
        app.addFlashCallback(ts, (newTs) => initLevel(newTs));
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

    function createScorePauseCallback(ts, calcY) {
        scoreCallbackCreated = true;
        app.addPauseCallback((newTs) => {
            let ets = ~~((newTs - ts) / 10);
            if (ets > 10) {
                baseBoard.push(...baseBoard.splice(calcY, 1));
                baseBoard[app.mainRows - 1].fill(app.emptyCell);
                const oldScore = app.status.score;
                app.status.score = app.status.score + scorePerDot * 10;
                if (~~(app.status.score / scorePerSpeed) != ~~(oldScore / scorePerSpeed)) {
                    updateGrade(ts);
                }
                else {
                    lastTagTime += newTs - ts;
                }
                return false;
            }
            else {
                //updateBoard();
                while (--ets >= 0) {
                    const index = ets;
                    app.mainBoard[calcY][index] = app.emptyCell;
                }
                return true;
            }
        });
    }

    const doAction = (ts) => {
        const x = actionItem.col;
        let sy = app.mainRows - 3;
        const newCell = actionItem.cell;
        if (options.fk) {
            if (baseBoard[sy][x] != app.emptyCell) {
                return true;
            }
            actionCallbacks.add(newTs => {
                let ets = ~~((newTs - ts) / 5);
                let i = 0;
                for (i = 0; i < ets; i++) {
                    let calcY = sy - i;
                    if (calcY - 1 < 0 || baseBoard[calcY - 1][x] != app.emptyCell) {
                        baseBoard[calcY][x] = newCell;
                        if (baseBoard[calcY].every(s => s != app.emptyCell)) {
                            createScorePauseCallback(newTs, [calcY], []);
                        }
                        return false;
                    }
                }
                app.mainBoard[sy - i][x] = newCell;
                return true;
            })
        }
        else {
            actionCallbacks.add(newTs => {
                let ets = ~~((newTs - ts) / 5);
                let i = 0;
                for (i = 0; i <= ets; i++) {
                    let calcY = sy - i;
                    if (sy - i < 0) {
                        return false;
                    }
                    if (baseBoard[calcY][x] != app.emptyCell) {
                        app.mainBoard[calcY][x] = baseBoard[calcY][x] = app.emptyCell;
                        const oldScore = app.status.score;
                        app.status.score = app.status.score + scorePerDot;
                        if (~~(app.status.score / scorePerSpeed) != ~~(oldScore / scorePerSpeed)) {
                            updateGrade(ts);
                        }
                        return false;
                    }
                }
                if (sy - i >= 0) {
                    app.mainBoard[sy - i][x] = newCell;
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
        [keys.KEY_LEFT]: [100, 50, (ts) => doMove(ts, -1)],
        [keys.KEY_RIGHT]: [100, 50, (ts) => doMove(ts, 1)],
        [keys.KEY_DOWN]: [100, 50, (ts) => doGrow(ts)],
        [keys.KEY_UP]: [100, 50, (ts) => doGrow(ts)],
        [keys.KEY_ACTION]: [100, 50, (ts) => doAction(ts)],
        [keys.KEY_EXTEND]: [3000, 1000, (ts) => updateGrade(ts)]
    };

    function randomCell() {
        return app.cells[~~(Math.random() * app.cells.length - 1) + 1];
    }

    const init = (ts, mainApp) => {
        app = mainApp;
        maxLevel = app.mainRows - 5;
        baseBoard = [];
        actionCallbacks.clear();
        for (let r = 0; r < app.mainRows; r++) {
            baseBoard.push(createEmptyRow());
        }
        actionItem = {
            col: ~~(app.mainCols / 2),
            cell: randomCell()
        };
        initLevel(ts);
    }
    const update = (ts) => {
        if (ts - lastTagTime > app.speeds[app.status.speed] * 4) {
            lastTagTime = ts;
            doGrow(ts);
        }
        updateBoard(ts);
        scoreCallbackCreated = false;
        for (let callback of [...actionCallbacks]) {
            if (!callback(ts)) {
                actionCallbacks.delete(callback);
                if (scoreCallbackCreated) {
                    break;
                }
            }
        }
    }
    return { keyMap, init, update };
}

export { game as default };
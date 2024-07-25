import keys from './keyboard.js';

function game(options) {
    const status = {
        score: 0,
        speed: 0,
        level: 0,
        life: 0,
        over: true,
    };

    let maxLevel = 30;
    let scorePerSpeed = 50000;
    let scorePerDot = 10;

    let lastTagTime = 0;

    let actionCallbacks = new Set();

    options = Object.assign({ fk: false }, options ?? {});

    const speeds = { 0: 1000, 1: 900, 2: 800, 3: 700, 4: 600, 5: 500, 6: 450, 7: 400, 8: 350, 9: 300, 10: 250, 11: 200, 12: 150, 13: 100, 14: 50, 15: 25, 16: 10 };


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
        app.mainBoard[0][actionItem.col] = actionItem.cell;

        app.subBoard.forEach(row => row.fill(app.emptyCell));
        for (let i = 0; i < app.subRows && i < status.life; i++) {
            app.subBoard[4 - i - 1][1] = actionItem.cell;
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
        actionCallbacks.clear();
        updateBoard(ts);
    }

    function nextLevel(ts) {
        status.score = (Object.keys(speeds).length * status.level + status.speed + 1) * scorePerSpeed;
        checkScore(ts);
        return true;
    }

    function checkScore(ts) {
        const totalSpeed = ~~(status.score / scorePerSpeed);
        const speedLength = Object.keys(speeds).length;
        const newSpeed = totalSpeed % speedLength;
        if (newSpeed != status.speed) {
            status.speed = totalSpeed % speedLength;
            status.level = ~~(totalSpeed / speedLength);
            if (status.level > maxLevel) {
                status.over = true;
                return false;
            }
            if (status.life < app.subRows) {
                status.life++;
            }
            app.addFlashCallback(ts, (newTs) => initLevel(newTs));
            return true;
        }
        return false;

    }

    function subLife(ts) {
        status.life--;
        if (status.life <= 0) {
            status.over = true;
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
        app.addPauseCallback((newTs) => {
            let ets = ~~((newTs - ts) / 10);
            if (ets > 10) {
                baseBoard.unshift(...baseBoard.splice(calcY, 1));
                baseBoard[0].fill(app.emptyCell);
                status.score += scorePerDot * 10;
                lastTagTime += newTs - ts;
                checkScore(ts);
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
        let sy = 1;
        const newCell = actionItem.cell;
        if (options.fk) {
            if (baseBoard[sy][x] != app.emptyCell) {
                return true;
            }
            actionCallbacks.add(newTs => {
                let ets = ~~((newTs - ts) / 5);
                let i = 0;
                for (i = 0; i < ets; i++) {
                    let calcY = sy + i;
                    if (calcY + 1 >= app.mainRows || baseBoard[calcY + 1][x] != app.emptyCell) {
                        baseBoard[calcY][x] = newCell;
                        if (baseBoard[calcY].every(s => s != app.emptyCell)) {
                            createScorePauseCallback(newTs, [calcY], []);
                        }
                        return false;
                    }
                }
                app.mainBoard[sy + i][x] = newCell;
                return true;
            })
        }
        else {
            actionCallbacks.add(newTs => {
                let ets = ~~((newTs - ts) / 5);
                let i = 0;
                for (i = 0; i < ets; i++) {
                    let calcY = sy + i;
                    if (calcY >= app.mainRows) {
                        return false;
                    }
                    if (baseBoard[calcY][x] != app.emptyCell) {
                        app.mainBoard[calcY][x] = baseBoard[calcY][x] = app.emptyCell;
                        status.score += scorePerDot;
                        checkScore(ts);
                        return false;
                    }
                }
                if (sy + i < app.mainRows) {
                    app.mainBoard[sy + i][x] = newCell;
                }
                return true;
            });
        }
        return true;
    };

    const doGrow = (ts) => {
        baseBoard.push(baseBoard.shift());
        fillRow(app.mainRows - 1);
        if (!baseBoard[0].every(s => s == app.emptyCell)) {
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
        [keys.KEY_EXTEND]: [3000, 1000, (ts) => nextLevel(ts)]
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
        Object.assign(status, { score: 0, level: 0, speed: 0, life: app.subRows, over: false });
        initLevel(ts);
    }
    const update = (ts) => {
        if (!status.over) {
            if (ts - lastTagTime > (speeds[status.speed] * 4 ?? 1) || ts < lastTagTime) {
                lastTagTime = ts;
                doGrow(ts);
            }
        }
        updateBoard(ts);
        for (let callback of [...actionCallbacks]) {
            if (!callback(ts)) {
                actionCallbacks.delete(callback);
                break;
            }
        }
    }
    return { status, keyMap, init, update };
}


export { game as default };
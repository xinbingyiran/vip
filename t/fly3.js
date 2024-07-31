import keys from './keyboard.js';

function game(options) {
    const maxLevel = 30;
    const scorePerSpeed = 2500;
    const scorePer10Distance = 100;
    let speedScore = 0;
    let lastTagTime = 0;
    let actionPercent = 0.2;
    let actionDistance = 100;
    let actionCallbacks = new Set();
    options = Object.assign({ loop: false }, options ?? {});

    let flybody = [[0, 1, 0], [1, 1, 1], [0, 1, 0], [1, 0, 1]];
    let app, backCell, distance, flyItem;
    let items = [];
    let itemDistance = 16;

    function updateBoard(ts) {
        app.mainBoard.forEach((row, r) => row.forEach((cell, c) => row[c] = (c == 0 || c == app.mainCols - 1) && ((distance - r) % 4 + 4) % 4 > 0 ? backCell : app.emptyCell));
        flyItem.body.forEach((row, r) => row.forEach((cell, c) => cell && (app.mainBoard[flyItem.y + r][flyItem.x + c] = flyItem.cell)));
        items.forEach(item => {
            item.body.forEach((row, r) => item.y + r >= 0 && item.y + r < app.mainRows && row.forEach((cell, c) => cell && (app.mainBoard[item.y + r][item.x + c] = item.cell)));
        });
        for (let i = 0; i < app.subRows; i++) {
            app.subBoard[i].fill(app.status.life > (app.subRows - 1 - i) ? backCell : app.emptyCell);
        }
    }

    function initLevel(ts) {
        lastTagTime = ts;
        distance = 0;
        speedScore = 0;
        flyItem = {
            x: 2,
            y: app.mainRows - flybody.length - 2,
            body: flybody,
            cell: backCell,
            action: 0
        }
        items.splice(0, items.length);
        actionCallbacks.clear();
        updateBoard(ts);
    }

    function updateGrade(ts) {
        if (!app.status.updateSpeed(app.speeds.length) && !app.status.updateGrade(maxLevel)) {
            return false;
        }
        app.status.updateLife(app.subRows, true);
        speedScore = 0;
        //app.addFlashCallback(ts, (newTs) => initLevel(newTs));
        return true;
    }

    function subLife(ts) {
        if (!app.status.updateLife(app.subRows, false)) {
            return false;
        }
        app.addFlashCallback(ts, (newTs) => initLevel(newTs));
        return true;
    }

    function checkHit(ts) {
        let index = items.findIndex(item => !item.broke && item.body.some((row, r) => row.some((cell, c) => cell && flyItem.body.some((frow, fr) => frow.some((fcell, fc) => fcell && item.x + c == flyItem.x + fc && item.y + r == flyItem.y + fr)))));
        if (index >= 0) {
            const fitem = items[index];
            fitem.broke = true;
            if (fitem.action) {
                items.splice(index, 1);
                fitem.action(ts);
            }
            else {
                subLife(ts);
            }
        }
        return index >= 0;
    }

    function doStep(ts) {
        if (distance % itemDistance == 0) {
            let body = [];
            let sc = ~~(Math.random() * (app.mainCols - 5)) + 1;
            for (let r = 0; r < 3; r++) {
                body.push(app.mainBoard[0].map((cell, c) => c < sc || c >= sc + 3 ? 1 : 0));
            }
            items.push({
                x: 0,
                y: -body.length,
                body: body,
                cell: randomCell()
            });
        }
        else if (distance % itemDistance == ~~(itemDistance / 2)) {
            if (Math.random() < actionPercent) {
                const body = [[1], [1]];
                items.push({
                    x: Math.random() < 0.5 ? 2 : app.mainCols - 2 - body[0].length,
                    y: -body.length,
                    body: body,
                    cell: randomCell(),
                    action: (ts) => {
                        flyItem.action = actionDistance
                    }
                });
            }
        }
        if (items.length && items[0].y >= app.mainRows) {
            items.shift();
        }
        items.forEach(item => item.y++);
        distance++;
        flyItem.action > 0 && flyItem.action--;
        if (checkHit(ts)) {
            return false;
        }
        if (distance % 10 == 0) {
            app.status.score += scorePer10Distance;
            speedScore += scorePer10Distance;
            if (speedScore > scorePerSpeed) {
                updateGrade(ts);
            }
        }
        return true;
    }

    function doMove(ts, x) {
        if (flyItem.x + x < 1 || flyItem.x + x > app.mainCols - 1 - flyItem.body[0].length) {
            return false;
        }
        flyItem.x += x;
        return !checkHit(ts);
    }

    function doAction(ts) {
        if (flyItem.action > 0) {
            const x = flyItem.x + 1;
            const sy = flyItem.y;
            const newCell = flyItem.cell;
            actionCallbacks.add(newTs => {
                let ets = ~~((newTs - ts) / 5);
                let i = 0;
                for (i = 0; i <= ets; i++) {
                    let calcY = sy - i;
                    if (sy - i < 0) {
                        return false;
                    }
                    if (items.some(item => {
                        if (calcY < item.y || calcY > item.y + item.body.length - 1 || x < item.x || x > item.x + item.body[0].length - 1) {
                            return false;
                        }
                        if (item.body[calcY - item.y][x - item.x]) {
                            item.body.forEach(row => (row[x - item.x] = 0, (x - item.x - 1 >= 0 && (row[x - item.x - 1] = 0)), (x - item.x + 1 < row.length && (row[x - item.x + 1] = 0))));
                            return true;
                        }
                        return false;
                    })) {
                        return false;
                    }
                }
                if (sy - i >= 0) {
                    app.mainBoard[sy - i][x] = newCell;
                }
                return true;
            });
            return true;
        }
        return false;
    }

    const keyMap = {
        [keys.KEY_LEFT]: [100, 50, (ts) => doMove(ts, -1)],
        [keys.KEY_RIGHT]: [100, 50, (ts) => doMove(ts, 1)],
        [keys.KEY_DOWN]: [100, 50, (ts) => doStep(ts)],
        [keys.KEY_UP]: [100, 50, (ts) => doStep(ts)],
        [keys.KEY_ACTION]: [100, 50, (ts) => doAction(ts)],
        [keys.KEY_EXTEND]: [3000, 1000, (ts) => updateGrade(ts)]
    };

    function randomCell() {
        return app.cells[~~(Math.random() * app.cells.length - 1) + 1];
    }

    const init = (ts, mainApp) => {
        app = mainApp;
        backCell = randomCell();
        initLevel(ts);
    }
    const update = (ts) => {
        if (!app.status.over) {
            if (ts - lastTagTime > app.speeds[app.status.speed] / 5) {
                lastTagTime = ts;
                doStep(ts);
            }
        }
        updateBoard(ts);
        for (let callback of [...actionCallbacks]) {
            if (!callback(ts)) {
                actionCallbacks.delete(callback);
            }
        }
    }
    return { keyMap, init, update };
}

export { game as default };
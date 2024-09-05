function game(app, { } = {}) {
    const maxLevel = 30;
    const scorePerSpeed = 2500;
    const scorePer10Distance = 100;
    let speedScore = 0;
    let lastTagTime = 0;
    let actionPercent = 0.2;
    let actionDistance = 100;
    let actionCallbacks;

    let flybody = [[0, 1, 0], [1, 1, 1], [0, 1, 0], [1, 0, 1]];
    let app, backCell, distance, flyItem;
    let otherItems;
    let overItems;
    let itemDistance = 16;

    function updateBoard(ts) {
        app.mainBoard.forEach((row, r) => row.forEach((cell, c) => row[c] = (c == 0 || c == app.mainCols - 1) && ((distance - r) % 4 + 4) % 4 > 0 ? backCell : app.emptyCell));
        flyItem.body.forEach((row, r) => row.forEach((cell, c) => cell && (app.mainBoard[flyItem.y + r][flyItem.x + c] = flyItem.cell)));
        otherItems.forEach(item => {
            item.body.forEach((row, r) => item.y + r >= 0 && item.y + r < app.mainRows && row.forEach((cell, c) => cell && (app.mainBoard[item.y + r][item.x + c] = item.cell)));
        });
        overItems.forEach(item => {
            app.mainBoard[item.y][item.x] = item.cell;
        });
        for (let i = 0; i < app.subRows; i++) {
            app.subBoard[i].fill(status.life > (app.subRows - 1 - i) ? backCell : app.emptyCell);
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
        otherItems = new Set();
        actionCallbacks = new Set();
        overItems = new Set();
        updateBoard(ts);
    }

    function updateGrade(ts) {
        if (!status.updateSpeed(app.speeds.length) && !status.updateGrade(maxLevel)) {
            return false;
        }
        status.updateLife(app.subRows, true);
        speedScore = 0;
        return true;
    }

    function subLife(ts) {
        if (!status.updateLife(app.subRows, false)) {
            return false;
        }
        app.addFlashCallback(()=>initLevel(ts));
        return true;
    }

    function checkHit(ts) {
        for (let item of otherItems) {
            if (item.broke) {
                continue;
            }
            if (!item.body.some((row, r) => row.some((cell, c) => cell && flyItem.body.some((frow, fr) => frow.some((fcell, fc) => fcell && item.x + c == flyItem.x + fc && item.y + r == flyItem.y + fr))))) {
                continue;
            }
            if (item.action) {
                otherItems.delete(item);
                item.action(ts);
            }
            else {
                subLife(ts);
            }
            return true;
        }
        return false;
    }

    function doStep(ts) {
        distance++;
        flyItem.action > 0 && flyItem.action--;
        if (distance % itemDistance == 1) {
            let body = [];
            let sc = ~~(Math.random() * (app.mainCols - 5)) + 1;
            for (let r = 0; r < 3; r++) {
                body.push(app.mainBoard[0].map((cell, c) => c < sc || c >= sc + 3 ? 1 : 0));
            }
            otherItems.add({
                x: 0,
                y: -body.length,
                body: body,
                cell: randomCell()
            });
        }
        else if (distance % itemDistance == ~~(itemDistance / 2) + 1) {
            if (Math.random() < actionPercent) {
                const body = [[1], [1]];
                otherItems.add({
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
        for (let item of otherItems) {
            item.y++;
            if (item.y >= app.mainRows) {
                otherItems.delete(item);
            }
        }
        if (checkHit(ts)) {
            return false;
        }
        if (distance % 10 == 0) {
            status.score += scorePer10Distance;
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
                    let calcY = sy - i;
                    if (sy - i < 0) {
                        overItems.delete(overItem);
                        return false;
                    }
                    for (let item of otherItems) {
                        if (calcY < item.y || calcY > item.y + item.body.length - 1 || x < item.x || x > item.x + item.body[0].length - 1) {
                            continue;
                        }
                        if (!item.body[calcY - item.y][x - item.x]) {
                            continue;
                        }
                        item.body.forEach(row => (row[x - item.x] = 0, (x - item.x - 1 >= 0 && (row[x - item.x - 1] = 0)), (x - item.x + 1 < row.length && (row[x - item.x + 1] = 0))));
                        overItems.delete(overItem);
                        return false;
                    };
                }
                if (sy - i >= 0) {
                    overItem.y = sy - i;
                }
                return true;
            });
            return true;
        }
        return false;
    }

    const keyMap = {
        [app.keys.KEY_LEFT]: [100, 50, (ts) => doMove(ts, -1)],
        [app.keys.KEY_RIGHT]: [100, 50, (ts) => doMove(ts, 1)],
        [app.keys.KEY_DOWN]: [100, 50, (ts) => doStep(ts)],
        [app.keys.KEY_UP]: [100, 50, (ts) => doStep(ts)],
        [app.keys.KEY_ACTION]: [100, 50, (ts) => doAction(ts)],
        [app.keys.KEY_EXTEND]: [3000, 1000, (ts) => updateGrade(ts)]
    };

    function randomCell() {
        return app.cells[~~(Math.random() * app.cells.length)];
    }

    const init = (ts, mainStatus) => {
        status = mainStatus;
        backCell = randomCell();
        initLevel(ts);
    }
    const update = (ts) => {
        if (!status.over) {
            if (ts - lastTagTime > app.speeds[status.speed] / 5) {
                lastTagTime = ts;
                doStep(ts);
            }
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
function game(app, { } = {}) {
    const maxLevel = 30;
    const scorePerSpeed = 2500;
    const scorePer10Distance = 100;
    let speedScore = 0;
    let lastTagTime = 0;

    let flybody = [[0, 1, 0], [1, 1, 1], [0, 1, 0], [1, 0, 1]];
    let itemBody = [...flybody].reverse();
    let status, backCell, distance, flyItem;
    let otherItems;
    let itemDistance = 12;

    function updateBoard(ts) {
        app.mainBoard.forEach((row, r) => row.forEach((cell, c) => row[c] = (c == 0 || c == app.mainCols - 1) && ((distance - r) % 4 + 4) % 4 > 0 ? backCell : app.emptyCell));
        flyItem.body.forEach((row, r) => row.forEach((cell, c) => cell && (app.mainBoard[flyItem.y + r][flyItem.x + c] = flyItem.cell)));
        otherItems.forEach(item => {
            item.body.forEach((row, r) => item.y + r >= 0 && item.y + r < app.mainRows && row.forEach((cell, c) => cell && (app.mainBoard[item.y + r][item.x + c] = item.cell)));
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
            cell: backCell
        }
        otherItems = new Set();
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
        app.addFlashCallback(() => initLevel(ts));
        return true;
    }

    function checkHit(ts) {
        for (let item of otherItems) {
            if (item.broke || item.x != flyItem.x) {
                continue;
            }
            if (item.y > flyItem.y && item.y - flyItem.y >= flyItem.body.length) {
                continue;
            }
            if (item.y < flyItem.y && flyItem.y - item.y >= item.body.length) {
                continue;
            }
            item.broke = true;
            subLife(ts);
            return true;
        }
        return false;
    }

    function doStep(ts) {
        distance++;
        for (let item of otherItems) {
            item.y++;
            if (item.y >= app.mainRows) {
                otherItems.delete(item);
            }
        }
        if (checkHit(ts)) {
            return false;
        }
        if (distance % itemDistance == 1) {
            otherItems.add({
                x: ~~(Math.random() * 2) ? 2 : 5,
                y: -itemBody.length,
                body: itemBody,
                cell: randomCell()
            });
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
        flyItem.x = x;
        checkHit(ts);
        return false;
    }

    const keyMap = {
        [app.keys.KEY_LEFT]: [100, 50, (ts) => doMove(ts, 2)],
        [app.keys.KEY_RIGHT]: [100, 50, (ts) => doMove(ts, 5)],
        [app.keys.KEY_DOWN]: [100, 50, (ts) => doStep(ts)],
        [app.keys.KEY_UP]: [100, 50, (ts) => doStep(ts)],
        [app.keys.KEY_ACTION]: [100, 50, (ts) => doStep(ts)],
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
        updateBoard(ts);
    }
    return { keyMap, init, update };
}

export { game as default };
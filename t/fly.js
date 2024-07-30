import keys from './keyboard.js';

function game(options) {
    const maxLevel = 30;
    const scorePerSpeed = 2500;
    const scorePer10Distance = 100;
    let speedScore = 0;
    let lastTagTime = 0;
    options = Object.assign({ loop: false }, options ?? {});

    let flybody = [[0, 1, 0], [1, 1, 1], [0, 1, 0], [1, 0, 1]];
    let itemBody = [...flybody].reverse();
    let app, backCell, distance, flyItem;
    let items = [];
    let itemDistance = 12;

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
            cell: backCell
        }
        items.splice(0, items.length);
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
        let index = items.findIndex(item => !item.broke && item.x == flyItem.x && ((item.y >= flyItem.y && item.y - flyItem.y < flyItem.body.length) || (flyItem.y >= item.y && flyItem.y - item.y < item.body.length)));
        if (index >= 0) {
            items[index].broke = true;
            subLife(ts);
        }
        return index >= 0;
    }

    function doStep(ts) {
        if (distance % itemDistance == 0) {
            items.push({
                x: ~~(Math.random() * 2) ? 2 : 5,
                y: -itemBody.length,
                body: itemBody,
                cell: randomCell()
            });
        }
        if (items.length && items[0].y >= app.mainRows) {
            items.shift();
        }
        items.forEach(item => item.y++);
        distance++;
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
        flyItem.x = x;
        checkHit(ts);
        return false;
    }

    const keyMap = {
        [keys.KEY_LEFT]: [100, 50, (ts) => doMove(ts, 2)],
        [keys.KEY_RIGHT]: [100, 50, (ts) => doMove(ts, 5)],
        [keys.KEY_DOWN]: [100, 50, (ts) => doStep(ts)],
        [keys.KEY_UP]: [100, 50, (ts) => doStep(ts)],
        [keys.KEY_ACTION]: [100, 50, (ts) => doStep(ts)],
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
    }
    return { keyMap, init, update };
}

export { game as default };
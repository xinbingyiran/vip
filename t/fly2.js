import keys from './keyboard.js';

function game(options) {
    const maxLevel = 30;
    const scorePerSpeed = 2500;
    const scorePer10Distance = 100;
    let speedScore = 0;
    let lastTagTime = 0;
    options = Object.assign({ loop: false }, options ?? {});

    let flybody = [[0, 1, 0], [1, 1, 1], [0, 1, 0], [1, 0, 1]];
    let app, backCell, distance, flyItem;
    let baseBoard = [];
    let itemBodys = [];
    let itemIndex, itemDistance = 12;

    function updateBoard(ts) {
        app.mainBoard.forEach((row, r) => row.forEach((cell, c) => app.mainBoard[r][c] = baseBoard[r][c]));
        flyItem.body.forEach((row, r) => row.forEach((cell, c) => cell && (app.mainBoard[flyItem.y + r][flyItem.x + c] = flyItem.cell)));
        for (let i = 0; i < app.subRows; i++) {
            app.subBoard[i].fill(app.status.life > (app.subRows - 1 - i) ? backCell : app.emptyCell);
        }
    }

    function initLevel(ts) {
        lastTagTime = ts;
        distance = 0;
        speedScore = 0;
        baseBoard = app.mainBoard.map(row => row.map(cell => app.emptyCell));
        itemIndex = ~~(Math.random() * itemBodys.length);
        flyItem = {
            x: itemIndex + 2,
            y: app.mainRows - flybody.length - 2,
            body: flybody,
            cell: backCell
        }
        for (let y = 0; y < app.length; y++) {

        }
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

    function doStep(ts) {
        if (distance % itemDistance == 0) {
            itemIndex = (itemIndex - 1 + ~~(Math.random() * 3));
            if (itemIndex < 0) {
                itemIndex = 0;
            }
            if (itemIndex >= itemBodys.length) {
                itemIndex = itemBodys.length - 1;
            }
        }
        baseBoard.pop();
        baseBoard.unshift(itemBodys[itemIndex]);
        distance++;
        if (flyItem.body.some((row, r) => row.some((cell, c) => cell && baseBoard[flyItem.y + r][flyItem.x + c] != app.emptyCell))) {
            subLife(ts);
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
        let newx = flyItem.x + x;
        let hit = flyItem.body.some((row, r) => row.some((cell, c) => cell && baseBoard[flyItem.y + r][newx + c] != app.emptyCell));
        if (hit) {
            return false;
        }
        flyItem.x = newx;
        return true;
    }

    const keyMap = {
        [keys.KEY_LEFT]: [100, 50, (ts) => doMove(ts, -1)],
        [keys.KEY_RIGHT]: [100, 50, (ts) => doMove(ts, 1)],
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
        itemBodys = [];
        backCell = randomCell();
        let bodyCell = randomCell();
        for (let i = 1; i < app.mainCols - 4; i++) {
            itemBodys.push(app.mainBoard[0].map((cell, c) => (c < i || c > (i + 3)) ? bodyCell : app.emptyCell));
        }
        initLevel(ts);
    }
    const update = (ts) => {
        if (!app.status.over) {
            if (ts - lastTagTime > app.speeds[app.status.speed] / 10) {
                lastTagTime = ts;
                doStep(ts);
            }
        }
        updateBoard(ts);
    }
    return { keyMap, init, update };
}

export { game as default };
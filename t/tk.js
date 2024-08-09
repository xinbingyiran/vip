import keys from './keyboard.js';

function game({ tankCount = 25, bossLife = 1 } = {}) {

    let maxLevel = 30;
    let scorePerSpeed = 100 * (tankCount > 0 ? tankCount : 25);
    let scorePerTank = 100;
    let scorePerBoss = 1000;
    let levelScore = 0;

    let lastTagTime = 0;
    let maxTankSize = 5;
    let createTankTick = 0;
    let createTankTicks = 2;
    let shotPercent = 0.25;
    let wayPercent = 0.2;
    let maxFlySize = 2;

    const action_Up = 0, action_Right = 1, action_Down = 2, action_Left = 3;
    let steps = [[0, -1], [1, 0], [0, 1], [-1, 0]];
    let testBody = [[1, 1, 1], [1, 1, 1], [1, 1, 1]];
    let tankBodys = [
        [[0, 1, 0], [1, 1, 1], [1, 0, 1]],
        [[1, 1, 0], [0, 1, 1], [1, 1, 0]],
        [[1, 0, 1], [1, 1, 1], [0, 1, 0]],
        [[0, 1, 1], [1, 1, 0], [0, 1, 1]],
    ];
    let bossBody = [
        [1, 1, 0, 0, 0, 1, 1],
        [0, 1, 0, 0, 0, 1, 0],
        [1, 0, 1, 1, 1, 0, 1],
        [0, 1, 1, 0, 1, 1, 0],
        [1, 0, 1, 1, 1, 0, 1],
        [0, 1, 0, 1, 0, 1, 0],
        [1, 1, 0, 1, 0, 1, 1],
        [0, 0, 0, 1, 0, 0, 0]
    ];
    let actionCallbacks;
    let tanks;
    let overItems;

    let tankPosions;

    let app, tankItem, bossItem, tankCell;
    function updateBoard(ts) {
        for (let r = 0; r < app.mainRows; r++) {
            for (let c = 0; c < app.mainCols; c++) {
                app.mainBoard[r][c] = app.emptyCell;
            }
        }

        tanks.forEach(tank => {
            tank.body.forEach((row, r) => {
                row.forEach((cell, c) => {
                    if (!cell) {
                        return;
                    }
                    let row = tank.y + r;
                    let col = tank.x + c;
                    if (row < 0 || row >= app.mainRows || col < 0 || col >= app.mainCols) {
                        return;
                    }
                    app.mainBoard[row][col] = (tank == tankItem && r == 1 && c == 1 && (~~ts % 300) > 150) ? app.emptyCell : tank.cell;
                })
            });
        });

        overItems.forEach(item => {
            app.mainBoard[item.y][item.x] = item.cell;
        });

        for (let i = 0; i < app.subRows; i++) {
            app.subBoard[i].fill(app.status.life > (app.subRows - 1 - i) ? tankCell : app.emptyCell);
        }
    }


    function initLevel(ts) {
        lastTagTime = ts;
        actionCallbacks = new Set();
        tanks = new Set();
        overItems = new Set();
        createTankTick = 0;
        levelScore = 0;
        tankItem = {
            x: ~~((app.mainCols - testBody[0].length) / 2),
            y: ~~((app.mainRows - testBody.length) / 2),
            actions: [action_Left, action_Right, action_Up, action_Down],
            action: action_Up,
            cell: tankCell,
            dead: false,
            body: tankBodys[action_Up],
            withShot: withShotTank,
            doStep: (ts, tank) => true,
            getShotAction: tank => tank.action,
            refreshBody: tank => tank.body = tankBodys[tank.action]
        };
        tanks.add(tankItem);
        bossItem = undefined;
        updateBoard(ts);
    }

    function updateGrade(ts) {
        levelScore = 0;
        if (!app.status.updateSpeed(app.speeds.length) && !app.status.updateGrade(maxLevel)) {
            return false;
        }
        app.status.updateLife(app.subRows, true);
        //app.addFlashCallback(ts, (newTs) => initLevel(newTs));
        //initLevel(ts);
        return true;
    }

    function subLife(ts) {
        if (!app.status.updateLife(app.subRows, false)) {
            return false;
        }
        app.addFlashCallback(ts, (newTs) => initLevel(newTs));
        return true;
    }

    const calcTankBody = tank => {
        let tankBody = {};
        tank.body.forEach((row, r) => row.forEach((cell, c) => {
            if (cell) {
                tankBody[tank.x + c + (tank.y + r) * app.mainCols] = cell;
            }
        }));
        return tankBody;
    }

    const overrideOtherBody = (tank) => {
        let tankBody = calcTankBody(tank);
        tanks.forEach(item => {
            if (item == tank) {
                return;
            }
            if (tank.x - item.x >= item.body[0].length || item.x - tank.x >= tank.body[0].length || tank.y - item.y >= item.body.length || item.y - tank.y >= tank.body.length) {
                return;
            }
            item.body = item.body.map((row, r) => row.map((cell, c) => tankBody[item.x + c + (item.y + r) * app.mainCols] ? 0 : cell));
        });
    };

    const doMove = (ts, actionType) => {
        let index = tankItem.actions.findIndex(s => s == actionType);
        if (index < 0) {
            return false;
        }
        if (actionType != tankItem.action) {
            tankItem.action = actionType;
            tankItem.refreshBody(tankItem);
            overrideOtherBody(tankItem);
            return true;
        }
        return tryMove(tankItem);
    };

    const findTank = (matrix, x, y, ftank) => {
        for (let tank of tanks) {
            if (ftank && !ftank(tank)) {
                continue;
            }
            const tankBody = calcTankBody(tank);
            if (matrix.some((row, r) => row.some((cell, c) => cell && tankBody[(y + r) * app.mainCols + x + c]))) {
                return tank;
            }
        }
        return undefined;
    }

    const tryCreateFlyItem = (ts, tank) => {
        let flyCount = 0;
        overItems.forEach(item => item.tank == tank && flyCount++);
        if (flyCount < maxFlySize) {
            actionCallbacks.add(createShotAction(ts, tank));
            return true;
        }
        return false;
    }

    const createShotAction = (ts, tank) => {
        let step = steps[tank.getShotAction(tank)];
        let x = step[0];
        let y = step[1];
        let mx = ~~((tank.body[0].length - 1) / 2);
        let my = ~~((tank.body.length - 1) / 2);
        let sx = tank.x + mx + x * mx;
        let sy = tank.y + my + y * my;
        let lastEts = 0;
        let newCell = tank.cell;
        let overItem = {
            tank: tank,
            x: sx,
            y: sy,
            end: false,
            cell: newCell,
            overTank: undefined
        }
        overItems.add(overItem);
        return newTs => {
            if (!overItems.has(overItem)) {
                return false;
            }
            let ets = ~~((newTs - ts) / 50);
            let i = lastEts;
            for (; i <= ets; i++) {
                let nx = sx + i * x;
                let ny = sy + i * y;
                if (nx < 0 || nx >= app.mainCols || ny < 0 || ny >= app.mainRows) {
                    overItems.delete(overItem);
                    return false;
                }
                for (let s of overItems) {
                    if (s.x == nx && s.y == ny && s.tank != tank) {
                        overItem.overTank = s.tank;
                        overItems.delete(s);
                        break;
                    }
                }

                let findItem = undefined;
                let shotItem = { x: nx, y: ny, tank: tank, overTank: overItem.overTank };
                for (let t of tanks) {
                    if (t != tank && t.withShot(newTs, t, shotItem)) {
                        findItem = t;
                        break;
                    }

                }
                if (!findItem) {
                    continue;
                }
                if (!findItem.dead) {
                }
                else if (findItem == tankItem) {
                    subLife(newTs);
                }
                else if (findItem == bossItem) {
                    tanks.delete(findItem);
                    app.status.score += scorePerBoss;
                    bossKilled(newTs);
                }
                else {
                    tanks.delete(findItem);
                    app.status.score += scorePerTank;
                    levelScore += scorePerTank;
                    if (levelScore >= scorePerSpeed) {
                        bossCome(newTs);
                    }
                }
                overItems.delete(overItem);
                return false;
            }
            overItem.x = sx + x * ets;
            overItem.y = sy + y * ets;
            lastEts = ets;
            return true;
        }
    }
    const doShot = (ts) => {
        return tryCreateFlyItem(ts, tankItem);
    };

    const withShotTank = (ts, tank, shotItem) => {
        const hasHist = tank.body.some((row, r) => row.some((cell, c) => cell && tank.x + c == shotItem.x && tank.y + r == shotItem.y));
        tank.dead = shotItem.tank != tankItem;
        return hasHist;
    }
    const withShotCommon = (ts, tank, shotItem) => {
        const hasHist = tank.body.some((row, r) => row.some((cell, c) => cell && tank.x + c == shotItem.x && tank.y + r == shotItem.y));
        tank.dead = shotItem.tank == tankItem;
        return hasHist;
    }

    const createShotBossPauseCallback = (ts, tank) => {
        app.addPauseCallback((newTs) => {
            let ets = ~~((newTs - ts) / 50);
            if (ets > 10) {
                lastTagTime += newTs - ts;
                return false;
            }
            else {
                tank.body.forEach((row, r) => {
                    row.forEach((cell, c) => {
                        if (!cell) {
                            return;
                        }
                        let row = tank.y + r;
                        let col = tank.x + c;
                        if (row < 0 || row >= app.mainRows || col < 0 || col >= app.mainCols) {
                            return;
                        }
                        app.mainBoard[row][col] = (ets % 2) ? app.emptyCell : tank.cell;
                    })
                });
                return true;
            }
        });
    }

    const withShotBoss = (ts, tank, shotItem) => {
        if (tank.x - shotItem.x >= tank.body[0].length || shotItem.x - tank.x >= tank.body[0].length || tank.y - shotItem.y >= tank.body.length || shotItem.y - tank.y >= tank.body.length) {
            return false;
        }
        let body = tank.body.map(row => [...row]);
        let hitPosition = undefined;
        for (let r = 0; r < body.length; r++) {
            const row = body[r];
            for (let c = 0; c < row.length; c++) {
                const cell = row[c];
                if (cell && tank.x + c == shotItem.x && tank.y + r == shotItem.y) {
                    body[r][c] = 0;
                    hitPosition = [c, r];
                    break;
                }
            }
            if (hitPosition) {
                break;
            }
        }
        if (hitPosition) {
            tank.body = body;
            if (shotItem.tank == tankItem && shotItem.overTank == tank && hitPosition[0] == ~~((bossBody[0].length - 1) / 2) && hitPosition[1] == bossBody.length - 1) {
                tank.life--;
                tank.dead = tank.life <= 0;
                createShotBossPauseCallback(ts, tank);
            }
        }
        return !!hitPosition;
    }


    const tryCreateTank = (ts) => {
        if (tanks.size >= maxTankSize) {
            return undefined;
        }
        let positions = [...tankPosions];
        while (positions.length) {
            let pi = ~~(positions.length * Math.random());
            let position = positions.splice(pi, 1)[0];
            let actionTypes = [...position.ac];
            while (actionTypes.length) {
                let ai = ~~(actionTypes.length * Math.random());
                let actionType = actionTypes.splice(ai, 1)[0];
                if (!findTank(testBody, position.x, position.y)) {
                    let newTank = {
                        x: position.x,
                        y: position.y,
                        actions: [action_Left, action_Right, action_Up, action_Down],
                        action: actionType,
                        body: tankBodys[actionType],
                        cell: randomCell(),
                        dead: false,
                        withShot: withShotCommon,
                        doStep: doCommonAction,
                        getShotAction: tank => tank.action,
                        refreshBody: tank => tank.body = tankBodys[tank.action]
                    };
                    tanks.add(newTank);
                    return newTank;
                }
            }
        }
        return undefined;
    }
    const clearStage = (ts) => {
        tanks = new Set();
        tanks.add(tankItem);
        actionCallbacks = new Set();
        overItems = new Set();
    }

    const createBossComeCallback = (ts) => {
        let sx = ~~((app.mainCols - bossBody[0].length) / 2);
        let tsy = tankItem.y;
        let tey = app.mainRows - testBody.length;
        let bsy = -bossBody.length;
        let bey = 1;
        let bossCell = randomCell();
        bossItem = {
            x: ~~((app.mainCols - bossBody[0].length) / 2),
            y: bsy,
            actions: [action_Left, action_Right],
            action: action_Right,
            cell: bossCell,
            dead: false,
            body: bossBody,
            life: bossLife,
            withShot: withShotBoss,
            doStep: createBossStep(),
            getShotAction: tank => action_Down,
            refreshBody: tank => tank.body = bossBody
        }
        tanks.add(bossItem);
        return newTs => {
            let ets = ~~((newTs - ts) / 75);
            if (tsy + ets >= tey) {
                tankItem.y = tey;
                tankItem.action = action_Up;
            }
            else {
                tankItem.y = tsy + ets;
                tankItem.action = action_Down;
            }
            tankItem.refreshBody(tankItem);

            let by = bsy + ets + tsy - tey;
            if (by >= bey) {
                bossItem.y = bey;
                tankItem.actions = [action_Left, action_Right];
                tankItem.action = action_Up;
                tankItem.refreshBody = tank => tank.body = tankBodys[action_Up];
                tankItem.getShotAction = tank => action_Up;
                tankItem.refreshBody(tankItem);
                lastTagTime = newTs;
                return false;
            }
            else if (by >= bsy) {
                bossItem.y = by;
            }
            updateBoard(newTs);
            return true;
        }
    }

    const createBossKilledCallback = (ts) => {
        let sx = tankItem.x;
        let sy = tankItem.y;
        let tx = ~~((app.mainCols - tankItem.body[0].length) / 2);
        let ty = ~~((app.mainRows - tankItem.body.length) / 2);
        bossItem = undefined;
        tankItem.actions = [action_Left, action_Right, action_Up, action_Down];
        tankItem.refreshBody = tank => tank.body = tankBodys[tank.action];
        return newTs => {
            let ets = ~~((newTs - ts) / 75);
            let es = Math.abs(sx - tx);
            if (ets < es) {
                if (sx < tx) {
                    tankItem.x = sx + ets;
                    tankItem.action = action_Right;
                }
                else {
                    tankItem.x = sx - ets;
                    tankItem.action = action_Left;
                }
            }
            else if (ets < es + (sy - ty)) {
                tankItem.x = tx;
                tankItem.y = sy - (ets - es);
                tankItem.action = action_Up;
            }
            else {
                tankItem.x = tx;
                tankItem.y = ty;
                tankItem.action = action_Up;
                tankItem.getShotAction = tank => tank.action;
                lastTagTime = newTs;
                updateGrade(newTs);
                return false;
            }
            tankItem.refreshBody(tankItem);
            updateBoard(newTs);
            return true;
        }
    }

    const bossCome = (ts) => {
        clearStage(ts);
        app.addPauseCallback(createBossComeCallback(ts));
    }

    const bossKilled = (ts) => {
        clearStage(ts);
        app.addPauseCallback(createBossKilledCallback(ts));
    }

    const tryMove = item => {
        let actionType = item.action, step = steps[actionType];
        const newx = item.x + step[0];
        const newy = item.y + step[1];
        if (newx < 0 || newx > app.mainCols - item.body[0].length || newy < 0 || newy > app.mainRows - item.body.length) {
            return false;
        }
        if (findTank(testBody, newx, newy, t => t != item)) {
            return false;
        }
        item.x = newx;
        item.y = newy;
        item.refreshBody(item);
        return true;
    }

    const changeAction = item => {
        const actionTypes = [...item.actions];
        if (item.action == action_Left || item.x <= 0) {
            let removeIndex = actionTypes.findIndex(s => s == action_Left);
            removeIndex >= 0 && actionTypes.splice(removeIndex, 1);
        }
        if (item.action == action_Up || item.y <= 0) {
            let removeIndex = actionTypes.findIndex(s => s == action_Up);
            removeIndex >= 0 && actionTypes.splice(removeIndex, 1);
        }
        if (item.action == action_Right || item.x >= app.mainCols - item.body[0].length) {
            let removeIndex = actionTypes.findIndex(s => s == action_Right);
            removeIndex >= 0 && actionTypes.splice(removeIndex, 1);
        }
        if (item.action == action_Down || item.y >= app.mainCols - item.body.length) {
            let removeIndex = actionTypes.findIndex(s => s == action_Down);
            removeIndex >= 0 && actionTypes.splice(removeIndex, 1);
        }
        item.action = actionTypes[~~(Math.random() * actionTypes.length)];
        item.refreshBody(item);
    }
    const doCommonAction = (ts, item) => {
        let findWay = false;
        if (Math.random() < wayPercent) {
            changeAction(item);
            findWay = true;
        }
        !findWay && !tryMove(item) && changeAction(item);
        if (Math.random() < shotPercent) {
            tryCreateFlyItem(ts, item);
        }
    }

    const createBossStep = () => {
        let actionIndex = 1;
        let shotIndex = 0;
        return (ts, item) => {
            if (!tryMove(item)) {
                actionIndex = (actionIndex + 1) % item.actions.length;
                item.action = item.actions[actionIndex];
                tryMove(item);
            }
            if (shotIndex++ % 3 <= 1) {
                tryCreateFlyItem(ts, item);
            }
        }
    }


    const doStep = (ts) => {
        let newTank = undefined;
        if (!bossItem) {
            createTankTick++;
            if (createTankTick >= createTankTicks) {
                newTank = tryCreateTank(ts);
                createTankTick = newTank ? 0 : createTankTicks;
            }
        }
        tanks.forEach(item => {
            if (item == tankItem) {
                return;
            }
            if (item != newTank) {
                item.doStep(ts, item);
                overrideOtherBody(item);
            }
        });
        return true;
    };

    const keyMap = {
        [keys.KEY_LEFT]: [100, 100, (ts) => doMove(ts, action_Left)],
        [keys.KEY_RIGHT]: [100, 100, (ts) => doMove(ts, action_Right)],
        [keys.KEY_DOWN]: [100, 100, (ts) => doMove(ts, action_Down)],
        [keys.KEY_UP]: [100, 100, (ts) => doMove(ts, action_Up)],
        [keys.KEY_ACTION]: [300, 300, (ts) => doShot(ts)],
        [keys.KEY_EXTEND]: [3000, 1000, (ts) => updateGrade(ts)]
    };

    function randomCell() {
        return app.cells[~~(Math.random() * app.cells.length - 1) + 1];
    }

    const init = (ts, mainApp) => {
        app = mainApp;
        tankCell = randomCell();
        tankPosions = [
            { x: 0, y: 0, ac: [action_Right, action_Down] },
            { x: app.mainCols - testBody[0].length, y: 0, ac: [action_Left, action_Down] },
            { x: app.mainCols - testBody[0].length, y: app.mainRows - testBody.length, ac: [action_Left, action_Up] },
            { x: 0, y: app.mainRows - testBody.length, ac: [action_Right, action_Up] }
        ];
        initLevel(ts);
    }
    const update = (ts) => {
        if (ts - lastTagTime > app.speeds[app.status.speed]) {
            lastTagTime = ts;
            doStep(ts);
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
import keys from './keyboard.js';

function game(options) {

    let maxLevel = 30;
    let scorePerSpeed = 2500;
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
    let actionCallbacks = [];
    let tanks = [];
    let flyItems = [];

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
                    cell && (app.mainBoard[tank.y + r][tank.x + c] = (tank == tankItem && r == 1 && c == 1 && (~~ts % 300) > 150) ? app.emptyCell : tank.cell);
                })
            });
        });

        for (let i = 0; i < app.subRows; i++) {
            app.subBoard[i].fill(app.status.life > (app.subRows - 1 - i) ? tankCell : app.emptyCell);
        }
    }


    function initLevel(ts) {
        lastTagTime = ts;
        actionCallbacks.splice(0, actionCallbacks.length);
        tanks.splice(0, tanks.length);
        flyItems.forEach(s => s.end = true);
        flyItems.splice(0, flyItems.length);
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
            withShot: isCommonShotPoint,
            doStep: (ts, tank) => true,
            getShotAction: tank => tank.action,
            refreshBody: tank => tank.body = tankBodys[tank.action]
        };
        tanks.push(tankItem);
        bossItem = undefined;
        updateBoard(ts);
    }

    function updateGrade(ts) {
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
            if (tank.x - item.x >= testBody[0].length || item.x - tank.x >= testBody[0].length || tank.y - item.y >= testBody.length || item.y - tank.y >= testBody.length) {
                return;
            }
            let body = item.body.map(row => [...row]);
            let hasChange = false;
            body.forEach((row, r) => row.forEach((cell, c) => {
                if (cell && tankBody[item.x + c + (item.y + r) * app.mainCols]) {
                    body[r][c] = 0;
                    hasChange = true;
                }
            }));
            if (hasChange) {
                item.body = body;
            }
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

    const findBodyIndex = (matrix, x, y, ftank) => {
        return tanks.findIndex(tank => {
            if (ftank && !ftank(tank)) {
                return false;
            }
            const tankBody = calcTankBody(tank);
            return matrix.some((row, r) => row.some((cell, c) => cell && tankBody[(y + r) * app.mainCols + x + c]));
        })
    }
    const findHitIndex = (x, y, ftank) => {
        return tanks.findIndex(tank => {
            if (ftank && !ftank(tank)) {
                return false;
            }
            return tank.withShot(tank, x, y);
        })
    }

    const tryCreateFlyItem = (ts, tank) => {
        const flyCount = flyItems.filter(s => s.tank == tank).length;
        if (flyCount < maxFlySize) {
            actionCallbacks.push(createShotAction(ts, tank));
            return true;
        }
        return false;
    }

    const removeFlyItem = item => {
        let curIndex = flyItems.findIndex(s => s == item);
        if (curIndex >= 0) {
            flyItems.splice(curIndex, 1);
        }
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
        let flyItem = {
            tank: tank,
            x: sx,
            y: sy,
            end: false
        }
        flyItems.push(flyItem);
        return newTs => {
            if (flyItem.end) {
                return false;
            }
            let ets = ~~((newTs - ts) / 50);
            let i = lastEts;
            for (; i <= ets; i++) {
                let nx = sx + i * x;
                let ny = sy + i * y;
                if (nx < 0 || nx >= app.mainCols || ny < 0 || ny >= app.mainRows) {
                    removeFlyItem(flyItem);
                    return false;
                }
                let findIndex = flyItems.findIndex(s => s.x == nx && s.y == ny && s.tank != tank);
                if (findIndex >= 0) {
                    flyItems[findIndex].end = true;
                    flyItems.splice(findIndex, 1);
                }
                let targetIndex = findHitIndex(nx, ny, t => t != tank);
                if (targetIndex < 0) {
                    continue;
                }
                removeFlyItem(flyItem);
                let findItem = tanks[targetIndex];
                if (findItem.dead) {
                    if (tank == tankItem) {
                        tanks.splice(targetIndex, 1);
                        if (findItem == bossItem) {
                            app.status.score += scorePerBoss;
                            bossKilled(newTs);
                        }
                        else {
                            app.status.score += scorePerTank;
                            levelScore += scorePerTank;
                            if (levelScore >= scorePerSpeed) {
                                bossCome(newTs);
                            }
                        }
                    }
                    else if (findItem == tankItem) {
                        subLife(newTs);
                    }
                }
                return false;
            }
            flyItem.x = sx + x * ets;
            flyItem.y = sy + y * ets;
            app.mainBoard[sy + y * ets][sx + x * ets] = newCell;
            lastEts = ets;
            return true;
        }
    }
    const doShot = (ts) => {
        return tryCreateFlyItem(ts, tankItem);
    };

    const isCommonShotPoint = (tank, x, y) => {
        return tank.dead = tank.body.some((row, r) => row.some((cell, c) => cell && tank.x + c == x && tank.y + r == y));
    }

    const tryCreateTank = (ts) => {
        if (tanks.length >= maxTankSize) {
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
                let findIndex = findBodyIndex(testBody, position.x, position.y);
                if (findIndex < 0) {
                    let newTank = {
                        x: position.x,
                        y: position.y,
                        actions: [action_Left, action_Right, action_Up, action_Down],
                        action: actionType,
                        body: tankBodys[actionType],
                        cell: randomCell(),
                        dead: false,
                        withShot: isCommonShotPoint,
                        doStep: doCommonAction,
                        getShotAction: tank => tank.action,
                        refreshBody: tank => tank.body = tankBodys[tank.action]
                    };
                    tanks.push(newTank);
                    return newTank;
                }
            }
        }
        return undefined;
    }

    const withShotBoss = (tank, x, y) => {
        if (tank.x - x >= tank.body[0].length || x - tank.x >= tank.body[0].length || tank.y - y >= tank.body.length || y - tank.y >= tank.body.length) {
            return;
        }
        let body = tank.body.map(row => [...row]);
        let hasHit = false;
        body.forEach((row, r) => row.forEach((cell, c) => {
            if (cell && tank.x + c == x && tank.y + r == y) {
                body[r][c] = 0;
                hasHit = true;
            }
        }));
        if (hasHit) {
            tank.body = body;
        }
        tank.dead = ~~((bossBody[0].length - 1) / 2) + tank.x == x && bossBody.length - 1 + tank.y == y;
        return hasHit;
    }

    const clearStage = (ts) => {
        tanks.splice(1, tanks.length - 1);
        flyItems.forEach(s => s.end = true);
        flyItems.splice(0, flyItems.length);
        updateBoard(ts);
    }

    const createBossComeCallback = (ts) => {
        let sx = ~~((app.mainCols - bossBody[0].length) / 2);
        let tsy = tankItem.y;
        let tey = app.mainRows - testBody.length;
        let bsy = -bossBody.length;
        let bey = 1;
        let bossCell = randomCell();
        return newTs => {
            let ets = ~~((newTs - ts) / 75);
            let ty = tsy + ets;
            let action = action_Down;
            if (ty >= tey) {
                ty = tey;
                action = action_Up;
            }
            tankItem.y = ty;
            tankItem.action = action;
            tankItem.refreshBody(tankItem);
            updateBoard(newTs);
            let by = bsy + ets + tsy - tey;
            if (by >= bey) {
                bossItem = {
                    x: ~~((app.mainCols - bossBody[0].length) / 2),
                    y: 1,
                    actions: [action_Left, action_Right],
                    action: action_Right,
                    cell: bossCell,
                    dead: false,
                    body: bossBody,
                    withShot: withShotBoss,
                    doStep: createBossStep(),
                    getShotAction: tank => action_Down,
                    refreshBody: tank => tank.body = bossBody
                }
                tanks.push(bossItem);
                tankItem.actions = [action_Left, action_Right];
                tankItem.action = action_Up;
                tankItem.refreshBody = tank => tank.body = tankBodys[action_Up];
                tankItem.getShotAction = tank => action_Up;
                tankItem.refreshBody(tankItem);
                lastTagTime = newTs;
                return false;
            }
            else if (by > 0) {
                for (let y = 0; y < by; y++) {
                    for (let x = 0; x < bossBody[0].length; x++) {
                        app.mainBoard[y][x + sx] = app.emptyCell;
                    }
                }
            }
            else if (-by <= bossBody.length) {
                for (let y = 0; y - by < bossBody.length; y++) {
                    for (let x = 0; x < bossBody[0].length; x++) {
                        app.mainBoard[y][x + sx] = bossBody[y - by][x] ? bossCell : app.emptyCell;
                    }
                }
            }
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
            updateBoard(ts);
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
        if (findBodyIndex(testBody, newx, newy, t => t != item) >= 0) {
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
        if (item.action == action_Right || item.x >= app.mainCols - item.body[0].length - 1) {
            let removeIndex = actionTypes.findIndex(s => s == action_Right);
            removeIndex >= 0 && actionTypes.splice(removeIndex, 1);
        }
        if (item.action == action_Down || item.y >= app.mainCols - item.body.length - 1) {
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
            tryCreateFlyItem(ts,item);
        }
    }

    const createBossStep = () => {
        let actionIndex = 1;
        return (ts, item) => {
            if (!tryMove(item)) {
                actionIndex = (actionIndex + 1) % item.actions.length;
                item.action = item.actions[actionIndex];
                tryMove(item);
            }
            tryCreateFlyItem(ts,item);
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
        updateBoard(ts);
        for (let index = 0; index < actionCallbacks.length; index++) {
            if (!actionCallbacks[index](ts)) {
                actionCallbacks.splice(index, 1);
                break;
            }
        }
    }
    return { keyMap, init, update };
}

export { game as default };
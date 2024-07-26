import keys from './keyboard.js';

function game(options) {

    let maxLevel = 30;
    let scorePerSpeed = 2000;
    let scorePerTank = 100;
    let scorePerBoss = 1000;

    let lastTagTime = 0;
    let maxTankSize = 4;
    let createTankTick = 0;
    let createTankTicks = 2;
    let shotPercent = 0.25;
    let wayPercent = 0.2;

    let actionCallbacks = new Set();
    let tankSet = new Set();
    const action_Up = 0, action_Right = 1, action_Down = 2, action_Left = 3;
    let steps = [[0, -1], [1, 0], [0, 1], [-1, 0]];
    const body_none = 0, body_common = 1, body_slide = 2, body_tail = 3;
    let tankBodys = [
        [[body_slide, body_common, body_slide], [body_common, body_common, body_common], [body_common, body_tail, body_common]],
        [[body_common, body_common, body_slide], [body_tail, body_common, body_common], [body_common, body_common, body_slide]],
        [[body_common, body_tail, body_common], [body_common, body_common, body_common], [body_slide, body_common, body_slide]],
        [[body_slide, body_common, body_common], [body_common, body_common, body_tail], [body_slide, body_common, body_common]],
    ];
    let flyItems = new Set();

    let tankPosions;

    let app, tankItem, tankCell;
    function updateBoard(ts) {
        for (let r = 0; r < app.mainRows; r++) {
            for (let c = 0; c < app.mainCols; c++) {
                app.mainBoard[r][c] = app.emptyCell;
            }
        }

        tankItem.body.forEach((row, r) => {
            row.forEach((cell, c) => {
                cell == body_common && (app.mainBoard[tankItem.y - 1 + r][tankItem.x - 1 + c] = (r == 1 && c == 1 && (~~ts % 300) > 150) ? app.emptyCell : tankItem.cell);
            })
        });

        tankSet.forEach(tank => {
            tank.body.forEach((row, r) => {
                row.forEach((cell, c) => {
                    cell == body_common && (app.mainBoard[tank.y - 1 + r][tank.x - 1 + c] = tank.cell);
                })
            });
        });

        for (let i = 0; i < app.subRows; i++) {
            app.subBoard[i].fill(app.status.life > (app.subRows - 1 - i) ? tankCell : app.emptyCell);
        }
    }


    function initLevel(ts) {
        lastTagTime = ts;
        actionCallbacks.clear();
        tankSet.clear();
        createTankTick = 0;
        tankItem = {
            x: ~~(app.mainCols / 2),
            y: ~~(app.mainRows / 2),
            step: steps[action_Up],
            body: tankBodys[action_Up],
            cell: tankCell
        };
        updateBoard(ts);
    }

    function updateGrade(ts) {
        if (!app.status.updateSpeed(app.speeds.length) && !app.status.updateGrade(maxLevel)) {
            return false;
        }
        app.status.updateLife(app.subRows, true);
        //app.addFlashCallback(ts, (newTs) => initLevel(newTs));
        initLevel(ts);
        return true;
    }

    function subLife(ts) {
        if (!app.status.updateLife(app.subRows, false)) {
            return false;
        }
        app.addFlashCallback(ts, (newTs) => initLevel(newTs));
        return true;
    }

    const overrideOtherBody = (tank) => {
        let tankBody = {};
        tank.body.forEach((row, r) => row.forEach((cell, c) => {
            if (cell == body_common) {
                tankBody[tank.x - 1 + c + (tank.y - 1 + r) * app.mainCols] = cell;
            }
        }));
        [tankItem, ...tankSet].forEach(item => {
            if (item == tank) {
                return;
            }
            if (tank.x - item.x >= 3 || item.x - tank.x >= 3 || tank.y - item.y >= 3 || item.y - tank.y >= 3) {
                return;
            }
            let body = item.body.map(row => [...row]);
            let hasChange = false;
            body.forEach((row, r) => row.forEach((cell, c) => {
                if (cell == body_common && tankBody[item.x - 1 + c + (item.y - 1 + r) * app.mainCols] == body_common) {
                    body[r][c] = body_none;
                    hasChange = true;
                }
            }));
            if (hasChange) {
                item.body = body;
            }
        });
    };

    const doMove = (ts, actionType) => {
        const newStep = steps[actionType];
        if (newStep != tankItem.step) {
            tankItem.step = newStep;
            tankItem.body = tankBodys[actionType];
            overrideOtherBody(tankItem);
            return true;
        }
        return tryMove(tankItem);
    };

    const findItems = (items, matrix, x, y) => {
        return items.find(item =>
            matrix.some((mrow, mr) =>
                mrow.some((mcell, mc) =>
                    mcell && (mc + x >= item.x - 1 && mc + x <= item.x + 1 && mr + y >= item.y - 1 && mr + y <= item.y + 1)
                )
            )
        );
        // return items.find(item => item.body.some((row, r) => row.some((cell, c) => {
        //     if (cell == 0) {
        //         return false;
        //     }
        //     let tx = item.x - 1 + c;
        //     let ty = item.y - 1 + r;
        //     return matrix.some((mrow, mr) => mrow.some((mcell, mc) => mcell && (tx == mc + x && ty == mr + y)));
        // })));
    }

    const createShotAction = (ts, tank) => {
        let x = tank.step[0];
        let y = tank.step[1];
        let sx = tank.x + x + x;
        let sy = tank.y + y + y;
        let lastEts = 0;
        let newCell = tank.cell;
        let flyItem = {
            tank: tank,
            x: sx,
            y: sy,
            end: false
        }
        flyItems.add(flyItem);
        return newTs => {
            if (flyItem.end) {
                return false;
            }
            let ets = ~~((newTs - ts) / 50);
            let i = lastEts;
            let items = [tankItem, ...tankSet].filter(s => s != tank);
            for (; i <= ets; i++) {
                let nx = sx + i * x;
                let ny = sy + i * y;
                if (nx < 0 || nx >= app.mainCols || ny < 0 || ny >= app.mainRows) {
                    return false;
                }
                let findFly = [...flyItems].find(s=>s.x == nx && s.y == ny && s.tank != tank);      
                if(findFly){
                    findFly.end = true;
                    flyItems.delete(findFly);
                }          
                let findItem = findItems(items, [[1]], nx, ny);
                if (!findItem) {
                    continue;
                }
                flyItems.delete(findFly);
                if (tank == tankItem) {
                    tankSet.delete(findItem);
                    const oldScore = app.status.score;
                    app.status.score = app.status.score + scorePerTank;
                    if (~~(app.status.score / scorePerSpeed) != ~~(oldScore / scorePerSpeed)) {
                        updateGrade(ts);
                    }
                }
                else if (findItem == tankItem) {
                    subLife(ts);
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
        actionCallbacks.add(createShotAction(ts, tankItem));
        return true;
    };

    const tryCreateTank = (ts) => {
        if (tankSet.size >= maxTankSize) {
            return undefined;
        }
        let positions = [...tankPosions];
        let allItems = [...tankSet, tankItem];
        while (positions.length) {
            let pi = ~~(positions.length * Math.random());
            let position = positions.splice(pi, 1)[0];
            let actionTypes = [...position.ac];
            while (actionTypes.length) {
                let ai = ~~(actionTypes.length * Math.random());
                let actionType = actionTypes.splice(ai, 1)[0];
                let body = tankBodys[actionType];
                let findItem = findItems(allItems, body, position.x - 1, position.y - 1);
                if (!findItem) {
                    let newTank = {
                        x: position.x,
                        y: position.y,
                        step: steps[actionType],
                        body: body,
                        cell: randomCell()
                    };
                    tankSet.add(newTank);
                    return newTank;
                }
            }
        }
        return undefined;
    }

    const tryMove = item => {
        let matrix, findx, findy, actionType;
        switch (item.step) {
            case steps[action_Up]:
                actionType = action_Up;
                if (item.y + item.step[1] < 1) {
                    return false;
                }
                matrix = [[0, 1, 0], [1, 0, 1]];
                findx = item.x - 1;
                findy = item.y + item.step[1] - 1;
                break;
            case steps[action_Down]:
                actionType = action_Down;
                if (item.y + item.step[1] > app.mainRows - 2) {
                    return false;
                }
                matrix = [[1, 0, 1], [0, 1, 0]];
                findx = item.x - 1;
                findy = item.y + item.step[1];
                break;
            case steps[action_Left]:
                actionType = action_Left;
                if (item.x + item.step[0] < 1) {
                    return false;
                }
                matrix = [[0, 1], [1, 0], [0, 1]];
                findx = item.x + item.step[0] - 1;
                findy = item.y - 1;
                break;
            case steps[action_Right]:
                actionType = action_Right;
                if (item.x + item.step[0] > app.mainCols - 2) {
                    return false;
                }
                matrix = [[1, 0], [0, 1], [1, 0]];
                findx = item.x + item.step[0];
                findy = item.y - 1;
                break;
            default:
                return false;
        }
        let allItems = [...tankSet, tankItem].filter(s => s != item);
        if (findItems(allItems, matrix, findx, findy)) {
            return false;
        }
        item.x += item.step[0];
        item.y += item.step[1];
        item.body = tankBodys[actionType];
        return true;
    }

    const findNewWay = item => {
        const actionTypes = [];
        if (item.x > 1 && item.step != steps[action_Left]) {
            actionTypes.push(action_Left);
        }
        if (item.y > 1 && item.step != steps[action_Up]) {
            actionTypes.push(action_Up);
        }
        if (item.x < app.mainCols - 2 && item.step != steps[action_Right]) {
            actionTypes.push(action_Right);
        }
        if (item.y < app.mainRows - 2 && item.step != steps[action_Down]) {
            actionTypes.push(action_Down);
        }
        let actionType = actionTypes[~~(Math.random() * actionTypes.length)];
        item.step = steps[actionType];
        item.body = tankBodys[actionType];
        overrideOtherBody(item);
    }

    const doStep = (ts) => {
        createTankTick++;
        let newTank = undefined;
        if (createTankTick >= createTankTicks) {
            newTank = tryCreateTank(ts);
        }
        tankSet.forEach(item => {
            if (item != newTank) {
                let findWay = false;
                if (Math.random() < wayPercent) {
                    findNewWay(item);
                    findWay = true;
                }
                if (!tryMove(item) && !findWay) {
                    findNewWay(item);
                }
            }
            if (Math.random() < shotPercent) {
                actionCallbacks.add(createShotAction(ts, item));
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
            { x: 1, y: 1, ac: [action_Right, action_Down] },
            { x: app.mainCols - 2, y: 1, ac: [action_Left, action_Down] },
            { x: app.mainCols - 2, y: app.mainRows - 2, ac: [action_Left, action_Up] },
            { x: 1, y: app.mainRows - 2, ac: [action_Right, action_Up] }
        ];
        initLevel(ts);
    }
    const update = (ts) => {
        if (ts - lastTagTime > app.speeds[app.status.speed] * 2) {
            lastTagTime = ts;
            doStep(ts);
        }
        updateBoard(ts);
        for (let callback of [...actionCallbacks]) {
            if (!callback(ts)) {
                actionCallbacks.delete(callback);
                break;
            }
        }
    }
    return { keyMap, init, update };
}

export { game as default };
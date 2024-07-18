import keys from './keyboard.js';

function game(options) {
    const status = {
        score: 0,
        speed: 0,
        level: 0,
        over: true,
    };

    const maxLevel = 30;

    options = Object.assign({ loop: false }, options ?? {});

    const boardEmptyColor = 'gray';
    const snakeColor = '#000';
    const colors = ["red", "green", "blue", "purple", "orange"];
    const speeds = { 0: 1000, 1: 900, 2: 800, 3: 700, 4: 600, 5: 500, 6: 450, 7: 400, 8: 350, 9: 300, 10: 250, 11: 200, 12: 150, 13: 100, 14: 50, 15: 25, 16: 10 };


    let mBoard, sBoard, boardRows, boardCols, cshape, ncolor, snake, nstep;

    function updateBoard(ts) {

        mBoard.forEach(row => row.fill(boardEmptyColor));
        snake.forEach(value => mBoard[value.y][value.x] = value.color);
        if (cshape) {
            mBoard[cshape.y][cshape.x] = (~~ts % 300) > 150 ? cshape.color : boardEmptyColor;
        }

        sBoard.forEach(row => row.fill(boardEmptyColor));
        sBoard[1][1] = ncolor;
    }
    function calcEmptyShapes() {
        const emptyShapes = [];
        const sets = new Set(snake.map(s => s.y * boardRows + s.x));
        for (let y = 0; y < boardRows; y++) {
            for (let x = 0; x < boardCols; x++) {
                !sets.has(y * boardRows + x) && emptyShapes.push({ x, y });
            }
        }
        return emptyShapes;
    }
    function createShape() {
        const emptyShapes = calcEmptyShapes();
        if (emptyShapes.length === 0) {
            status.over = true;
            return undefined;
        }
        const item = emptyShapes[~~(Math.random() * emptyShapes.length)];
        const color = ncolor;
        ncolor = randomColor();
        return { ...item, color };
    }

    function newSnake() {
        ncolor = randomColor();
        const initColor = randomColor();
        snake = [
            { x: ~~(boardCols / 2) + 1, y: ~~(boardRows / 2), color: snakeColor },
            { x: ~~(boardCols / 2), y: ~~(boardRows / 2), color: initColor },
            { x: ~~(boardCols / 2) - 1, y: ~~(boardRows / 2), color: initColor },
            { x: ~~(boardCols / 2) - 2, y: ~~(boardRows / 2), color: initColor }];
        cshape = createShape();
        //const steps = [[0, 1], [1, 0], [0, -1], [-1, 0]];
        nstep = [1, 0];//steps[~~(Math.random() * steps.length)];
    }

    function nextLevel() {
        const overPoint = boardCols * boardRows - maxLevel + status.level - snake.length;
        if (overPoint > 0) {
            status.score += overPoint * 100;
        }
        status.speed += 1;
        const speedLength = Object.keys(speeds).length;
        if (status.speed >= speedLength) {
            status.speed = 0;
            status.level++;
            if (status.level > maxLevel) {
                status.over = true;
                return;
            }
        }
        newSnake();
    }

    function doStep(step) {
        if (step[0] + nstep[0] === 0 && step[1] + nstep[1] === 0) {
            return false;
        }
        let newx = snake[0].x + step[0], newy = snake[0].y + step[1],newColor = undefined;
        if (options.loop) {
            newx = (newx + boardCols) % boardCols;
            newy = (newy + boardRows) % boardRows;
        }
        else if (newx < 0 || newx >= boardCols || newy < 0 || newy >= boardRows) {
            status.over = true;
            return false;
        }
        const sets = new Set(snake.slice(0, -1).map(s => s.y * boardRows + s.x));
        if (sets.has(newy * boardRows + newx)) {
            status.over = true;
            return false;
        }
        if (cshape.x === newx && cshape.y === newy) {
            newColor = cshape.color;
            snake.push(cshape);
            cshape = createShape();
            status.score += 100;
            if (snake.length >= boardCols * boardRows - maxLevel + status.level) {
                nextLevel();
            }
        }
        for (let i = snake.length - 1; i > 0; i--) {
            snake[i].x = snake[i - 1].x;
            snake[i].y = snake[i - 1].y;
            newColor && (snake[i].color = newColor);
        }
        snake[0].x = newx;
        snake[0].y = newy;
        nstep = step;
        return true;
    }

    let currentAction = undefined;
    let lastAction = undefined;
    let lastDelay = undefined;
    let repeatTimes = undefined;
    let freezeAction = undefined;

    const actionMap = {
        [keys.KEY_LEFT]: [100, 50, () => doStep([-1, 0])],
        [keys.KEY_RIGHT]: [100, 50, () => doStep([1, 0])],
        [keys.KEY_DOWN]: [100, 50, () => doStep([0, 1])],
        [keys.KEY_UP]: [100, 50, () => doStep([0, -1])],
        [keys.KEY_ACTION]: [100, 50, () => doStep(nstep)],
        [keys.KEY_EXTEND]: [3000, 1000, () => nextLevel()]
    };
    function randomColor() {
        return colors[~~(Math.random() * colors.length)];
    }

    function checkKeys(ts, keys, addKeys, removeKeys) {
        if (currentAction && (!keys.size || !keys.has(currentAction))) {
            freezeAction = currentAction = undefined;
        }
        if (!currentAction && keys.size) {
            Object.keys(actionMap).some(key => {
                keys.has(key) && (currentAction = key)
            });
        }
        if (currentAction && currentAction != freezeAction) {
            const [fdelay, odelay, actionCallback] = actionMap[currentAction] ?? [undefined, undefined, undefined];
            if (!actionCallback) {
                return;
            }
            if (!lastAction) {
                lastAction = currentAction;
                lastDelay = ts;
                repeatTimes = 0;
            }
            else if (lastDelay) {
                if (ts - lastDelay < (repeatTimes == 0 ? fdelay : odelay)) {
                    return;
                }
                lastDelay = ts;
                repeatTimes++;;
            }
            actionCallback() && (gtime = ts)
        }
        else {
            lastAction = undefined;
        }
    }
    const init = (ts, mainBoard, subBoard) => {
        mBoard = mainBoard;
        sBoard = subBoard;
        boardRows = mBoard.length;
        boardCols = mBoard[0].length;
        currentAction = undefined;
        for (let r = 0; r < boardRows; r++) {
            for (let c = 0; c < boardCols; c++) {
                mBoard[r][c] = boardEmptyColor;
            }
        }
        Object.assign(status, { score: 0, level: 0, speed: 0, over: false });
        newSnake();
    }

    let gtime = 0;
    const update = (ts, keys, addKeys, removeKeys) => {
        if (!status.over) {
            checkKeys(ts, keys, addKeys, removeKeys);
        }
        if (!status.over) {
            if (ts - gtime > (speeds[status.speed] ?? 1) || ts < gtime) {
                gtime = ts;
                doStep(nstep);
            }
        }
        updateBoard(ts);
    }
    return { status, init, update };
}


export { game as default };
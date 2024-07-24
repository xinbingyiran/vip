import keys from './keyboard.js';

function game(options) {
    const status = {
        score: 0,
        speed: 0,
        level: 0,
        life: 0,
        over: true,
    };

    const maxLevel = 30;

    let lastTagTime = 0;

    options = Object.assign({ loop: false }, options ?? {});

    const speeds = { 0: 1000, 1: 900, 2: 800, 3: 700, 4: 600, 5: 500, 6: 450, 7: 400, 8: 350, 9: 300, 10: 250, 11: 200, 12: 150, 13: 100, 14: 50, 15: 25, 16: 10 };


    let app, cshape, snake, nstep, headCell;

    function updateBoard(ts) {
        app.mainBoard.forEach(row => row.fill(app.emptyCell));
        snake.forEach(value => app.mainBoard[value.y][value.x] = value.cell);
        if (cshape) {
            app.mainBoard[cshape.y][cshape.x] = (~~ts % 300) > 150 ? cshape.cell : app.emptyCell;
        }

        app.subBoard.forEach(row => row.fill(app.emptyCell));
        for (let i = 0; i < app.subRows && i < status.life; i++) {
            app.subBoard[4 - i - 1][1] = headCell;
        }
    }
    function calcEmptyShapes() {
        const emptyShapes = [];
        const sets = new Set(snake.map(s => s.y * app.mainRows + s.x));
        for (let y = 0; y < app.mainRows; y++) {
            for (let x = 0; x < app.mainCols; x++) {
                !sets.has(y * app.mainRows + x) && emptyShapes.push({ x, y });
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
        const cell = randomCell();
        return { ...item, cell };
    }

    function initLevel(ts) {
        const newCell = randomCell();
        snake = [
            { x: ~~(app.mainCols / 2) + 1, y: ~~(app.mainRows / 2), cell: headCell },
            { x: ~~(app.mainCols / 2), y: ~~(app.mainRows / 2), cell: newCell },
            { x: ~~(app.mainCols / 2) - 1, y: ~~(app.mainRows / 2), cell: newCell },
            { x: ~~(app.mainCols / 2) - 2, y: ~~(app.mainRows / 2), cell: newCell }];
        cshape = createShape();
        //const steps = [[0, 1], [1, 0], [0, -1], [-1, 0]];
        nstep = [1, 0];//steps[~~(Math.random() * steps.length)];
    }

    function nextLevel(ts) {
        const overPoint = app.mainCols * app.mainRows - maxLevel + status.level - snake.length;
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
                return false;
            }
        }
        initLevel(ts);
        return true;
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

    function doStep(ts, step) {
        if (step[0] + nstep[0] === 0 && step[1] + nstep[1] === 0) {
            return false;
        }
        let newx = snake[0].x + step[0], newy = snake[0].y + step[1], newCell = undefined;
        if (options.loop) {
            newx = (newx + app.mainCols) % app.mainCols;
            newy = (newy + app.mainRows) % app.mainRows;
        }
        else if (newx < 0 || newx >= app.mainCols || newy < 0 || newy >= app.mainRows) {
            return subLife(ts);
        }
        const sets = new Set(snake.slice(0, -1).map(s => s.y * app.mainRows + s.x));
        if (sets.has(newy * app.mainRows + newx)) {
            return subLife(ts);
        }
        if (cshape.x === newx && cshape.y === newy) {
            newCell = cshape.cell;
            snake.push(cshape);
            cshape = createShape();
            status.score += 100;
            if (snake.length >= app.mainCols * app.mainRows - maxLevel + status.level) {
                nextLevel();
            }
        }
        for (let i = snake.length - 1; i > 0; i--) {
            snake[i].x = snake[i - 1].x;
            snake[i].y = snake[i - 1].y;
            newCell && (snake[i].cell = newCell);
        }
        snake[0].x = newx;
        snake[0].y = newy;
        nstep = step;
        return true;
    }

    const keyMap = {
        [keys.KEY_LEFT]: [100, 50, (ts) => doStep(ts, [-1, 0])],
        [keys.KEY_RIGHT]: [100, 50, (ts) => doStep(ts, [1, 0])],
        [keys.KEY_DOWN]: [100, 50, (ts) => doStep(ts, [0, 1])],
        [keys.KEY_UP]: [100, 50, (ts) => doStep(ts, [0, -1])],
        [keys.KEY_ACTION]: [100, 50, (ts) => doStep(ts, nstep)],
        [keys.KEY_EXTEND]: [3000, 1000, (ts) => nextLevel(ts)]
    };

    function randomCell() {
        return app.cells[~~(Math.random() * app.cells.length - 1) + 1];
    }

    const init = (ts, mainApp) => {
        lastTagTime = ts;
        app = mainApp;
        headCell = app.cells[0];
        for (let r = 0; r < app.mainRows; r++) {
            for (let c = 0; c < app.mainCols; c++) {
                app.mainBoard[r][c] = app.emptyCell;
            }
        }
        Object.assign(status, { score: 0, level: 0, speed: 0, life: app.subRows, over: false });
        initLevel(ts);
    }
    const update = (ts) => {
        if (!status.over) {
            if (ts - lastTagTime > (speeds[status.speed] ?? 1) || ts < lastTagTime) {
                lastTagTime = ts;
                doStep(ts, nstep);
            }
        }
        updateBoard(ts);
    }
    return { status, keyMap, init, update };
}


export { game as default };
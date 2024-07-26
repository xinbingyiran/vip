import keys from './keyboard.js';

function game(options) {
    const maxLevel = 30;
    let lastTagTime = 0;
    options = Object.assign({ loop: false }, options ?? {});

    let app, cshape, snake, nstep, headCell;

    function updateBoard(ts) {
        app.mainBoard.forEach(row => row.fill(app.emptyCell));
        snake.forEach(value => app.mainBoard[value.y][value.x] = value.cell);
        if (cshape) {
            app.mainBoard[cshape.y][cshape.x] = (~~ts % 300) > 150 ? cshape.cell : app.emptyCell;
        }

        app.subBoard.forEach(row => row.fill(app.emptyCell));
        for (let i = 0; i < app.subRows && i < app.status.life; i++) {
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
            app.status.over = true;
            return undefined;
        }
        const item = emptyShapes[~~(Math.random() * emptyShapes.length)];
        const cell = randomCell();
        return { ...item, cell };
    }

    function initLevel(ts) {
        lastTagTime = ts;
        const newCell = randomCell();
        const headx = 3;
        const heady = ~~(app.mainRows / 2);
        snake = [
            { x: headx, y: heady, cell: headCell },
            { x: headx - 1, y: heady, cell: newCell },
            { x: headx - 2, y: heady, cell: newCell },
            { x: headx - 3, y: heady, cell: newCell }
        ];
        cshape = createShape();
        //const steps = [[0, 1], [1, 0], [0, -1], [-1, 0]];
        nstep = [1, 0];//steps[~~(Math.random() * steps.length)];
        updateBoard(ts);
    }

    function updateGrade(ts) {
        if (!app.status.updateSpeed(app.speeds.length) && !app.status.updateGrade(maxLevel)) {
            return false;
        }
        app.status.updateLife(app.subRows, true);
        app.addFlashCallback(ts, (newTs) => initLevel(newTs));
        return true;
    }

    function subLife(ts) {
        if (!app.status.updateLife(app.subRows, false)) {
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
            subLife(ts);
            return false;
        }
        const sets = new Set(snake.slice(0, -1).map(s => s.y * app.mainRows + s.x));
        if (sets.has(newy * app.mainRows + newx)) {
            subLife(ts);
            return false;
        }
        if (cshape.x === newx && cshape.y === newy) {
            newCell = cshape.cell;
            snake.push(cshape);
            app.status.score += 100;
            if (snake.length >= app.mainCols * app.mainRows - maxLevel + app.status.level) {
                return updateGrade(ts);
            }
            cshape = createShape();
        }
        for (let i = snake.length - 1; i > 0; i--) {
            snake[i].x = snake[i - 1].x;
            snake[i].y = snake[i - 1].y;
            newCell && (snake[i].cell = newCell);
        }
        snake[0].x = newx;
        snake[0].y = newy;
        nstep = step;
        //lastTagTime = ts;
        return true;
    }

    const keyMap = {
        [keys.KEY_LEFT]: [100, 50, (ts) => doStep(ts, [-1, 0])],
        [keys.KEY_RIGHT]: [100, 50, (ts) => doStep(ts, [1, 0])],
        [keys.KEY_DOWN]: [100, 50, (ts) => doStep(ts, [0, 1])],
        [keys.KEY_UP]: [100, 50, (ts) => doStep(ts, [0, -1])],
        [keys.KEY_ACTION]: [100, 50, (ts) => doStep(ts, nstep)],
        [keys.KEY_EXTEND]: [3000, 1000, (ts) => updateGrade(ts)]
    };

    function randomCell() {
        return app.cells[~~(Math.random() * app.cells.length - 1) + 1];
    }

    const init = (ts, mainApp) => {
        app = mainApp;
        headCell = app.cells[0];
        initLevel(ts);
    }
    const update = (ts) => {
        if (!app.status.over) {
            if (ts - lastTagTime > app.speeds[app.status.speed]) {
                lastTagTime = ts;
                doStep(ts, nstep);
            }
        }
        updateBoard(ts);
    }
    return { keyMap, init, update };
}

export { game as default };
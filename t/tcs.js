function game(app, { loop = false } = {}) {
    const maxLevel = 30;
    let lastTagTime = 0;

    let status, food, snake, nstep, headCell;

    function updateBoard(ts) {
        app.mainBoard.forEach(row => row.fill(app.emptyCell));
        snake.forEach(value => app.mainBoard[value.y][value.x] = value.cell);
        if (food) {
            app.mainBoard[food.y][food.x] = (~~ts % 300) > 150 ? food.cell : app.emptyCell;
        }

        for (let i = 0; i < app.subRows; i++) {
            app.subBoard[i].fill(status.life > (app.subRows - 1 - i) ? headCell : app.emptyCell);
        }
    }
    function calcEmptyPositions() {
        const emptyPositions = [];
        const sets = new Set(snake.map(s => s.y * app.mainCols + s.x));
        const totalCells = app.mainRows * app.mainCols;
        for (let i = 0; i < totalCells; i++) {
            !sets.has(i) && emptyPositions.push({ x: i % app.mainCols, y: ~~(i / app.mainCols) });
        }
        return emptyPositions;
    }
    function createFood() {
        const emptyPositions = calcEmptyPositions();
        if (emptyPositions.length === 0) {
            subLife(ts);
            return undefined;
        }
        const item = emptyPositions[~~(Math.random() * emptyPositions.length)];
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
        food = createFood();
        //const steps = [[0, 1], [1, 0], [0, -1], [-1, 0]];
        nstep = [1, 0];//steps[~~(Math.random() * steps.length)];
        updateBoard(ts);
    }

    function updateGrade(ts) {
        if (!status.updateSpeed(app.speeds.length) && !status.updateGrade(maxLevel)) {
            return false;
        }
        status.updateLife(app.subRows, true);
        app.addFlashCallback(()=>initLevel(ts));
        return true;
    }

    function subLife(ts) {
        if (!status.updateLife(app.subRows, false)) {
            return false;
        }
        app.addFlashCallback(()=>initLevel(ts));
        return true;
    }

    function doStep(ts, step) {
        if (step[0] + nstep[0] === 0 && step[1] + nstep[1] === 0) {
            return false;
        }
        let newx = snake[0].x + step[0], newy = snake[0].y + step[1], newCell = undefined;
        if (loop) {
            newx = (newx + app.mainCols) % app.mainCols;
            newy = (newy + app.mainRows) % app.mainRows;
        }
        else if (newx < 0 || newx >= app.mainCols || newy < 0 || newy >= app.mainRows) {
            subLife(ts);
            return false;
        }
        const sets = new Set(snake.slice(0, -1).map(s => s.y * app.mainCols + s.x));
        if (sets.has(newy * app.mainCols + newx)) {
            subLife(ts);
            return false;
        }
        if (food.x === newx && food.y === newy) {
            newCell = food.cell;
            snake.push(food);
            status.score += 100;
            if (snake.length >= app.mainCols * app.mainRows - maxLevel + status.level) {
                updateGrade(ts);
                return false;
            }
            food = createFood();
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
        [app.keys.KEY_LEFT]: [100, 50, (ts) => doStep(ts, [-1, 0])],
        [app.keys.KEY_RIGHT]: [100, 50, (ts) => doStep(ts, [1, 0])],
        [app.keys.KEY_DOWN]: [100, 50, (ts) => doStep(ts, [0, 1])],
        [app.keys.KEY_UP]: [100, 50, (ts) => doStep(ts, [0, -1])],
        [app.keys.KEY_ACTION]: [100, 50, (ts) => doStep(ts, nstep)],
        [app.keys.KEY_EXTEND]: [3000, 1000, (ts) => updateGrade(ts)]
    };

    function randomCell() {        
        return app.cells[~~(Math.random() * app.cells.length)];
    }

    const init = (ts, mainStatus) => {
        status = mainStatus;
        headCell = randomCell();
        initLevel(ts);
    }
    const update = (ts) => {
        if (!status.over) {
            if (ts - lastTagTime > app.speeds[status.speed]) {
                lastTagTime = ts;
                doStep(ts, nstep);
            }
        }
        updateBoard(ts);
    }
    return { keyMap, init, update };
}

export { game as default };
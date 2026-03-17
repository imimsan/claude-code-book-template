# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Server

```bash
npx http-server . -p 8080 --cors -c-1
```

`-c-1` disables caching, which is important when iterating on JS/HTML changes — without it the browser serves stale files.

## Architecture

This is a vanilla JS canvas-based Breakout game with no build step, bundler, or dependencies. Two files:

- **`index.html`** — Layout, CSS, and DOM elements only. Loads `main.js` at the end of `<body>` so all DOM elements exist before the script runs.
- **`main.js`** — All game logic, physics, and rendering.

### Game Loop

`gameLoop()` uses `requestAnimationFrame` and calls `update()` then `draw()` each frame. `update()` returns `false` (continue), `true` (game over), or `'clear'` (game clear) to signal state transitions. The loop stops by simply not registering the next `requestAnimationFrame` — never use a `running` flag to gate the loop, as it causes duplicate loop bugs.

### State

Global mutable state: `score`, `lives`, `level`, `ball`, `paddle`, `blocks`, `penetrate`, `animId`, `clearAnimId`.

- `initGame()` resets everything and calls `initLevel()`
- `initLevel()` creates the ball, paddle, and block grid for the current level
- `MAX_LEVEL = 3` — completing level 3 triggers the game clear sequence

### Physics

- Ball reflection off walls/ceiling uses absolute value of velocity to avoid sign errors
- Paddle reflection computes angle based on hit position (`hit` ratio from center), capped at ±60°
- Block collision resolves which face was hit by comparing four overlap values and picking the minimum
- Penetrate mode (`P` key) skips reflection and `break` in the block loop so the ball passes through all blocks in one frame

### Input

All keyboard listeners are registered once. `keys` object tracks held keys for paddle movement; discrete actions (launch, penetrate toggle) are handled directly in `keydown`.

### Rendering

All drawing uses the Canvas 2D API (`ctx`). Gradients are created fresh each frame. `draw()` is also called during the game clear animation with reduced `globalAlpha` to show a faded background.

### Game Clear Animation

`startGameClear()` spawns 120 particles and starts a separate `drawClear()` animation loop (`clearAnimId`). Time-based via `performance.now()` — opacity, overlay darkness, and text glow all derive from elapsed seconds. Both `animId` and `clearAnimId` must be cancelled in `restartGame()`.

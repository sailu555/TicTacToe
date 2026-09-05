# Tic Tac Toe — Running & Testing Guide

Assumes you've already completed [SETUP.md](./SETUP.md).

## Running the application

You need **two terminals** — the backend and frontend run as separate
processes.

### Terminal 1 — Backend

```bash
cd backend/TicTacToe.Api
dotnet run
```

You should see output confirming the app is listening on
`http://localhost:5000`. Leave this running.

### Terminal 2 — Frontend

```bash
cd frontend
npm run build   # compiles src/server.ts -> dist/, src/client.ts -> public/
npm start        # serves the app on http://localhost:3000
```

> During active development, `npm run dev` combines both steps.

### Open the app

Navigate to **http://localhost:3000**. The page immediately creates a new
Two Player game against the backend and displays the board.

## Exploring the API directly (Swagger)

With the backend running, open **http://localhost:5000/swagger**. This
gives you an interactive page listing every endpoint, its expected
request/response shapes, and a "Try it out" button to fire real requests
without needing the frontend or a separate tool like Postman. This is the
fastest way to test the backend in isolation.

> Note: Swagger's "Try it out" won't automatically attach the
> `X-Session-Id` header for you on every call — you'll need to add it
> manually in the header field for `POST /api/game`, `GET /api/scoreboard`,
> and `POST /api/scoreboard/reset`. Any GUID string works, e.g.
> `11111111-1111-1111-1111-111111111111`.

## Manual test checklist

Use this checklist to verify the application end-to-end after any change.
All of these can be done either through the UI at `localhost:3000` or
directly via Swagger/curl.

### Core gameplay

- [ ] **Create a Two Player game** — board is empty, status is
      `InProgress`, it's X's turn.
- [ ] **Play alternating moves** (X, O, X, O, ...) — board updates, turn
      alternates, occupied cells can't be clicked/re-played.
- [ ] **Win a game** — get three in a row (any of the 8 lines). Confirm:
  - `status` becomes `"Won"`
  - `winner` is set to the correct player
  - `winningLine` contains the 3 correct cell indices
  - the board becomes non-interactive
- [ ] **Draw a game** — fill the board with no winner. Confirm `status`
      becomes `"Draw"`.

### vs Computer mode

- [ ] **Create a VsComputer game** — human is X and moves first.
- [ ] **Make a human move** — confirm the computer's reply is applied
      automatically in the same response (no separate call needed).
- [ ] **Try to beat the computer** — play several games; the computer
      should **never lose** (it should only ever win or draw), since it
      plays via exhaustive minimax search.

### Undo

- [ ] **Two Player undo** — make 2+ moves, undo once, confirm only the
      *last* move is removed and the turn returns to that same player.
- [ ] **VsComputer undo** — make a human move (triggering the computer's
      auto-reply), then undo once, confirm **both** the computer's move and
      the human's move are removed together, and it's the human's turn again.
- [ ] **Undo with no moves** — attempt undo on a freshly created game;
      expect `400 Bad Request` ("No moves to undo yet").
- [ ] **Undo after game over** — win or draw a game, then attempt undo;
      expect `400 Bad Request` ("Cannot undo — this game has already ended").
  Also confirm the scoreboard count from that finished game is **not**
  affected by the rejected undo attempt.

### Reset Board vs Reset Scoreboard

- [ ] **Reset Board mid-game** — confirm the board clears, move history
      empties, and the game keeps the same id and mode.
- [ ] **Reset Board after a finished game** — confirm the scoreboard total
      recorded from that finished game is **unchanged** by the reset.
- [ ] **Reset Scoreboard** — confirm `xWins`/`oWins`/`draws` all return to
      `0`, and confirm this does **not** affect any in-progress game's board.

### Scoreboard correctness

- [ ] **Win several games in a row** (same session) — confirm the
      scoreboard total increases by exactly 1 per completed game, never
      more (re-fetching `GET /api/scoreboard` repeatedly should not change
      the count).
- [ ] **Session isolation** — open the app in two different browsers (or
      one normal + one incognito window, since each generates its own
      session id on page load). Play a game to completion in one; confirm
      the scoreboard in the other window is **not** affected.

### Move history

- [ ] Confirm the move list grows by one entry per move, in order, with
      correct player and 1-based row/col.
- [ ] Confirm undo removes the correct number of trailing entries (1 in
      Two Player, 2 in VsComputer).
- [ ] Confirm Reset Board empties the list.

### Error handling

- [ ] **Invalid move — occupied cell**: attempt to play a cell that's
      already taken; expect `400 Bad Request`.
- [ ] **Invalid move — out of range**: send `row`/`col` outside `0-2`;
      expect `400 Bad Request`.
- [ ] **Nonexistent game id**: call any `/api/game/{id}/...` route with a
      random GUID; expect `404 Not Found`.
- [ ] **Missing/invalid session header**: call `POST /api/game` or either
      scoreboard endpoint without a valid `X-Session-Id` GUID header;
      expect `400 Bad Request`.

## Automated verification (backend logic)

The `.NET` backend was not run directly in the environment where this
project was first built (no `.NET` SDK was available there), so its core
algorithms — win/draw detection, the minimax computer opponent,
scoreboard exactly-once counting, and undo behavior in both modes — were
each independently verified by porting the exact logic to standalone
Node.js scripts and asserting expected outcomes (win rates, counting
guarantees, turn restoration, etc.) before being written into the C#
services. If you want to re-verify any of this yourself once the backend
runs on your machine, the checklist above exercises every one of those
code paths directly through the real API.

# Tic Tac Toe — Node.js/TypeScript Frontend + .NET Web API Backend

A simple two-player (local, same-screen) Tic Tac Toe game. All game logic —
move validation, win/draw detection, turn switching — lives in the .NET API.
The frontend is a thin TypeScript client that renders the board and calls
the API for every move.

```
tictactoe-app/
├── backend/
│   └── TicTacToe.Api/          .NET 8 Web API
│       ├── Controllers/        GameController — REST endpoints
│       ├── Models/             GameState, MoveRequest, etc.
│       ├── Services/           GameService — in-memory game store + rules
│       └── Program.cs          Hosting, CORS, Swagger setup
└── frontend/                   Node.js + TypeScript
    ├── src/
    │   ├── server.ts           Static file server (Node http/fs, no deps)
    │   └── client.ts           Browser game client (fetch-based, talks to API)
    └── public/
        ├── index.html
        └── styles.css
```

## How it works

1. The **.NET API** (`backend/TicTacToe.Api`) owns all game state in memory,
   keyed by a `Guid` game id. It exposes:

   | Method | Route                        | Description                          |
   |--------|-------------------------------|---------------------------------------|
   | POST   | `/api/game`                  | Create a new game. Body: `{ "mode": 0 }` (0 = TwoPlayer, 1 = VsComputer). Omit body for TwoPlayer. |
   | GET    | `/api/game/{id}`             | Get current state of a game          |
   | POST   | `/api/game/{id}/move`        | Body: `{ "row": 0-2, "col": 0-2 }`   |
   | POST   | `/api/game/{id}/undo`        | Undo the last move (see below)       |
   | POST   | `/api/game/{id}/reset`       | Reset the board, keep the same id and mode |

   **Undo Move** behaves differently by mode:
   - **TwoPlayer**: undoes only the single last move, returning the turn to
     whichever player made it.
   - **VsComputer**: undoes the computer's move *and* the human move that
     triggered it, together as one unit — since the computer always replies
     automatically, undoing anything less would leave a dangling computer
     move with no corresponding human decision to reconsider.

   Undo is only allowed while a game is still in progress — once a game has
   ended its result is already recorded on the scoreboard (see below), and
   reversing that would mean decrementing scoreboard counts, which is out
   of scope here. Attempting to undo a finished game, or a game with no
   moves yet, returns `400 Bad Request`. The response's `canUndo` field
   tells the frontend when the Undo button should be enabled, so it doesn't
   need to track move counts itself.

   Every `GameState` response also includes a `history` array — one entry
   per move made so far (`moveNumber`, `row`, `col`, `player`), derived live
   from the same internal move list Undo uses. It automatically shrinks on
   Undo and empties on Reset — there's no separate history-tracking code to
   keep in sync.

   In **VsComputer** mode, the human is always **X** and moves first. After
   each human move, `GameService` automatically computes and applies the
   computer's move (playing **O**) using **minimax search** — the computer
   plays perfectly and will never lose (it wins whenever the human gives it
   an opening, otherwise it draws). Since Tic Tac Toe has only 9 cells, the
   full game tree is searched exhaustively on every move; no pruning is
   needed for it to be instant.

   A separate **scoreboard** API tracks session-level X wins / O wins /
   Draws:

   | Method | Route                  | Description                                   |
   |--------|-------------------------|------------------------------------------------|
   | GET    | `/api/scoreboard`      | Get the current session's X/O/draw totals      |
   | POST   | `/api/scoreboard/reset`| Reset the current session's totals to zero     |

   Both require an `X-Session-Id` header (a GUID) identifying the browser
   session — same header the frontend already sends when creating a game.
   A game's result is recorded on its session's scoreboard **exactly once**,
   the moment the game ends (win or draw), guarded by an internal
   `ScoreCounted` flag on the game so it can never be double-counted.
   **Reset Board** (`/api/game/{id}/reset`) never touches the scoreboard —
   it only clears the 3x3 grid and starts a fresh round under the same game
   id. **Reset Scoreboard** is a separate, explicit action.

2. The **Node/TypeScript frontend** (`frontend/`) compiles to two pieces:
   - `dist/server.ts` → a plain Node `http` static file server (no Express,
     no external runtime dependencies) that serves `public/`.
   - `src/client.ts` → compiled to `public/client.js`, runs in the browser,
     renders the board and scoreboard, and calls the API directly via
     `fetch`. Two buttons ("2 Player" / "vs Computer") start a new game in
     the chosen mode; a random session id is generated once per page load
     and sent as `X-Session-Id` on every request that needs it.

## Prerequisites

- **.NET 8 SDK** — https://dotnet.microsoft.com/download
- **Node.js 18+** and npm

## Running the backend

```bash
cd backend/TicTacToe.Api
dotnet restore
dotnet run
```

The API starts on **http://localhost:5000** (see `Properties/launchSettings.json`).
Swagger UI is available at `http://localhost:5000/swagger` in Development mode.

## Running the frontend

```bash
cd frontend
npm install
npm run build   # compiles both server.ts and client.ts
npm start        # serves the app on http://localhost:3000
```

Then open **http://localhost:3000** in your browser. The page immediately
creates a new game against the API and lets you play.

> During development, run `npm run dev` to build and start in one step.

## Configuration notes

- **CORS**: the API's `Program.cs` allows `http://localhost:3000` (and
  `127.0.0.1:3000`) by default. If you host the frontend elsewhere, update
  the `WithOrigins(...)` call in `Program.cs`.
- **API URL**: the frontend points at `http://localhost:5000/api/game` via
  the `API_BASE_URL` constant at the top of `src/client.ts`. Update this if
  you deploy the API elsewhere, then re-run `npm run build:client`.
- **Game store**: `GameService` keeps games in an in-memory
  `ConcurrentDictionary`. Games are lost on API restart and won't be shared
  across multiple API instances — swap in Redis or a database if you need
  either of those for a real deployment.

## Extending it

- Add a "Play again" flow that reuses `/reset` instead of creating a brand
  new game id.
- Add a difficulty setting for the computer opponent (e.g. an "Easy" mode
  that occasionally picks a random move instead of the optimal one).
- Persist finished games (winner, timestamp) to a database for a match
  history view.

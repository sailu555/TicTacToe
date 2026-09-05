# Tic Tac Toe — API Contract

Base URL (default local run): `http://localhost:5000`

All request/response bodies are JSON. All responses use camelCase field
names (ASP.NET Core's default JSON casing).

---

## Authentication / Session header

There is no user authentication. Instead, a lightweight **session** concept
scopes the scoreboard to one browser tab:

| Header | Required on | Format |
|---|---|---|
| `X-Session-Id` | `POST /api/game`, `GET /api/scoreboard`, `POST /api/scoreboard/reset` | A GUID string, e.g. `3fa85f64-5717-4562-b3fc-2c963f66afa6` |

The frontend generates this once per page load (`crypto.randomUUID()`) and
reuses it for the lifetime of that tab. Any client can generate its own —
the backend doesn't issue or validate identity beyond "is this a
well-formed GUID."

`GET /api/game/{id}`, `POST /api/game/{id}/move`, `POST /api/game/{id}/undo`,
and `POST /api/game/{id}/reset` do **not** require this header — the game
already remembers which session created it.

Missing or malformed `X-Session-Id` on an endpoint that requires it returns:

```json
// 400 Bad Request
{ "message": "A valid 'X-Session-Id' header (GUID) is required." }
```

---

## Enums

| Enum | Values | Wire format |
|---|---|---|
| `CellValue` | `Empty = 0`, `X = 1`, `O = 2` | **Number** |
| `GameMode` | `TwoPlayer = 0`, `VsComputer = 1` | **Number** |
| `GameStatus` | `InProgress`, `Won`, `Draw` | **String** (e.g. `"InProgress"`) |

> `GameStatus` is deliberately serialized as a string
> (`[JsonConverter(typeof(JsonStringEnumConverter))]` on that enum only) for
> readability. `CellValue` and `GameMode` remain numeric — changing that
> would be a breaking change for any client comparing against the numeric
> values shown above.

---

## Data models

### `GameState`

Returned by every game endpoint (create, get, move, undo, reset).

| Field | Type | Description |
|---|---|---|
| `id` | `string` (GUID) | This game's unique id |
| `mode` | `GameMode` (number) | `0` = TwoPlayer, `1` = VsComputer |
| `status` | `GameStatus` (string) | `"InProgress"`, `"Won"`, or `"Draw"` |
| `board` | `CellValue[9]` | Flat row-major array; index `i` = row `i/3`, col `i%3` |
| `currentPlayer` | `CellValue` (number) | Whose turn it is (irrelevant once the game has ended) |
| `winner` | `CellValue \| null` | Set once `status` is `"Won"`; otherwise `null` |
| `isDraw` | `boolean` | `true` once the game ends in a draw |
| `isGameOver` | `boolean` | `true` if either `winner` is set or `isDraw` is `true` |
| `canUndo` | `boolean` | Whether `POST /undo` would currently succeed |
| `winningLine` | `int[3] \| null` | The 3 winning cell indices, once there's a winner; otherwise `null` |
| `history` | `MoveHistoryEntry[]` | Chronological list of every move made so far |
| `scoreboard` | `ScoreboardState` | The current session's running totals, embedded for convenience |

> Internal-only fields (`sessionId`, `scoreCounted`, the raw `moveHistory`
> used for undo bookkeeping) are marked `[JsonIgnore]` in the backend and
> never appear in responses.

**Example:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "mode": 0,
  "status": "InProgress",
  "board": [1, 0, 0, 0, 2, 0, 0, 0, 0],
  "currentPlayer": 1,
  "winner": null,
  "isDraw": false,
  "isGameOver": false,
  "canUndo": true,
  "winningLine": null,
  "history": [
    { "moveNumber": 1, "row": 0, "col": 0, "player": 1 },
    { "moveNumber": 2, "row": 1, "col": 1, "player": 2 }
  ],
  "scoreboard": { "xWins": 0, "oWins": 0, "draws": 0 }
}
```

### `MoveHistoryEntry`

| Field | Type | Description |
|---|---|---|
| `moveNumber` | `number` | 1-based sequence number |
| `row` | `number` | `0-2` |
| `col` | `number` | `0-2` |
| `player` | `CellValue` (number) | Who made this move |

### `ScoreboardState`

| Field | Type | Description |
|---|---|---|
| `xWins` | `number` | Total games won by X this session |
| `oWins` | `number` | Total games won by O this session |
| `draws` | `number` | Total draws this session |

### `ApiError`

Returned for all `400`/`404` responses.

| Field | Type |
|---|---|
| `message` | `string` |

---

## Endpoints

### `POST /api/game`

Create a new game.

- **Headers:** `X-Session-Id` (required)
- **Body** (optional):
  ```json
  { "mode": 0 }
  ```
  Omit the body entirely, or omit `mode`, to default to TwoPlayer (`0`).
- **Response:** `201 Created` with a fresh `GameState`.
- **Errors:** `400` if `X-Session-Id` is missing/invalid.

### `GET /api/game/{id}`

Get the current state of a game.

- **Response:** `200 OK` with `GameState`.
- **Errors:** `404 Not Found` if `{id}` doesn't exist.

### `POST /api/game/{id}/move`

Apply a move for the current player.

- **Body:**
  ```json
  { "row": 0, "col": 0 }
  ```
  Both `row` and `col` must be `0-2`.
- **Response:** `200 OK` with the updated `GameState`. In VsComputer mode,
  if the game is still in progress after the human's move, the computer's
  reply is computed (via minimax) and applied **before** this response is
  returned — one API call covers both half-moves.
- **Errors:**
  - `400` — cell already occupied, row/col out of range, or the game has
    already ended.
  - `400` — attempting to move when it's the computer's turn (shouldn't
    normally be reachable through the frontend, but guarded server-side).
  - `404` — `{id}` doesn't exist.

### `POST /api/game/{id}/undo`

Undo the most recent move(s).

- **Body:** none.
- **Behavior:**
  - **TwoPlayer**: undoes exactly 1 move — the last one played — and
    returns the turn to whichever player made it.
  - **VsComputer**: undoes exactly 2 moves — the computer's move and the
    human move that triggered it — together as one unit, returning the
    turn to the human.
- **Response:** `200 OK` with the updated `GameState`.
- **Errors:**
  - `400` — the game has already ended (undo is only valid while a game
    is in progress; ending a game finalizes its scoreboard entry, and
    undoing past that point would require reversing that count, which
    this API does not support).
  - `400` — not enough moves recorded yet to undo (e.g. undo on a
    freshly created game).
  - `404` — `{id}` doesn't exist.

### `POST /api/game/{id}/reset`

Reset a game's board to empty, keeping the same id and mode.

- **Body:** none.
- **Response:** `200 OK` with a fresh `GameState` (same `id`, same `mode`,
  empty board, empty `history`, `canUndo: false`).
- **Note:** this does **not** modify the scoreboard, regardless of whether
  the game being reset had already ended.
- **Errors:** `404` if `{id}` doesn't exist.

### `GET /api/scoreboard`

Get the current session's running totals.

- **Headers:** `X-Session-Id` (required)
- **Response:** `200 OK` with `ScoreboardState`. If this session has no
  recorded results yet, returns all zeros (an entry is not required to
  exist beforehand).
- **Errors:** `400` if `X-Session-Id` is missing/invalid.

### `POST /api/scoreboard/reset`

Reset the current session's scoreboard to zero.

- **Headers:** `X-Session-Id` (required)
- **Body:** none.
- **Response:** `200 OK` with the zeroed `ScoreboardState`.
- **Note:** this does not affect any in-progress game.
- **Errors:** `400` if `X-Session-Id` is missing/invalid.

---

## Business rules summary

These aren't separate endpoints, but are contract-level guarantees worth
documenting explicitly since they were specific requirements during
development:

1. **Exactly-once scoring**: a game's result is recorded on its session's
   scoreboard the instant it ends, guarded so it can never be counted
   twice — regardless of how many times the game is subsequently fetched.
2. **Reset Board never touches the scoreboard** — only `POST /scoreboard/reset`
   does.
3. **The computer opponent plays optimally** (minimax over the full game
   tree) — it will never lose, only win or draw.
4. **Undo is mode-aware**: 1 move in TwoPlayer, 2 moves (a full round) in
   VsComputer — see `POST /api/game/{id}/undo` above.
5. **Sessions are isolated**: two different `X-Session-Id` values never
   share scoreboard state.

/**
 * Tic Tac Toe browser client.
 * Renders the 3x3 board and calls the .NET Web API backend for every
 * game action (create, move, reset). No game logic lives in the browser
 * beyond rendering — the backend is the single source of truth.
 */

// Change this if the .NET API is hosted somewhere other than localhost:5000.
const API_BASE_URL = "http://localhost:5000/api/games";
const SCOREBOARD_URL = "http://localhost:5000/api/scoreboard";

// One session id per page load, sent as a header so the backend can
// attribute completed games to this browser tab's scoreboard.
const SESSION_ID = crypto.randomUUID();
const SESSION_HEADERS = { "X-Session-Id": SESSION_ID };

// Mirrors the backend's CellValue enum (0 = Empty, 1 = X, 2 = O).
const enum CellValue {
  Empty = 0,
  X = 1,
  O = 2,
}

// Mirrors the backend's GameMode enum.
const enum GameMode {
  TwoPlayer = 0,
  VsComputer = 1,
}
// Add this new enum near GameMode:
const enum GameStatus {
  InProgress = 0,
  Won = 1,
  Draw = 2,
}

interface MoveHistoryEntry {
  moveNumber: number;
  row: number;
  col: number;
  player: CellValue;
}

interface GameState {
  id: string;
  mode: GameMode;
  status: GameStatus;  
  board: CellValue[];
  currentPlayer: CellValue;
  winner: CellValue | null;
  isDraw: boolean;
  isGameOver: boolean;
  canUndo: boolean;
  winningLine: number[] | null;
  history: MoveHistoryEntry[];
  scoreboard: ScoreboardState; 
}

interface ScoreboardState {
  xWins: number;
  oWins: number;
  draws: number;
}

interface ApiError {
  message: string;
}

let currentGame: GameState | null = null;
let selectedMode: GameMode = GameMode.TwoPlayer;

const boardEl = document.getElementById("board") as HTMLDivElement;
const statusEl = document.getElementById("status") as HTMLDivElement;
const modeTwoPlayerBtn = document.getElementById("mode-two-player") as HTMLButtonElement;
const modeVsComputerBtn = document.getElementById("mode-vs-computer") as HTMLButtonElement;
const resetBtn = document.getElementById("reset") as HTMLButtonElement;
const undoBtn = document.getElementById("undo") as HTMLButtonElement;
const resetScoreboardBtn = document.getElementById("reset-scoreboard") as HTMLButtonElement;
const scoreXEl = document.getElementById("score-x") as HTMLSpanElement;
const scoreOEl = document.getElementById("score-o") as HTMLSpanElement;
const scoreDrawEl = document.getElementById("score-draws") as HTMLSpanElement;
const moveHistoryListEl = document.getElementById("move-history-list") as HTMLOListElement;

function cellLabel(value: CellValue): string {
  if (value === CellValue.X) return "X";
  if (value === CellValue.O) return "O";
  return "";
}

function playerLabel(value: CellValue, mode: GameMode): string {
  if (mode === GameMode.VsComputer && value === CellValue.O) return "Computer";
  return cellLabel(value);
}

function updateModeButtonStyles(): void {
  modeTwoPlayerBtn.classList.toggle("mode-active", selectedMode === GameMode.TwoPlayer);
  modeVsComputerBtn.classList.toggle("mode-active", selectedMode === GameMode.VsComputer);
}

function renderBoard(game: GameState): void {
  boardEl.innerHTML = "";

  game.board.forEach((cellValue, index) => {
    const cellEl = document.createElement("button");
    cellEl.type = "button";
    cellEl.className = "cell";
    cellEl.textContent = cellLabel(cellValue);
    //cellEl.disabled = cellValue !== CellValue.Empty || game.isGameOver;
    cellEl.disabled = game.isGameOver;

    if (game.winningLine?.includes(index)) {
      cellEl.classList.add("winning-cell");
    }

    const row = Math.floor(index / 3);
    const col = index % 3;
    cellEl.addEventListener("click", () => handleMove(row, col));

    boardEl.appendChild(cellEl);
  });

  renderStatus(game);
  renderMoveHistory(game.history);
  undoBtn.disabled = !game.canUndo;
}

function renderMoveHistory(history: MoveHistoryEntry[]): void {
  moveHistoryListEl.innerHTML = "";

  if (history.length === 0) {
    const emptyItem = document.createElement("li");
    emptyItem.className = "move-history-empty";
    emptyItem.textContent = "No moves yet";
    moveHistoryListEl.appendChild(emptyItem);
    return;
  }

  for (const entry of history) {
    const item = document.createElement("li");
    item.textContent = `${cellLabel(entry.player)} → Row ${entry.row + 1}, Col ${entry.col + 1}`;
   // item.textContent = `${entry.moveNumber}. ${cellLabel(entry.player)} → Row ${entry.row + 1}, Col ${entry.col + 1}`;
    moveHistoryListEl.appendChild(item);
  }

  // Keep the most recent move in view as the list grows.
  moveHistoryListEl.scrollTop = moveHistoryListEl.scrollHeight;
}

function renderStatus(game: GameState): void {
  if (game.winner !== null) {
    statusEl.textContent = `${playerLabel(game.winner, game.mode)} wins! 🎉`;
    statusEl.className = "status status-winner";
  } else if (game.isDraw) {
    statusEl.textContent = "It's a draw!";
    statusEl.className = "status status-draw";
  } else {
    statusEl.textContent = `${playerLabel(game.currentPlayer, game.mode)}'s turn`;
    statusEl.className = "status";
  }
}

function renderScoreboard(scoreboard: ScoreboardState): void {
  scoreXEl.textContent = String(scoreboard.xWins);
  scoreOEl.textContent = String(scoreboard.oWins);
  scoreDrawEl.textContent = String(scoreboard.draws);
}

async function refreshScoreboard(): Promise<void> {
  try {
    const response = await fetch(SCOREBOARD_URL, {
      method: "GET",
      headers: SESSION_HEADERS,
    });
    const scoreboard = await parseJsonOrThrow<ScoreboardState>(response);
    renderScoreboard(scoreboard);
  } catch (err) {
    // Non-fatal: the game itself still works even if the scoreboard fetch
    // fails, so just log it rather than surfacing a connection error.
    console.error("Failed to refresh scoreboard:", err);
  }
}

async function resetScoreboard(): Promise<void> {
  try {
    const response = await fetch(`${SCOREBOARD_URL}/reset`, {
      method: "POST",
      headers: SESSION_HEADERS,
    });
    const scoreboard = await parseJsonOrThrow<ScoreboardState>(response);
    renderScoreboard(scoreboard);
  } catch (err) {
    console.error("Failed to reset scoreboard:", err);
  }
}

async function parseJsonOrThrow<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = (await response.json().catch(() => null)) as ApiError | null;
    throw new Error(error?.message ?? `Request failed with status ${response.status}`);
  }
  return response.json() as Promise<T>;
}

async function createNewGame(mode: GameMode): Promise<void> {
  try {
    selectedMode = mode;
    updateModeButtonStyles();
    statusEl.textContent = "Starting new game…";
    const response = await fetch(API_BASE_URL, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...SESSION_HEADERS },
      body: JSON.stringify({ mode }),
    });
    currentGame = await parseJsonOrThrow<GameState>(response);
    renderBoard(currentGame);
    void refreshScoreboard();
  } catch (err) {
    showConnectionError(err);
  }
}

async function handleMove(row: number, col: number): Promise<void> {
  if (!currentGame || currentGame.isGameOver) return;

  const index = row * 3 + col;
  if (currentGame.board[index] !== CellValue.Empty) {
    statusEl.textContent = "That cell is already taken — pick an empty one.";
    statusEl.className = "status status-error";
    return;
  }
  try {
    const response = await fetch(`${API_BASE_URL}/${currentGame.id}/move`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ row, col }),
    });
    currentGame = await parseJsonOrThrow<GameState>(response);
    renderBoard(currentGame);

    // Only the scoreboard changed by this move matters here — refreshing
    // unconditionally is cheap and keeps the display correct even if a
    // future change alters exactly when games can end.
    if (currentGame.isGameOver) {
      void refreshScoreboard();
    }
  } catch (err) {
    showConnectionError(err);
  }
}

async function handleUndo(): Promise<void> {
  if (!currentGame) return;

  try {
    const response = await fetch(`${API_BASE_URL}/${currentGame.id}/undo`, {
      method: "POST",
    });
    currentGame = await parseJsonOrThrow<GameState>(response);
    renderBoard(currentGame);
  } catch (err) {
    showConnectionError(err);
  }
}

async function resetCurrentGame(): Promise<void> {
  if (!currentGame) {
    await createNewGame(selectedMode);
    return;
  }

  try {
    const response = await fetch(`${API_BASE_URL}/${currentGame.id}/reset`, {
      method: "POST",
    });
    currentGame = await parseJsonOrThrow<GameState>(response);
    renderBoard(currentGame);
  } catch (err) {
    showConnectionError(err);
  }
}

function showConnectionError(err: unknown): void {
  const message = err instanceof Error ? err.message : "Unknown error";
  statusEl.textContent = `⚠️ ${message}`;
  statusEl.className = "status status-error";
  console.error(err);
}

modeTwoPlayerBtn.addEventListener("click", () => void createNewGame(GameMode.TwoPlayer));
modeVsComputerBtn.addEventListener("click", () => void createNewGame(GameMode.VsComputer));
resetBtn.addEventListener("click", () => void resetCurrentGame());
undoBtn.addEventListener("click", () => void handleUndo());
resetScoreboardBtn.addEventListener("click", () => void resetScoreboard());

// Kick off a fresh two-player game as soon as the page loads.
updateModeButtonStyles();
void createNewGame(selectedMode);
void refreshScoreboard();

using System.Text.Json.Serialization;

namespace TicTacToe.Api.Models;

/// <summary>
/// The value held in a single board cell.
/// </summary>
public enum CellValue
{
    Empty = 0,
    X = 1,
    O = 2
}

/// <summary>
/// Whether a game is two humans taking turns, or a human vs. the computer AI.
/// </summary>
public enum GameMode
{
    TwoPlayer = 0,
    VsComputer = 1
}

/// <summary>
/// High-level status of a game, derived from Winner/IsDraw. Provided as an
/// explicit enum (rather than making the client infer it from Winner +
/// IsDraw) so consumers have one clear field to check.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameStatus
{
    InProgress = 0,
    Won = 1,
    Draw = 2
}

/// <summary>
/// Full state of a single Tic Tac Toe game, returned to the client
/// after every create / move / reset operation.
/// </summary>
/// <summary>
/// A single applied move, kept so Undo can reverse it precisely (which
/// cell, and which player made it).
/// </summary>
public class MoveRecord
{
    public int Index { get; set; }
    public CellValue Player { get; set; }
}

/// <summary>
/// Client-friendly view of a single move, derived from MoveRecord for the
/// "Move History" display (1-based move number, row/col instead of a flat
/// index).
/// </summary>
public class MoveHistoryEntry
{
    public int MoveNumber { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public CellValue Player { get; set; }
}

public class GameState
{
    public Guid Id { get; set; }

    /// <summary>The session (browser tab) this game belongs to — used to
    /// attribute its final result to the right scoreboard.</summary>
    [JsonIgnore]
    public Guid SessionId { get; set; }

    /// <summary>Set to true the moment this game's result has been recorded
    /// on the scoreboard, so it is never counted a second time (e.g. if the
    /// client re-fetches state after the game has already ended).</summary>
    [JsonIgnore]
    public bool ScoreCounted { get; set; }

    /// <summary>Chronological record of every move applied so far, used by
    /// Undo. Not sent to the client — only the resulting board state is.</summary>
    [JsonIgnore]
    public List<MoveRecord> MoveHistory { get; set; } = new();

    public GameMode Mode { get; set; } = GameMode.TwoPlayer;

    /// <summary>3x3 board, row-major. Serialized as a flat array of 9 values.</summary>
    public CellValue[] Board { get; set; } = new CellValue[9];

    public CellValue CurrentPlayer { get; set; } = CellValue.X;

    /// <summary>Null while the game is in progress.</summary>
    public CellValue? Winner { get; set; }

    public bool IsDraw { get; set; }

    public bool IsGameOver => Winner is not null || IsDraw;

    /// <summary>Explicit status enum version of IsGameOver/Winner/IsDraw,
    /// for consumers that prefer a single field over checking three.</summary>
    public GameStatus Status =>
        Winner is not null ? GameStatus.Won :
        IsDraw ? GameStatus.Draw :
        GameStatus.InProgress;

    /// <summary>Whether Undo Move is currently valid: the game must still
    /// be in progress, and there must be enough recorded moves to undo (1 in
    /// TwoPlayer mode, 2 — the human+computer pair — in VsComputer mode).</summary>
    public bool CanUndo =>
        !IsGameOver && MoveHistory.Count >= (Mode == GameMode.VsComputer ? 2 : 1);

    /// <summary>Indices (0-8) of the three cells that formed the winning line, if any.</summary>
    public int[]? WinningLine { get; set; }

    /// <summary>
    /// Client-facing move history, derived from MoveHistory. Always reflects
    /// the game's current state — shrinks on Undo, empties on Reset, since
    /// it's computed fresh from MoveHistory rather than tracked separately.
    /// </summary>
    public List<MoveHistoryEntry> History =>
        MoveHistory
            .Select((move, i) => new MoveHistoryEntry
            {
                MoveNumber = i + 1,
                Row = move.Index / 3,
                Col = move.Index % 3,
                Player = move.Player
            })
            .ToList();

    /// <summary>
    /// The session's current scoreboard, attached by GameService on every
    /// response (create/move/undo/reset/get) so clients don't need a
    /// separate call to /api/scoreboard just to show it alongside the board.
    /// </summary>
    public ScoreboardState? Scoreboard { get; set; }
}

/// <summary>
/// Request body for POST /api/game. Mode defaults to TwoPlayer if omitted.
/// In VsComputer mode, the human is always X and moves first; the computer
/// plays O and moves automatically right after each human move.
/// </summary>
public class CreateGameRequest
{
    public GameMode Mode { get; set; } = GameMode.TwoPlayer;
}

/// <summary>
/// Request body for POST /api/game/{id}/move
/// </summary>
public class MoveRequest
{
    public int Row { get; set; }
    public int Col { get; set; }
}

/// <summary>
/// Standard error payload returned for invalid moves / bad requests.
/// </summary>
public class ApiError
{
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Session-level running totals. One instance per session id, held by
/// IScoreboardService — not persisted, resets when the API process restarts.
/// </summary>
public class ScoreboardState
{
    public int XWins { get; set; }
    public int OWins { get; set; }
    public int Draws { get; set; }
}
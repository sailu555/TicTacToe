using System.Collections.Concurrent;
using TicTacToe.Api.Models;

namespace TicTacToe.Api.Services;

public interface IGameService
{
    GameState CreateGame(Guid sessionId, GameMode mode = GameMode.TwoPlayer);
    GameState? GetGame(Guid id);
    GameState MakeMove(Guid id, int row, int col);
    GameState UndoMove(Guid id);
    GameState ResetGame(Guid id);
}

/// <summary>
/// Thrown when a requested game id does not exist.
/// </summary>
public class GameNotFoundException : Exception
{
    public GameNotFoundException(Guid id) : base($"Game '{id}' was not found.") { }
}

/// <summary>
/// Thrown for any illegal move (out of range, occupied cell, game already over).
/// </summary>
public class InvalidMoveException : Exception
{
    public InvalidMoveException(string message) : base(message) { }
}

/// <summary>
/// In-memory game store. Suitable for a demo / single-instance deployment;
/// swap for a persistent store (e.g. Redis, a database) for production use
/// across multiple server instances.
/// </summary>
public class GameService : IGameService
{
    private readonly ConcurrentDictionary<Guid, GameState> _games = new();
    private readonly IScoreboardService _scoreboardService;

    public GameService(IScoreboardService scoreboardService)
    {
        _scoreboardService = scoreboardService;
    }

    // All 8 possible winning lines, expressed as flat-array indices (0-8).
    private static readonly int[][] WinningLines =
    {
        new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, // rows
        new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 }, // columns
        new[] { 0, 4, 8 }, new[] { 2, 4, 6 }                     // diagonals
    };

    // In VsComputer mode, the human is always X and the computer is always O.
    private const CellValue ComputerSymbol = CellValue.O;
    private const CellValue HumanSymbol = CellValue.X;

    public GameState CreateGame(Guid sessionId, GameMode mode = GameMode.TwoPlayer)
    {
        var game = new GameState
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ScoreCounted = false,
            Mode = mode,
            Board = new CellValue[9],
            CurrentPlayer = CellValue.X,
            Winner = null,
            IsDraw = false,
            WinningLine = null
        };

        _games[game.Id] = game;
        return AttachScoreboard(game);
    }

    public GameState? GetGame(Guid id)
    {
        return _games.TryGetValue(id, out var game) ? AttachScoreboard(game) : null;
    }

    public GameState MakeMove(Guid id, int row, int col)
    {
        var game = _games.TryGetValue(id, out var g) ? g : throw new GameNotFoundException(id);

        if (game.IsGameOver)
        {
            throw new InvalidMoveException("This game has already ended. Start a new game to keep playing.");
        }

        if (game.Mode == GameMode.VsComputer && game.CurrentPlayer == ComputerSymbol)
        {
            throw new InvalidMoveException("It's the computer's turn.");
        }

        ApplyMove(game, row, col);

        // If playing against the computer and the game is still going after
        // the human's move, let the computer take its turn immediately so
        // the client gets back a state where it's the human's turn again
        // (or the game has ended).
        if (game.Mode == GameMode.VsComputer && !game.IsGameOver && game.CurrentPlayer == ComputerSymbol)
        {
            var (bestRow, bestCol) = GetBestComputerMove(game.Board);
            ApplyMove(game, bestRow, bestCol);
        }

        return AttachScoreboard(game);
    }

    /// <summary>
    /// Applies a single move to the board for the current player, then
    /// updates winner/draw/turn state. Assumes the move has NOT yet been
    /// validated as legal for row/col bounds; occupied-cell and game-over
    /// checks are the caller's responsibility for human moves (the computer
    /// only ever proposes free cells, by construction of the minimax search).
    /// </summary>
    private void ApplyMove(GameState game, int row, int col)
    {
        if (row is < 0 or > 2 || col is < 0 or > 2)
        {
            throw new InvalidMoveException("Row and column must each be between 0 and 2.");
        }

        var index = row * 3 + col;

        if (game.Board[index] != CellValue.Empty)
        {
            throw new InvalidMoveException("That cell is already occupied.");
        }

        game.Board[index] = game.CurrentPlayer;
        game.MoveHistory.Add(new MoveRecord { Index = index, Player = game.CurrentPlayer });

        var (winner, winningLine) = CheckWinner(game.Board);
        if (winner is not null)
        {
            game.Winner = winner;
            game.WinningLine = winningLine;
        }
        else if (game.Board.All(cell => cell != CellValue.Empty))
        {
            game.IsDraw = true;
        }
        else
        {
            game.CurrentPlayer = game.CurrentPlayer == CellValue.X ? CellValue.O : CellValue.X;
        }

        // Record the result on the session's scoreboard the moment the game
        // ends — guarded by ScoreCounted so this can only ever fire once per
        // game, no matter how many times ApplyMove or MakeMove is called
        // afterward (MakeMove already refuses moves once IsGameOver is true,
        // but the flag makes the exactly-once guarantee explicit and safe
        // even if that changes later).
        if (game.IsGameOver && !game.ScoreCounted)
        {
            _scoreboardService.RecordResult(game.SessionId, game.Winner, game.IsDraw);
            game.ScoreCounted = true;
        }
    }

    /// <summary>
    /// Undoes the most recent move(s):
    /// - TwoPlayer mode: undoes only the single last move, returning the
    ///   turn to the player who made it.
    /// - VsComputer mode: undoes the computer's reply AND the human move
    ///   that triggered it, as one unit — returning the turn to the human.
    /// Only allowed while the game is still in progress. Once a game has
    /// ended, its result has already been recorded on the scoreboard (see
    /// ApplyMove), and undoing a finished game would require reversing that
    /// scoreboard entry too — out of scope here, so it's simply disallowed.
    /// </summary>
    public GameState UndoMove(Guid id)
    {
        var game = _games.TryGetValue(id, out var g) ? g : throw new GameNotFoundException(id);

        if (game.IsGameOver)
        {
            throw new InvalidMoveException(
                "Cannot undo — this game has already ended. Start a new game or reset the board.");
        }

        var movesToUndo = game.Mode == GameMode.VsComputer ? 2 : 1;

        if (game.MoveHistory.Count < movesToUndo)
        {
            throw new InvalidMoveException("No moves to undo yet.");
        }

        for (var i = 0; i < movesToUndo; i++)
        {
            var lastMove = game.MoveHistory[^1];
            game.MoveHistory.RemoveAt(game.MoveHistory.Count - 1);
            game.Board[lastMove.Index] = CellValue.Empty;

            // Restores turn to whoever made the move being undone. In
            // VsComputer mode this loop runs twice (computer's move, then
            // the human's move beneath it), so it correctly ends with the
            // turn back on the human (X).
            game.CurrentPlayer = lastMove.Player;
        }

        // Undoing from a non-terminal position can only produce another
        // non-terminal position, so these are already null/false — reset
        // explicitly anyway for clarity and safety.
        game.Winner = null;
        game.IsDraw = false;
        game.WinningLine = null;

        return AttachScoreboard(game);
    }

    public GameState ResetGame(Guid id)
    {
        if (!_games.TryGetValue(id, out var existing))
        {
            throw new GameNotFoundException(id);
        }

        var fresh = new GameState
        {
            Id = id,
            SessionId = existing.SessionId,
            ScoreCounted = false,
            MoveHistory = new List<MoveRecord>(),
            Mode = existing.Mode,
            Board = new CellValue[9],
            CurrentPlayer = CellValue.X,
            Winner = null,
            IsDraw = false,
            WinningLine = null
        };

        _games[id] = fresh;
        return AttachScoreboard(fresh);
    }

    private GameState AttachScoreboard(GameState game)
    {
        game.Scoreboard = _scoreboardService.GetScoreboard(game.SessionId);
        return game;
    }

    private static (CellValue? winner, int[]? line) CheckWinner(CellValue[] board)
    {
        foreach (var line in WinningLines)
        {
            var a = board[line[0]];
            var b = board[line[1]];
            var c = board[line[2]];

            if (a != CellValue.Empty && a == b && b == c)
            {
                return (a, line);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Finds the optimal move for the computer (O) using minimax with a
    /// depth-based score, so it prefers faster wins and slower losses.
    /// With only 9 cells total, an exhaustive search is instant — no
    /// alpha-beta pruning or transposition table is needed.
    /// The computer plays perfectly: it never loses, and wins whenever
    /// the human gives it an opening.
    /// </summary>
    private static (int row, int col) GetBestComputerMove(CellValue[] board)
    {
        var bestScore = int.MinValue;
        var bestIndex = -1;

        for (var i = 0; i < board.Length; i++)
        {
            if (board[i] != CellValue.Empty) continue;

            board[i] = ComputerSymbol;
            var score = Minimax(board, depth: 0, isMaximizing: false);
            board[i] = CellValue.Empty;

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return (bestIndex / 3, bestIndex % 3);
    }

    private static int Minimax(CellValue[] board, int depth, bool isMaximizing)
    {
        var (winner, _) = CheckWinner(board);
        if (winner == ComputerSymbol) return 10 - depth;
        if (winner == HumanSymbol) return depth - 10;
        if (board.All(cell => cell != CellValue.Empty)) return 0;

        if (isMaximizing)
        {
            var best = int.MinValue;
            for (var i = 0; i < board.Length; i++)
            {
                if (board[i] != CellValue.Empty) continue;
                board[i] = ComputerSymbol;
                best = Math.Max(best, Minimax(board, depth + 1, isMaximizing: false));
                board[i] = CellValue.Empty;
            }
            return best;
        }
        else
        {
            var best = int.MaxValue;
            for (var i = 0; i < board.Length; i++)
            {
                if (board[i] != CellValue.Empty) continue;
                board[i] = HumanSymbol;
                best = Math.Min(best, Minimax(board, depth + 1, isMaximizing: true));
                board[i] = CellValue.Empty;
            }
            return best;
        }
    }
}
using System.Collections.Concurrent;
using TicTacToe.Api.Models;

namespace TicTacToe.Api.Services;

public interface IScoreboardService
{
    ScoreboardState GetScoreboard(Guid sessionId);
    ScoreboardState RecordResult(Guid sessionId, CellValue? winner, bool isDraw);
    ScoreboardState ResetScoreboard(Guid sessionId);
}

/// <summary>
/// In-memory, session-scoped scoreboard. Each browser session (see
/// SessionAwareControllerBase) gets its own independent X wins / O wins /
/// Draws tally, tracked here for the lifetime of the API process.
/// </summary>
public class ScoreboardService : IScoreboardService
{
    private readonly ConcurrentDictionary<Guid, ScoreboardState> _scores = new();

    public ScoreboardState GetScoreboard(Guid sessionId)
    {
        return _scores.GetOrAdd(sessionId, _ => new ScoreboardState());
    }

    public ScoreboardState RecordResult(Guid sessionId, CellValue? winner, bool isDraw)
    {
        // AddOrUpdate is atomic; the update factory returns a brand-new
        // ScoreboardState rather than mutating the existing one in place,
        // so concurrent callers can never observe a half-updated count.
        return _scores.AddOrUpdate(
            sessionId,
            addValueFactory: _ => ApplyResult(new ScoreboardState(), winner, isDraw),
            updateValueFactory: (_, existing) => ApplyResult(
                new ScoreboardState { XWins = existing.XWins, OWins = existing.OWins, Draws = existing.Draws },
                winner,
                isDraw));
    }

    public ScoreboardState ResetScoreboard(Guid sessionId)
    {
        var fresh = new ScoreboardState();
        _scores[sessionId] = fresh;
        return fresh;
    }

    private static ScoreboardState ApplyResult(ScoreboardState state, CellValue? winner, bool isDraw)
    {
        if (winner == CellValue.X) state.XWins++;
        else if (winner == CellValue.O) state.OWins++;
        else if (isDraw) state.Draws++;
        return state;
    }
}

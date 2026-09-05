using TicTacToe.Api.Models;
using TicTacToe.Api.Services;
using Xunit;

namespace TicTacToe.Api.Tests;

public class ScoreboardServiceTests
{
    [Fact]
    public void GetScoreboard_ForNewSession_ReturnsAllZeros()
    {
        var scoreboard = new ScoreboardService();

        var result = scoreboard.GetScoreboard(Guid.NewGuid());

        Assert.Equal(0, result.XWins);
        Assert.Equal(0, result.OWins);
        Assert.Equal(0, result.Draws);
    }

    [Fact]
    public void RecordResult_IncrementsCorrectCounter()
    {
        var scoreboard = new ScoreboardService();
        var sessionId = Guid.NewGuid();

        scoreboard.RecordResult(sessionId, CellValue.X, isDraw: false);
        scoreboard.RecordResult(sessionId, CellValue.O, isDraw: false);
        var result = scoreboard.RecordResult(sessionId, null, isDraw: true);

        Assert.Equal(1, result.XWins);
        Assert.Equal(1, result.OWins);
        Assert.Equal(1, result.Draws);
    }

    [Fact]
    public void ResetScoreboard_ZeroesCountsForThatSession()
    {
        var scoreboard = new ScoreboardService();
        var sessionId = Guid.NewGuid();
        scoreboard.RecordResult(sessionId, CellValue.X, isDraw: false);

        var reset = scoreboard.ResetScoreboard(sessionId);

        Assert.Equal(0, reset.XWins);
        Assert.Equal(0, reset.OWins);
        Assert.Equal(0, reset.Draws);
    }

    [Fact]
    public void Sessions_AreIsolatedFromEachOther()
    {
        var scoreboard = new ScoreboardService();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();

        scoreboard.RecordResult(sessionA, CellValue.X, isDraw: false);

        var scoreA = scoreboard.GetScoreboard(sessionA);
        var scoreB = scoreboard.GetScoreboard(sessionB);

        Assert.Equal(1, scoreA.XWins);
        Assert.Equal(0, scoreB.XWins);
    }

    // --- Integration with GameService: does a completed game count exactly once? ---

    [Fact]
    public void CompletedGame_IsCountedExactlyOnce_EvenAfterRepeatedFetches()
    {
        var scoreboardService = new ScoreboardService();
        var gameService = new GameService(scoreboardService);
        var sessionId = Guid.NewGuid();
        var game = gameService.CreateGame(sessionId);

        gameService.MakeMove(game.Id, 0, 0); // X
        gameService.MakeMove(game.Id, 1, 0); // O
        gameService.MakeMove(game.Id, 0, 1); // X
        gameService.MakeMove(game.Id, 1, 1); // O
        gameService.MakeMove(game.Id, 0, 2); // X wins

        // Re-fetch the finished game several times — must not re-trigger scoring.
        gameService.GetGame(game.Id);
        gameService.GetGame(game.Id);
        var scoreboard = scoreboardService.GetScoreboard(sessionId);

        Assert.Equal(1, scoreboard.XWins);
        Assert.Equal(0, scoreboard.OWins);
        Assert.Equal(0, scoreboard.Draws);
    }

    [Fact]
    public void ResetGame_DoesNotChangeScoreboard()
    {
        var scoreboardService = new ScoreboardService();
        var gameService = new GameService(scoreboardService);
        var sessionId = Guid.NewGuid();
        var game = gameService.CreateGame(sessionId);

        gameService.MakeMove(game.Id, 0, 0);
        gameService.MakeMove(game.Id, 1, 0);
        gameService.MakeMove(game.Id, 0, 1);
        gameService.MakeMove(game.Id, 1, 1);
        gameService.MakeMove(game.Id, 0, 2); // X wins -> scoreboard: 1 X win

        gameService.ResetGame(game.Id); // should NOT touch the scoreboard

        var scoreboard = scoreboardService.GetScoreboard(sessionId);
        Assert.Equal(1, scoreboard.XWins);
    }

    [Fact]
    public void SecondRoundAfterReset_IsCountedIndependently()
    {
        var scoreboardService = new ScoreboardService();
        var gameService = new GameService(scoreboardService);
        var sessionId = Guid.NewGuid();
        var game = gameService.CreateGame(sessionId);

        // Round 1: X wins.
        gameService.MakeMove(game.Id, 0, 0);
        gameService.MakeMove(game.Id, 1, 0);
        gameService.MakeMove(game.Id, 0, 1);
        gameService.MakeMove(game.Id, 1, 1);
        gameService.MakeMove(game.Id, 0, 2);

        gameService.ResetGame(game.Id);

        // Round 2: draw (X O X / X O O / O X X).
        gameService.MakeMove(game.Id, 0, 0); // X:0
        gameService.MakeMove(game.Id, 0, 1); // O:1
        gameService.MakeMove(game.Id, 0, 2); // X:2
        gameService.MakeMove(game.Id, 1, 1); // O:4
        gameService.MakeMove(game.Id, 1, 0); // X:3
        gameService.MakeMove(game.Id, 2, 0); // O:6
        gameService.MakeMove(game.Id, 2, 1); // X:7
        gameService.MakeMove(game.Id, 1, 2); // O:5
        gameService.MakeMove(game.Id, 2, 2); // X:8 -> draw

        var scoreboard = scoreboardService.GetScoreboard(sessionId);
        Assert.Equal(1, scoreboard.XWins);
        Assert.Equal(1, scoreboard.Draws);
    }
}

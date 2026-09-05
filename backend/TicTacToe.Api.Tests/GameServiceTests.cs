using TicTacToe.Api.Models;
using TicTacToe.Api.Services;
using Xunit;

namespace TicTacToe.Api.Tests;

public class GameServiceTests
{
    private static GameService NewService() => new(new ScoreboardService());

    [Fact]
    public void CreateGame_ReturnsEmptyBoardWithXToMove()
    {
        var service = NewService();

        var game = service.CreateGame(Guid.NewGuid());

        Assert.All(game.Board, cell => Assert.Equal(CellValue.Empty, cell));
        Assert.Equal(CellValue.X, game.CurrentPlayer);
        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.False(game.CanUndo);
    }

    [Fact]
    public void MakeMove_PlacesSymbolAndSwitchesTurn()
    {
        var service = NewService();
        var game = service.CreateGame(Guid.NewGuid());

        var updated = service.MakeMove(game.Id, row: 0, col: 0);

        Assert.Equal(CellValue.X, updated.Board[0]);
        Assert.Equal(CellValue.O, updated.CurrentPlayer);
        Assert.Single(updated.History);
    }

    [Fact]
    public void MakeMove_OnOccupiedCell_Throws()
    {
        var service = NewService();
        var game = service.CreateGame(Guid.NewGuid());
        service.MakeMove(game.Id, 0, 0);

        Assert.Throws<InvalidMoveException>(() => service.MakeMove(game.Id, 0, 0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 3)]
    [InlineData(3, 3)]
    public void MakeMove_OutOfRange_Throws(int row, int col)
    {
        var service = NewService();
        var game = service.CreateGame(Guid.NewGuid());

        Assert.Throws<InvalidMoveException>(() => service.MakeMove(game.Id, row, col));
    }

    [Fact]
    public void MakeMove_ThreeInARow_DeclaresWinnerAndLine()
    {
        var service = NewService();
        var game = service.CreateGame(Guid.NewGuid());

        // X: (0,0) (0,1) (0,2) ; O: (1,0) (1,1)
        service.MakeMove(game.Id, 0, 0); // X
        service.MakeMove(game.Id, 1, 0); // O
        service.MakeMove(game.Id, 0, 1); // X
        service.MakeMove(game.Id, 1, 1); // O
        var result = service.MakeMove(game.Id, 0, 2); // X completes top row

        Assert.Equal(GameStatus.Won, result.Status);
        Assert.Equal(CellValue.X, result.Winner);
        Assert.Equal(new[] { 0, 1, 2 }, result.WinningLine);
        Assert.True(result.IsGameOver);
    }

    [Fact]
    public void MakeMove_FullBoardNoWinner_IsDraw()
    {
        var service = NewService();
        var game = service.CreateGame(Guid.NewGuid());

        // Known draw sequence: X O X / X O O / O X X
        int[] xMoves = { 0, 2, 3, 7, 8 };
        int[] oMoves = { 1, 4, 5, 6 };
        service.MakeMove(game.Id, 0, 0); // X: 0
        service.MakeMove(game.Id, 0, 1); // O: 1
        service.MakeMove(game.Id, 0, 2); // X: 2
        service.MakeMove(game.Id, 1, 1); // O: 4
        service.MakeMove(game.Id, 1, 0); // X: 3
        service.MakeMove(game.Id, 2, 0); // O: 6
        service.MakeMove(game.Id, 2, 1); // X: 7
        service.MakeMove(game.Id, 1, 2); // O: 5
        var result = service.MakeMove(game.Id, 2, 2); // X: 8

        Assert.Equal(GameStatus.Draw, result.Status);
        Assert.Null(result.Winner);
        Assert.True(result.IsDraw);
    }

    [Fact]
    public void MakeMove_AfterGameOver_Throws()
    {
        var service = NewService();
        var game = service.CreateGame(Guid.NewGuid());
        service.MakeMove(game.Id, 0, 0);
        service.MakeMove(game.Id, 1, 0);
        service.MakeMove(game.Id, 0, 1);
        service.MakeMove(game.Id, 1, 1);
        service.MakeMove(game.Id, 0, 2); // X wins

        Assert.Throws<InvalidMoveException>(() => service.MakeMove(game.Id, 2, 2));
    }

    [Fact]
    public void MakeMove_VsComputer_AutoRepliesInSameCall()
    {
        var service = NewService();
        var game = service.CreateGame(Guid.NewGuid(), GameMode.VsComputer);

        var result = service.MakeMove(game.Id, 1, 1); // human plays center

        var filledCells = result.Board.Count(c => c != CellValue.Empty);
        Assert.Equal(2, filledCells); // human's move + computer's automatic reply
        Assert.Equal(CellValue.X, result.CurrentPlayer); // turn is back on the human
        Assert.Equal(2, result.History.Count);
    }

    [Fact]
    public void ComputerOpponent_NeverLoses_AcrossManyRandomGames()
    {
        var random = new Random(42);
        var losses = 0;

        for (var i = 0; i < 200; i++)
        {
            var service = NewService();
            var game = service.CreateGame(Guid.NewGuid(), GameMode.VsComputer);

            while (!game.IsGameOver)
            {
                var emptyCells = Enumerable.Range(0, 9)
                    .Where(idx => game.Board[idx] == CellValue.Empty)
                    .ToList();

                if (emptyCells.Count == 0) break;

                var chosen = emptyCells[random.Next(emptyCells.Count)];
                game = service.MakeMove(game.Id, chosen / 3, chosen % 3);
            }

            if (game.Winner == CellValue.X) losses++; // human (X) beating the computer (O)
        }

        Assert.Equal(0, losses);
    }

    [Fact]
    public void UndoMove_TwoPlayer_RemovesOnlyLastMove()
    {
        var service = NewService();
        var game = service.CreateGame(Guid.NewGuid(), GameMode.TwoPlayer);
        service.MakeMove(game.Id, 0, 0); // X
        service.MakeMove(game.Id, 1, 1); // O

        var undone = service.UndoMove(game.Id);

        Assert.Equal(CellValue.Empty, undone.Board[4]); // O's move at (1,1) undone
        Assert.Equal(CellValue.X, undone.Board[0]);      // X's move untouched
        Assert.Equal(CellValue.O, undone.CurrentPlayer); // O's turn again
        Assert.Single(undone.History);
    }

    [Fact]
    public void UndoMove_VsComputer_RemovesHumanAndComputerMoveTogether()
    {
        var service = NewService();
        var game = service.CreateGame(Guid.NewGuid(), GameMode.VsComputer);
        service.MakeMove(game.Id, 0, 0); // human + auto computer reply

        var undone = service.UndoMove(game.Id);

        Assert.All(undone.Board, cell => Assert.Equal(CellValue.Empty, cell));
        Assert.Equal(CellValue.X, undone.CurrentPlayer); // back to human's turn
        Assert.Empty(undone.History);
    }

    [Fact]
    public void UndoMove_WithNoMoves_Throws()
    {
        var service = NewService();
        var game = service.CreateGame(Guid.NewGuid());

        Assert.Throws<InvalidMoveException>(() => service.UndoMove(game.Id));
    }

    [Fact]
    public void UndoMove_AfterGameOver_Throws()
    {
        var service = NewService();
        var game = service.CreateGame(Guid.NewGuid());
        service.MakeMove(game.Id, 0, 0);
        service.MakeMove(game.Id, 1, 0);
        service.MakeMove(game.Id, 0, 1);
        service.MakeMove(game.Id, 1, 1);
        service.MakeMove(game.Id, 0, 2); // X wins

        Assert.Throws<InvalidMoveException>(() => service.UndoMove(game.Id));
    }

    [Fact]
    public void ResetGame_ClearsBoardButKeepsIdAndMode()
    {
        var service = NewService();
        var game = service.CreateGame(Guid.NewGuid(), GameMode.VsComputer);
        service.MakeMove(game.Id, 0, 0);

        var reset = service.ResetGame(game.Id);

        Assert.Equal(game.Id, reset.Id);
        Assert.Equal(GameMode.VsComputer, reset.Mode);
        Assert.All(reset.Board, cell => Assert.Equal(CellValue.Empty, cell));
        Assert.Empty(reset.History);
        Assert.False(reset.CanUndo);
    }

    [Fact]
    public void MakeMove_NonexistentGame_ThrowsGameNotFound()
    {
        var service = NewService();

        Assert.Throws<GameNotFoundException>(() => service.MakeMove(Guid.NewGuid(), 0, 0));
    }
}

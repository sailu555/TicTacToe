using Microsoft.AspNetCore.Mvc;
using TicTacToe.Api.Models;
using TicTacToe.Api.Services;

namespace TicTacToe.Api.Controllers;

[ApiController]
[Route("api/games")]
public class GameController : SessionAwareControllerBase
{
    private readonly IGameService _gameService;
    private readonly ILogger<GameController> _logger;

    public GameController(IGameService gameService, ILogger<GameController> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new game. Requires an "X-Session-Id" header (GUID) so the
    /// game's eventual result can be attributed to the right scoreboard.
    /// Body: { "mode": 0 | 1 } — 0 = TwoPlayer, 1 = VsComputer. Defaults to TwoPlayer.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(GameState), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<GameState> CreateGame([FromBody] CreateGameRequest? request)
    {
        if (!TryGetSessionId(out var sessionId, out var error))
        {
            return error!;
        }

        var mode = request?.Mode ?? GameMode.TwoPlayer;
        var game = _gameService.CreateGame(sessionId, mode);
        _logger.LogInformation("Created game {GameId} in {Mode} mode for session {SessionId}", game.Id, mode, sessionId);
        return CreatedAtAction(nameof(GetGame), new { id = game.Id }, game);
    }

    /// <summary>Get the current state of a game by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GameState), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<GameState> GetGame(Guid id)
    {
        var game = _gameService.GetGame(id);
        if (game is null)
        {
            return NotFound(new ApiError { Message = $"Game '{id}' was not found." });
        }

        return Ok(game);
    }

    /// <summary>Make a move at (row, col) for the current player.</summary>
    [HttpPost("{id:guid}/move")]
    [ProducesResponseType(typeof(GameState), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<GameState> MakeMove(Guid id, [FromBody] MoveRequest request)
    {
        try
        {
            var game = _gameService.MakeMove(id, request.Row, request.Col);
            return Ok(game);
        }
        catch (GameNotFoundException ex)
        {
            return NotFound(new ApiError { Message = ex.Message });
        }
        catch (InvalidMoveException ex)
        {
            return BadRequest(new ApiError { Message = ex.Message });
        }
    }

    /// <summary>Reset an existing game back to an empty board, keeping the same id.</summary>
    [HttpPost("{id:guid}/reset")]
    [ProducesResponseType(typeof(GameState), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<GameState> ResetGame(Guid id)
    {
        try
        {
            var game = _gameService.ResetGame(id);
            return Ok(game);
        }
        catch (GameNotFoundException ex)
        {
            return NotFound(new ApiError { Message = ex.Message });
        }
    }

    /// <summary>
    /// Undo the last move. In TwoPlayer mode, undoes just the last player's
    /// move. In VsComputer mode, undoes the computer's move and the human
    /// move that triggered it together, returning the turn to the human.
    /// Not allowed once the game has ended.
    /// </summary>
    [HttpPost("{id:guid}/undo")]
    [ProducesResponseType(typeof(GameState), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<GameState> UndoMove(Guid id)
    {
        try
        {
            var game = _gameService.UndoMove(id);
            return Ok(game);
        }
        catch (GameNotFoundException ex)
        {
            return NotFound(new ApiError { Message = ex.Message });
        }
        catch (InvalidMoveException ex)
        {
            return BadRequest(new ApiError { Message = ex.Message });
        }
    }
}

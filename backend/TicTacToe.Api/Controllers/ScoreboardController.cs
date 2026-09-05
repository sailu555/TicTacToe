using Microsoft.AspNetCore.Mvc;
using TicTacToe.Api.Models;
using TicTacToe.Api.Services;

namespace TicTacToe.Api.Controllers;

[ApiController]
[Route("api/scoreboard")]
public class ScoreboardController : SessionAwareControllerBase
{
    private readonly IScoreboardService _scoreboardService;

    public ScoreboardController(IScoreboardService scoreboardService)
    {
        _scoreboardService = scoreboardService;
    }

    /// <summary>Get the current session's scoreboard (X wins / O wins / Draws).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ScoreboardState), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<ScoreboardState> GetScoreboard()
    {
        if (!TryGetSessionId(out var sessionId, out var error))
        {
            return error!;
        }

        return Ok(_scoreboardService.GetScoreboard(sessionId));
    }

    /// <summary>Reset the current session's scoreboard back to zero. Does not affect any in-progress game.</summary>
    [HttpPost("reset")]
    [ProducesResponseType(typeof(ScoreboardState), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<ScoreboardState> ResetScoreboard()
    {
        if (!TryGetSessionId(out var sessionId, out var error))
        {
            return error!;
        }

        return Ok(_scoreboardService.ResetScoreboard(sessionId));
    }
}

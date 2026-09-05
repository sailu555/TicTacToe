using Microsoft.AspNetCore.Mvc;
using TicTacToe.Api.Models;

namespace TicTacToe.Api.Controllers;

/// <summary>
/// Shared helper for controllers that need to identify the caller's
/// session (used to scope the scoreboard). The frontend generates one
/// random id per page load and sends it as the "X-Session-Id" header on
/// every request.
/// </summary>
public abstract class SessionAwareControllerBase : ControllerBase
{
    private const string SessionHeaderName = "X-Session-Id";

    protected bool TryGetSessionId(out Guid sessionId, out ActionResult? errorResult)
    {
        var headerValue = Request.Headers[SessionHeaderName].ToString();

        if (Guid.TryParse(headerValue, out sessionId))
        {
            errorResult = null;
            return true;
        }

        sessionId = Guid.Empty;
        errorResult = BadRequest(new ApiError
        {
            Message = $"A valid '{SessionHeaderName}' header (GUID) is required."
        });
        return false;
    }
}

# Tic Tac Toe — Running the Unit Tests

Covers the `TicTacToe.Api.Tests` project: what it tests, how to set it up,
how to run it, and fixes for issues you may hit along the way.

## What's covered

| Test file | What it verifies |
|---|---|
| `GameServiceTests.cs` | Game creation defaults, move placement and turn-switching, invalid-move rejection (occupied cell, out-of-range row/col), win detection and winning-line correctness, draw detection, rejecting moves after game-over, the computer's automatic reply in VsComputer mode, the computer opponent never losing across 200 simulated random games, Undo in both modes, Undo edge cases (no moves yet / game already over), Reset behavior, and the not-found error for an unknown game id. |
| `ScoreboardServiceTests.cs` | Fresh sessions default to zero, win/draw counts increment correctly, Reset Scoreboard zeroes counts, sessions are isolated from each other, and three integration checks run directly against `GameService`: a completed game is counted **exactly once** even after repeated fetches, **Reset Board never changes the scoreboard**, and sequential rounds after a reset are counted independently. |

## One-time setup

If you haven't created the test project yet:

```bash
cd backend
dotnet new xunit -n TicTacToe.Api.Tests
cd TicTacToe.Api.Tests
dotnet add reference ../TicTacToe.Api/TicTacToe.Api.csproj
```

Delete the auto-generated `UnitTest1.cs` and place `GameServiceTests.cs` and
`ScoreboardServiceTests.cs` in that folder instead.

## Running the tests

**Important — stop the backend first.** The test project references
`TicTacToe.Api.csproj` directly, so running the tests rebuilds the main
API project too. If `dotnet run` is still active in another terminal for
the backend, the build will fail because the running `.exe` is locked by
Windows (see [Troubleshooting](#troubleshooting) below).

```bash
cd backend/TicTacToe.Api.Tests
dotnet test
```

This builds both projects and runs every test, printing a pass/fail
summary. A clean run looks like:

```
Passed!  - Failed:     0, Passed:    22, Skipped:     0, Total:    22
```

### Running a single test or file

```bash
# Run only tests in one class:
dotnet test --filter "FullyQualifiedName~GameServiceTests"

# Run one specific test:
dotnet test --filter "FullyQualifiedName~ComputerOpponent_NeverLoses_AcrossManyRandomGames"
```

### Verbose output

```bash
dotnet test --logger "console;verbosity=detailed"
```
Useful when a test fails and you want to see the actual vs. expected
values `Assert` reported, rather than just pass/fail counts.

## Troubleshooting

### `MSB3027`/`MSB3021` — file locked by another process

```
error MSB3027: Could not copy "...\apphost.exe" to "bin\Debug\net10.0\TicTacToe.Api.exe".
Exceeded retry count of 10. Failed. The file is locked by: "TicTacToe.Api (36272)"
```

**Cause:** the backend is still running (`dotnet run` in another terminal).
Building the test project rebuilds `TicTacToe.Api.exe`, and Windows won't
let the build overwrite a `.exe` that's currently executing.

**Fix:** go to the terminal running the backend, press **Ctrl+C** to stop
it, then re-run `dotnet test`. You can restart the backend afterward — the
frontend (`npm start`) is a separate process and never needs to stop for
this.

### `CS1739` — named argument doesn't match a parameter

```
error CS1739: The best overload for 'Random' does not have a parameter named 'seed'
```

**Cause:** a genuine typo in the test code — `Random`'s constructor
parameter isn't named `seed`.

**Fix:** in `GameServiceTests.cs`, change:
```csharp
var random = new Random(seed: 42);
```
to:
```csharp
var random = new Random(42);
```

### Framework version mismatch

If `dotnet test` (or `dotnet run`) complains about a missing or
incompatible `Microsoft.NETCore.App` version, check what's actually
installed:
```bash
dotnet --list-sdks
dotnet --list-runtimes
```
Then make sure `<TargetFramework>` in **both** `TicTacToe.Api.csproj` and
`TicTacToe.Api.Tests.csproj` match a version you actually have installed
(e.g. `net10.0`). Both projects must target compatible frameworks for the
test project's reference to build correctly.

# Tic Tac Toe — Setup Guide

This document covers everything needed to get the application running
locally: prerequisites, project layout, and step-by-step installation for
both the backend and frontend.

## Architecture at a glance

```
Browser (Node/TypeScript frontend, http://localhost:3000)
        │  fetch() calls, JSON over HTTP
        ▼
.NET Web API backend (http://localhost:5000)
        │  in-memory state
        ▼
  GameService  +  ScoreboardService
```

- The **frontend** is a thin client: it renders the board and calls the
  backend for every game action. No game rules live in the browser.
- The **backend** owns all game logic, state, scoring, and move history.
  State is held in memory only (see [Configuration notes](#configuration-notes)).

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0.x | Must match (or be compatible with) the runtime installed on your machine. The project targets `net10.0`. |

- **.NET 10 SDK** — https://dotnet.microsoft.com/download/dotnet/10.0
  (the project targets `net10.0`; if you have a different major version
  installed, either install .NET 10 alongside it, or edit
  `<TargetFramework>` in `TicTacToe.Api.csproj` to match what you have

| Node.js | 18+ | 22.x also verified working |
| npm | Bundled with Node.js | Used for TypeScript compilation only — no runtime npm packages are required to run the app |
Install Node.js if not already installed. https://nodejs.org/en/download/

> **Framework version mismatch:** if `dotnet run` complains that the
> required framework version isn't installed, check your installed
> runtimes with `dotnet --list-runtimes` and either install a matching
> SDK, or edit `TargetFramework` in `TicTacToe.Api.csproj` to match what
> you have (e.g. `net8.0` if that's what's installed).

## Project structure

```
tictactoe-app/
├── backend/
│   └── TicTacToe.Api/
│       ├── Controllers/
│       │   ├── GameController.cs
│       │   ├── ScoreboardController.cs
│       │   └── SessionAwareControllerBase.cs
│       ├── Models/
│       │   └── GameModels.cs
│       ├── Services/
│       │   ├── GameService.cs
│       │   └── ScoreboardService.cs
│       ├── Program.cs
│       ├── appsettings.json
│       └── TicTacToe.Api.csproj
├── frontend/
│   ├── src/
│   │   ├── server.ts       (static file server)
│   │   └── client.ts       (browser game client)
│   ├── public/
│   │   ├── index.html
│   │   └── styles.css
│   ├── package.json
│   ├── tsconfig.server.json
│   └── tsconfig.client.json
├── docs/
│   ├── SETUP.md             (this file)
│   ├── RUNNING_AND_TESTING.md
│   └── API_CONTRACT.md
└── README.md
```

## Installation steps

### 1. Backend

```bash
cd backend/TicTacToe.Api
dotnet restore
```

This pulls the one NuGet dependency (`Swashbuckle.AspNetCore`, used for
Swagger). No further configuration is required — `appsettings.json` and
`Properties/launchSettings.json` are already set up to run on port 5000.

### 2. Frontend

```bash
cd frontend
npm install
```

This installs only **dev-time** dependencies (`typescript`, `@types/node`)
— the compiled server itself has no runtime npm dependencies (it uses
Node's built-in `http`/`fs`/`path` modules only).

## Configuration notes

- **Ports**: backend defaults to `http://localhost:5000` (set in
  `Properties/launchSettings.json`); frontend defaults to
  `http://localhost:3000` (set in `src/server.ts`, overridable via the
  `PORT` environment variable).
- **CORS**: the backend's `Program.cs` explicitly allows
  `http://localhost:3000` and `http://127.0.0.1:3000` as origins. If you
  host the frontend elsewhere, update the `WithOrigins(...)` call there.
- **API base URL**: the frontend points at the backend via two constants
  near the top of `src/client.ts` — `API_BASE_URL` and `SCOREBOARD_URL`.
  Update both (and re-run `npm run build:client`) if the backend is hosted
  somewhere other than `localhost:5000`.
- **State persistence**: both `GameService` and `ScoreboardService` store
  state in in-memory `ConcurrentDictionary` instances. Restarting the
  backend process clears all games and scoreboards — there is no database
  or file-based persistence in this setup.
- **Sessions**: the frontend generates one random session id
  (`crypto.randomUUID()`) per page load and sends it as an `X-Session-Id`
  header on requests that need it (game creation, scoreboard). This is
  what scopes each browser tab's scoreboard independently.

Once both installs succeed, proceed to
[RUNNING_AND_TESTING.md](./RUNNING_AND_TESTING.md) to start the app.

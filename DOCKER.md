# Running with Docker

An alternative to the local .NET SDK / Node.js setup in
[SETUP.md](./SETUP.md) — this runs both services in containers, so you
don't need matching SDK/runtime versions installed on your host machine.

## Prerequisite

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (includes
  Docker Compose) installed and running.

## Running

From the project root (the folder containing `docker-compose.yml`):

```bash
docker compose up --build
```

This builds both images (first run only takes longer; subsequent runs
reuse cached layers) and starts both containers. Once it settles, you'll
see log output from both `tictactoe-backend` and `tictactoe-frontend`.

Open **http://localhost:3000** — same as running locally.

Swagger is available at **http://localhost:5000/swagger** as before.

To stop everything:

```bash
docker compose down
```

## How it's wired together

- **Backend** (`backend/TicTacToe.Api/Dockerfile`): a multi-stage build —
  the .NET 10 SDK image restores and publishes the app; the much smaller
  ASP.NET runtime image actually runs it. Listens on port 5000 inside the
  container, mapped to port 5000 on your machine.
- **Frontend** (`frontend/Dockerfile`): a multi-stage build — a Node image
  installs the dev-only TypeScript tooling and compiles both `server.ts`
  and `client.ts`; the runtime image only copies the compiled output
  (`dist/`, `public/`) and runs `node dist/server.js` directly, since the
  compiled server has no npm runtime dependencies. Listens on port 3000,
  mapped to port 3000 on your machine.
- **No application code changes were needed.** The browser's `fetch()`
  calls in `client.ts` already point at `http://localhost:5000` directly
  — since both containers publish their ports to `localhost` on your
  machine (via the `ports:` mappings in `docker-compose.yml`), the browser
  reaches the backend exactly the same way it does when running both
  processes locally without Docker.

## Notes

- **State still doesn't persist.** Stopping and restarting the containers
  (or running `docker compose down` then `up` again) clears all games and
  scoreboards, same as restarting `dotnet run` locally — the in-memory
  storage design (see [SETUP.md](./SETUP.md#configuration-notes)) doesn't
  change just because it's containerized.
- **`ASPNETCORE_ENVIRONMENT` is set to `Development`** in the backend
  Dockerfile so Swagger stays available, matching local behavior. Change
  this to `Production` in `backend/TicTacToe.Api/Dockerfile` if you want
  to disable Swagger for a more production-like run.
- **Rebuilding after code changes**: `docker compose up --build` re-runs
  the build stages, so it picks up any source changes. If you only changed
  one side (e.g. just the frontend), you can rebuild just that service:
  ```bash
  docker compose up --build frontend
  ```


import * as http from "http";
import * as fs from "fs";
import * as path from "path";

/**
 * Minimal static file server for the compiled frontend (public/).
 * Deliberately dependency-free (built-in http/fs/path only) so the
 * project runs with just `npm install` (dev-only deps) + `npm run build`.
 *
 * The actual game logic lives in the .NET API (see ../backend); this
 * server just serves index.html / client.js / styles.css to the browser.
 */

const PORT = Number(process.env.PORT ?? 3000);
const PUBLIC_DIR = path.join(__dirname, "..", "public");

const MIME_TYPES: Record<string, string> = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".ico": "image/x-icon",
};

function resolveRequestedPath(urlPath: string): string {
  // Default to index.html for the root path.
  const safePath = urlPath === "/" ? "/index.html" : urlPath;

  // Prevent path traversal outside of the public directory.
  const resolved = path.normalize(path.join(PUBLIC_DIR, safePath));
  if (!resolved.startsWith(PUBLIC_DIR)) {
    return path.join(PUBLIC_DIR, "index.html");
  }
  return resolved;
}

const server = http.createServer((req, res) => {
  const urlPath = (req.url ?? "/").split("?")[0];
  const filePath = resolveRequestedPath(urlPath);

  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404, { "Content-Type": "text/plain" });
      res.end("404 Not Found");
      return;
    }

    const ext = path.extname(filePath);
    const contentType = MIME_TYPES[ext] ?? "application/octet-stream";
    res.writeHead(200, { "Content-Type": contentType });
    res.end(data);
  });
});

server.listen(PORT, () => {
  console.log(`Tic Tac Toe frontend running at http://localhost:${PORT}`);
  console.log(`Serving static files from: ${PUBLIC_DIR}`);
});

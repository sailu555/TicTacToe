# --- Build stage ---
FROM node:20-alpine AS build
WORKDIR /src

COPY package.json ./
RUN npm install

COPY tsconfig.server.json tsconfig.client.json ./
COPY src ./src
COPY public ./public
RUN npm run build

# --- Runtime stage ---
# The compiled server (dist/server.js) only uses Node's built-in http/fs/path
# modules — no npm packages are needed to actually run it, so the runtime
# image only needs Node itself plus the two build outputs.
FROM node:20-alpine AS runtime
WORKDIR /app

COPY --from=build /src/dist ./dist
COPY --from=build /src/public ./public

ENV PORT=3000
EXPOSE 3000

CMD ["node", "dist/server.js"]

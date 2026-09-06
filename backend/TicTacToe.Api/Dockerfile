# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first and restore separately, so Docker can cache the
# restore layer and skip it on rebuilds where only source files changed.
COPY TicTacToe.Api.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# The project's launchSettings.json targets port 5000 locally; inside the
# container we set it explicitly via ASPNETCORE_URLS instead, since
# launchSettings.json is a local-development-only file and isn't used
# when running the published app directly.
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Development
EXPOSE 5000

ENTRYPOINT ["dotnet", "TicTacToe.Api.dll"]

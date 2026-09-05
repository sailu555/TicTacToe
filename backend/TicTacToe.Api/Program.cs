using TicTacToe.Api.Services;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "FrontendCorsPolicy";

// The Node/TypeScript frontend runs on a different origin/port during local
// development, so the API needs an explicit CORS policy allowing it through.
// Adjust the allowed origin(s) here if you host the frontend elsewhere.
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://127.0.0.1:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Game state is kept in memory for the lifetime of the process. Registered
// as a singleton so all requests share the same in-memory game store.
builder.Services.AddSingleton<IGameService, GameService>();

// Session-scoped scoreboard (X wins / O wins / Draws), also in-memory and
// shared across requests for the lifetime of the process.
builder.Services.AddSingleton<IScoreboardService, ScoreboardService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(FrontendCorsPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();

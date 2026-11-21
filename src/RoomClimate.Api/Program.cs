using System.Text.Json.Serialization;
using RoomClimate.Api;
using RoomClimate.Core;

var builder = WebApplication.CreateBuilder(args);

var dbPath = builder.Configuration["Database:Path"] ?? Path.Combine(AppContext.BaseDirectory, "roomclimate.db");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);

builder.Services.AddSingleton(new ClimateRepository($"Data Source={dbPath}"));
builder.Services.AddSingleton(new ClimateAnalyzer());
builder.Services.AddSingleton<IngestQueue>();
builder.Services.AddHostedService<IngestService>();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
}));

app.MapPost("/api/readings", async (Reading reading, IngestQueue queue, CancellationToken ct) =>
{
    if (!ClimateAnalyzer.IsValid(reading, out var error))
        return Results.BadRequest(new { error });
    await queue.EnqueueAsync(reading, ct);
    return Results.Accepted();
});

app.MapGet("/api/rooms", (ClimateRepository repo) => repo.Rooms());

app.MapGet("/api/readings/latest", (ClimateRepository repo, ClimateAnalyzer analyzer) =>
    repo.Latest().Select(r => new
    {
        r.Room, r.Timestamp, r.TemperatureC, r.HumidityPercent, r.Co2Ppm,
        comfortIndex = analyzer.ComfortIndex(r.TemperatureC, r.HumidityPercent, r.Co2Ppm)
    }));

app.MapGet("/api/rooms/{room}/readings", (string room, int? minutes, ClimateRepository repo) =>
    repo.Readings(room, DateTimeOffset.UtcNow.AddMinutes(-(minutes ?? 30))));

app.MapGet("/api/rooms/{room}/summary", (string room, int? minutes, ClimateRepository repo, ClimateAnalyzer analyzer) =>
    analyzer.Summarize(room, repo.Readings(room, DateTimeOffset.UtcNow.AddMinutes(-(minutes ?? 30)))));

app.MapGet("/api/alerts", (int? limit, ClimateRepository repo) => repo.Alerts(limit ?? 50));

app.Run();

public partial class Program;

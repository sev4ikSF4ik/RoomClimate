using System.Net.Http.Json;
using RoomClimate.Simulator;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Використання: dotnet run --project src/RoomClimate.Simulator -- [apiUrl] [кімнати через кому] [інтервал, с]
var apiUrl = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5080";
var rooms = (args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("ROOMS") ?? "Аудиторія 101,Аудиторія 202,Лабораторія 305")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var interval = TimeSpan.FromSeconds(args.Length > 2 ? double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 2);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

using var http = new HttpClient { BaseAddress = new Uri(apiUrl), Timeout = TimeSpan.FromSeconds(5) };
Console.WriteLine($"Simulator -> {apiUrl}, rooms: {string.Join(", ", rooms)}, interval: {interval.TotalSeconds}s. Ctrl+C to stop.");

// Кожен "сенсор" працює у власній задачі паралельно з іншими
var tasks = rooms.Select((room, i) => RunSensorAsync(new RoomModel(room, 1000 + i), cts.Token)).ToArray();
await Task.WhenAll(tasks);

async Task RunSensorAsync(RoomModel model, CancellationToken ct)
{
    using var timer = new PeriodicTimer(interval);
    try
    {
        do
        {
            var reading = model.Next(DateTimeOffset.UtcNow);
            try
            {
                var resp = await http.PostAsJsonAsync("/api/readings", reading, ct);
                Console.WriteLine($"[{reading.Timestamp:HH:mm:ss}] {reading.Room,-16} T={reading.TemperatureC,5:F1}°C " +
                                  $"H={reading.HumidityPercent,4:F0}% CO2={reading.Co2Ppm,5:F0}ppm -> {(int)resp.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[{model.Name}] API unavailable: {ex.Message}");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
    catch (OperationCanceledException) { }
}

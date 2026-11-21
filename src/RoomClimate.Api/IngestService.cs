using System.Threading.Channels;
using RoomClimate.Core;

namespace RoomClimate.Api;

/// <summary>
/// Черга вхідних вимірювань. HTTP-запит лише кладе дані в канал,
/// а окремий фоновий потік пакетно аналізує й записує їх у БД
/// (патерн producer/consumer).
/// </summary>
public sealed class IngestQueue
{
    private readonly Channel<Reading> _channel =
        Channel.CreateBounded<Reading>(new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.Wait });

    public ValueTask EnqueueAsync(Reading r, CancellationToken ct) => _channel.Writer.WriteAsync(r, ct);
    public ChannelReader<Reading> Reader => _channel.Reader;
}

public sealed class IngestService(IngestQueue queue, ClimateRepository repo, ClimateAnalyzer analyzer,
    ILogger<IngestService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var batch = new List<Reading>();
        var alerts = new List<Alert>();
        while (await queue.Reader.WaitToReadAsync(ct))
        {
            while (batch.Count < 500 && queue.Reader.TryRead(out var r))
            {
                batch.Add(r);
                alerts.AddRange(analyzer.Evaluate(r));
            }
            try
            {
                repo.Save(batch, alerts);
                if (alerts.Count > 0)
                    log.LogInformation("Saved {Count} readings, {Alerts} alerts", batch.Count, alerts.Count);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to save batch of {Count} readings", batch.Count);
            }
            batch.Clear();
            alerts.Clear();
        }
    }
}

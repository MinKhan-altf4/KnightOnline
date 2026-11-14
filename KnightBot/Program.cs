using System.Diagnostics;
using System.Net;
using KnightOnline.Bot;

// KnightBot — headless load test for the KnightOnline server.
// See KnightBot/README.md. Every bot creates a real guest account and a
// character, so it must only target a Development server with its own
// throwaway database.

BotOptions options;
try
{
    options = BotOptions.Parse(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine(BotOptions.Usage);
    return 2;
}

if (!options.AllowDatabaseWrites)
{
    Console.Error.WriteLine(
        "Refusing to run: each bot creates a guest account and a character in the " +
        "server database. Point the server at a throwaway database and pass " +
        "--allow-db-writes.");
    return 2;
}

if (!IPAddress.TryParse(options.Host, out IPAddress? address) || !IPAddress.IsLoopback(address))
{
    Console.Error.WriteLine("Refusing to run: KnightBot only targets loopback servers.");
    return 2;
}

var metrics = new BotMetrics();
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    stop.Cancel();
};

Console.WriteLine(
    $"KnightBot run {options.RunId}: {options.Bots} bots -> {options.Host}:{options.Port}, " +
    $"ramp {options.RampPerSecond}/s, duration {options.DurationSeconds}s");

var clock = Stopwatch.StartNew();
var running = new List<Task>();
var delayBetweenBots = TimeSpan.FromSeconds(1.0 / options.RampPerSecond);
for (int index = 0; index < options.Bots && !stop.IsCancellationRequested; index++)
{
    running.Add(RunBotAsync(index));
    try
    {
        await Task.Delay(delayBetweenBots, stop.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

Task progress = ReportProgressAsync();
try
{
    await Task.Delay(TimeSpan.FromSeconds(options.DurationSeconds), stop.Token);
}
catch (OperationCanceledException)
{
}

stop.Cancel();
await Task.WhenAll(running).WaitAsync(TimeSpan.FromSeconds(10))
    .ContinueWith(_ => { }, TaskScheduler.Default);

Console.WriteLine();
Console.WriteLine(metrics.Report(options.Bots, clock.Elapsed));
return metrics.InWorld == options.Bots ? 0 : 1;

async Task RunBotAsync(int index)
{
    await using var bot = new BotClient(index, options, metrics);
    try
    {
        await bot.RunAsync(stop.Token);
    }
    catch (BotFailedException exception)
    {
        metrics.RecordFailure(exception.Stage, exception.Message);
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested)
    {
    }
    catch (Exception exception)
    {
        metrics.RecordFailure(bot.Stage, $"{exception.GetType().Name}: {exception.Message}");
    }
}

async Task ReportProgressAsync()
{
    try
    {
        while (!stop.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stop.Token);
            Console.WriteLine($"[{clock.Elapsed:mm\\:ss}] in world: {metrics.InWorld}/{options.Bots}");
        }
    }
    catch (OperationCanceledException)
    {
    }
}

public sealed record BotOptions(
    string Host,
    int Port,
    int Bots,
    double RampPerSecond,
    int DurationSeconds,
    int MoveIntervalMilliseconds,
    string ServerId,
    string RunId,
    int Seed,
    bool AllowDatabaseWrites)
{
    public const string Usage =
        "usage: KnightBot [--bots 50] [--ramp 10] [--duration 60] [--host 127.0.0.1] " +
        "[--port 7777] [--move-ms 100] [--server-id server-1] [--seed 1] --allow-db-writes";

    public static BotOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool allowWrites = false;
        for (int index = 0; index < args.Length; index++)
        {
            string key = args[index];
            if (key == "--allow-db-writes")
            {
                allowWrites = true;
                continue;
            }
            if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
                throw new ArgumentException($"Unexpected argument '{key}'.");
            values[key[2..]] = args[++index];
        }

        int Int(string name, int fallback, int min, int max)
        {
            if (!values.TryGetValue(name, out string? raw))
                return fallback;
            if (!int.TryParse(raw, out int value) || value < min || value > max)
                throw new ArgumentException($"--{name} must be between {min} and {max}.");
            return value;
        }

        return new BotOptions(
            values.GetValueOrDefault("host", "127.0.0.1"),
            Int("port", 7777, 1, 65535),
            Int("bots", 50, 1, 2000),
            Int("ramp", 10, 1, 500),
            Int("duration", 60, 1, 3600),
            Int("move-ms", 100, 20, 5000),
            values.GetValueOrDefault("server-id", "server-1"),
            Guid.NewGuid().ToString("N")[..6],
            Int("seed", 1, 0, int.MaxValue),
            allowWrites);
    }
}

using KnightOnline.Server.Persistence;
using Npgsql;

namespace KnightServer.Tests.Persistence;

public sealed class SessionPersistenceGateTests
{
    private static readonly Func<TimeSpan, CancellationToken, Task> NoDelay =
        (_, _) => Task.CompletedTask;

    [Fact]
    public async Task LimitsConcurrentOperations()
    {
        var gate = new SessionPersistenceGate(3, delay: NoDelay);
        int running = 0, peak = 0;
        var release = new TaskCompletionSource();

        Task[] work = Enumerable.Range(0, 20).Select(_ => gate.RunAsync("test", async _ =>
        {
            int now = Interlocked.Increment(ref running);
            InterlockedMax(ref peak, now);
            await release.Task;
            Interlocked.Decrement(ref running);
        })).ToArray();

        await Task.Delay(100);
        Assert.Equal(3, Volatile.Read(ref peak));
        release.SetResult();
        await Task.WhenAll(work);
        Assert.Equal(3, peak);
    }

    [Fact]
    public async Task RetriesTooManyConnections_ThenSucceeds()
    {
        var gate = new SessionPersistenceGate(1, maximumAttempts: 4, delay: NoDelay);
        int attempts = 0;

        bool result = await gate.RunAsync("test", _ =>
        {
            attempts++;
            if (attempts < 3)
                throw Wrapped(PostgresErrorCodes.TooManyConnections);
            return Task.FromResult(true);
        });

        Assert.True(result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task GivesUp_AfterMaximumAttempts()
    {
        var gate = new SessionPersistenceGate(1, maximumAttempts: 2, delay: NoDelay);
        int attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.RunAsync("test", _ =>
        {
            attempts++;
            throw Wrapped(PostgresErrorCodes.TooManyConnections);
        }));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task NonTransientFailure_IsNotRetried()
    {
        var gate = new SessionPersistenceGate(1, delay: NoDelay);
        int attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.RunAsync("test", _ =>
        {
            attempts++;
            throw new InvalidOperationException("bug");
        }));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task SlotIsReleased_AfterFailure()
    {
        var gate = new SessionPersistenceGate(1, maximumAttempts: 1, delay: NoDelay);
        await Assert.ThrowsAnyAsync<Exception>(() =>
            gate.RunAsync("fail", _ => throw new InvalidOperationException()));

        Assert.True(await gate.RunAsync("next", _ => Task.FromResult(true))
            .WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(0, gate.InFlight);
    }

    [Theory]
    [InlineData(PostgresErrorCodes.TooManyConnections, true)]
    [InlineData(PostgresErrorCodes.CannotConnectNow, true)]
    [InlineData(PostgresErrorCodes.SerializationFailure, true)]
    [InlineData(PostgresErrorCodes.UniqueViolation, false)]
    public void IsTransient_ClassifiesSqlState(string sqlState, bool expected)
    {
        Assert.Equal(expected, SessionPersistenceGate.IsTransient(Wrapped(sqlState)));
    }

    [Fact]
    public void PoolLimit_IsAppliedUnlessExplicit()
    {
        string applied = DatabaseConfiguration.ApplyPoolLimit(
            "Host=localhost;Database=x;Username=u;Password=p", 40);
        string explicitLimit = DatabaseConfiguration.ApplyPoolLimit(
            "Host=localhost;Database=x;Username=u;Password=p;Maximum Pool Size=7", 40);

        Assert.Equal(40, new NpgsqlConnectionStringBuilder(applied).MaxPoolSize);
        Assert.Equal(7, new NpgsqlConnectionStringBuilder(explicitLimit).MaxPoolSize);
    }

    /// <summary>Same shape as EF Core's "likely due to a transient failure".</summary>
    private static InvalidOperationException Wrapped(string sqlState) =>
        new("An exception has been raised that is likely due to a transient failure.",
            new PostgresException("simulated", "FATAL", "FATAL", sqlState));

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)) &&
               Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}

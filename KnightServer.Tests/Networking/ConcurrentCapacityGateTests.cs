using KnightOnline.Server.Networking;

namespace KnightServer.Tests.Networking;

public sealed class ConcurrentCapacityGateTests
{
    [Fact]
    public async Task ConcurrentEntries_NeverExceedTransportLimit()
    {
        var gate = new ConcurrentCapacityGate(750);

        bool[] results = await Task.WhenAll(
            Enumerable.Range(0, 1000)
                .Select(_ => Task.Run(gate.TryEnter)));

        Assert.Equal(750, results.Count(result => result));
        Assert.Equal(250, results.Count(result => !result));
        Assert.Equal(750, gate.Count);
        Assert.Equal(750, gate.Maximum);
    }

    [Fact]
    public void Exit_ReturnsCapacityForNextConnection()
    {
        var gate = new ConcurrentCapacityGate(1);

        Assert.True(gate.TryEnter());
        Assert.False(gate.TryEnter());
        gate.Exit();
        Assert.True(gate.TryEnter());
    }
}

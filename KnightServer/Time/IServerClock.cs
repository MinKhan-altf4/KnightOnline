namespace KnightOnline.Server.Time;

public interface IServerClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemServerClock : IServerClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

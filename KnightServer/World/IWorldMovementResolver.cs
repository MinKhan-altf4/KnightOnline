using System.Numerics;

namespace KnightOnline.Server.World;

public interface IWorldMovementResolver
{
    Vector2 Resolve(Vector2 start, Vector2 desiredEnd);
}

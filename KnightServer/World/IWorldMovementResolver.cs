using System.Numerics;

namespace KnightOnline.Server.World;

public interface IWorldMovementResolver
{
    Vector2 Resolve(
        string mapDefinitionId,
        Vector2 start,
        Vector2 desiredEnd);
}

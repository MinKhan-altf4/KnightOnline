using KnightOnline.Server.Networking;

namespace KnightOnline.Server.Combat;

public interface ICombatStatsProvider
{
    CombatStats GetFor(ClientConnection attacker);
}

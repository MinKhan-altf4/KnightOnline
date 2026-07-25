namespace KnightOnline.Server.Combat;

public sealed record CombatStats(
    int BaseAttack,
    int EquipmentAttack,
    int BuffAttack)
{
    public int TotalAttack => Math.Max(1, BaseAttack + EquipmentAttack + BuffAttack);
}

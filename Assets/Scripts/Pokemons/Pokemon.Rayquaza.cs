public partial class Pokemon
{
    public bool HasRayquazaShield => CurrentHp > 0 && (GetShieldWithID(RayquazaBuff.ShieldId)?.Amount ?? 0) > 0;

    // This is resolved on the server AFTER shield consumption, then sent with
    // the damage RPC. Do not infer it from potentially delayed client list updates.
    public bool LastDamageProtectedScoring { get; private set; }

    private float GetRayquazaDamageMultiplier(Pokemon attacker)
    {
        return Type == PokemonType.Player && attacker.Type == PokemonType.Player
            && TeamMember.Team != Team.Neutral && attacker.TeamMember.Team != Team.Neutral
            && !TeamMember.IsOnSameTeam(attacker.TeamMember) && attacker.HasRayquazaShield
            ? RayquazaBuff.DamageMultiplier : 1f;
    }
}

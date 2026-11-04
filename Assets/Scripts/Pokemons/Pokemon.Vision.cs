using System;
using Unity.Netcode;

public partial class Pokemon
{
    private const double BushCombatRevealSeconds = 5;
    // Replicate the deadline, not a local countdown or a one-shot RPC: remote
    // clients and late joiners must agree with the server about concealment.
    private NetworkVariable<double> bushCombatRevealEndsAt = new NetworkVariable<double>();

    public bool IsRevealedByBushCombat => IsSpawned && CurrentHp > 0
        && bushCombatRevealEndsAt.Value > NetworkManager.ServerTime.Time;

    private void RecordBushCombat(Pokemon attacker, int resolvedDamage)
    {
        if (!IsServer || resolvedDamage <= 0 || attacker == null || attacker == this
            || Type != PokemonType.Player || attacker.Type != PokemonType.Player
            || attacker.TeamMember.IsOnSameTeam(TeamMember)) return;

        double deadline = NetworkManager.ServerTime.Time + BushCombatRevealSeconds;
        bushCombatRevealEndsAt.Value = Math.Max(bushCombatRevealEndsAt.Value, deadline);
        attacker.bushCombatRevealEndsAt.Value = Math.Max(attacker.bushCombatRevealEndsAt.Value, deadline);
    }

    private void ClearBushCombatReveal()
    {
        if (IsServer) bushCombatRevealEndsAt.Value = 0;
    }
}

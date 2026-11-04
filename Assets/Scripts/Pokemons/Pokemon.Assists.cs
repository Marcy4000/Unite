using System;
using System.Collections.Generic;

public partial class Pokemon
{
    private readonly CombatContributionLedger hostileContributions = new CombatContributionLedger();
    private readonly CombatContributionLedger supportContributions = new CombatContributionLedger();
    private KnockoutResult resolvedKnockout;
    private bool knockoutPublished;
    private bool knockoutExperienceAwarded;

    // Fired only on the server, once per credited player (killer or assistant).
    // Consumers must use the supplied result instead of maintaining KO histories.
    public event Action<KnockoutResult> OnKnockoutAwarded;
    public Pokemon CreditedKnockoutKiller => resolvedKnockout?.Killer;

    private bool CanRecordContribution => IsServer && CurrentHp > 0
        && (GameManager.Instance == null || GameManager.Instance.GameState == GameState.Playing);
    private static bool IsAssistPlayer(Pokemon player) => player != null && player.IsSpawned && player.Type == PokemonType.Player;

    private Pokemon ContributionPlayer(ulong id)
    {
        return NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var obj)
            && obj.TryGetComponent(out Pokemon player) && IsAssistPlayer(player) ? player : null;
    }

    // For successful protection/cleanses and future support mechanics which do
    // not apply a standard heal, shield, stat change or status effect.
    public void RecordSupport(Pokemon provider)
    {
        if (!CanRecordContribution || Type != PokemonType.Player || !IsAssistPlayer(provider)
            || provider == this || !provider.TeamMember.IsOnSameTeam(TeamMember)) return;
        supportContributions.Record(provider.NetworkObjectId, NetworkManager.ServerTime.Time);
    }

    public void RecordHostileContribution(Pokemon provider)
    {
        if (!CanRecordContribution || (Type != PokemonType.Player && Type != PokemonType.Objective)
            || !IsAssistPlayer(provider) || provider == this || provider.TeamMember.IsOnSameTeam(TeamMember)) return;
        hostileContributions.Record(provider.NetworkObjectId, NetworkManager.ServerTime.Time);
    }

    private void RecordEffectContribution(ulong sourceId, bool beneficial)
    {
        if (!CanRecordContribution) return;
        Pokemon provider = ContributionPlayer(sourceId);
        if (beneficial) RecordSupport(provider); else RecordHostileContribution(provider);
    }

    private static bool IsAssistSupportStatus(StatusEffect effect) => effect.Type == StatusType.Unstoppable
        || effect.Type == StatusType.Invincible || effect.Type == StatusType.Untargetable
        || effect.Type == StatusType.HindranceResistance || effect.Type == StatusType.Invisible
        || effect.Type == StatusType.SpeedReductionImmunity;

    // Persistent effects remain useful while active, including auras lasting
    // longer than the window. Removed effects cannot refresh their attribution.
    private void RefreshActiveContributions()
    {
        foreach (StatChange change in statChanges)
            if (change.HasSource && change.EffectiveAmount > 0) RecordEffectContribution(change.SourceId, change.IsBuff);
        foreach (StatusEffect effect in statusEffects)
            if (effect.HasSource && (effect.IsNegativeStatus() || IsAssistSupportStatus(effect)))
                RecordEffectContribution(effect.SourceId, !effect.IsNegativeStatus());
        foreach (ShieldInfo shield in shields)
            if (shield.HasSource && shield.Amount > 0) RecordEffectContribution(shield.SourceId, true);
    }

    private KnockoutResult ResolveKnockout(Pokemon damageSource)
    {
        double now = NetworkManager.ServerTime.Time;
        var hostile = hostileContributions.Recent(now);
        Pokemon killer = damageSource;
        if ((Type == PokemonType.Player || Type == PokemonType.Objective)
            && (damageSource.Type == PokemonType.Wild || damageSource.Type == PokemonType.Objective) && damageSource != this)
        {
            foreach (var entry in hostile)
            {
                Pokemon candidate = ContributionPlayer(entry.PlayerId);
                if (candidate == null || candidate == this || candidate.TeamMember.IsOnSameTeam(TeamMember)) continue;
                killer = candidate;
                break;
            }
        }

        var assistants = new List<Pokemon>();
        if ((Type == PokemonType.Player || Type == PokemonType.Objective) && IsAssistPlayer(killer)
            && killer != this && !killer.TeamMember.IsOnSameTeam(TeamMember))
        {
            killer.RefreshActiveContributions();
            var candidates = new List<CombatContributionLedger.Entry>(hostile);
            candidates.AddRange(killer.supportContributions.Recent(now));
            candidates.Sort(CombatContributionLedger.NewestFirst);
            var seen = new HashSet<ulong> { killer.NetworkObjectId, NetworkObjectId };
            foreach (var entry in candidates)
            {
                Pokemon candidate = ContributionPlayer(entry.PlayerId);
                if (candidate == null || !candidate.TeamMember.IsOnSameTeam(killer.TeamMember) || !seen.Add(entry.PlayerId)) continue;
                assistants.Add(candidate);
                if (assistants.Count == AssistRules.MaximumAssists) break;
            }
        }
        return new KnockoutResult(this, damageSource, killer, assistants.ToArray());
    }

    private void PublishKnockout(KnockoutResult result)
    {
        if (knockoutPublished) return;
        knockoutPublished = true;
        hostileContributions.Clear();
        supportContributions.Clear();
        curseEndsAt = 0;
        if (GameManager.Instance != null && GameManager.Instance.GameState != GameState.Playing) return;
        Pokemon killer = result.Killer;
        if (!IsAssistPlayer(killer) || (result.VictimType != PokemonType.Player && result.VictimType != PokemonType.Objective)) return;
        if (killer != this && !killer.TeamMember.IsOnSameTeam(TeamMember))
        {
            killer.OnKnockoutAwarded?.Invoke(result);
            foreach (Pokemon assistant in result.Assistants)
            {
                if (assistant == null || !assistant.IsSpawned) continue;
                assistant.OnKnockoutAwarded?.Invoke(result);
            }
        }
    }

    private void ResetCombatContributions()
    {
        hostileContributions.Clear();
        supportContributions.Clear();
        resolvedKnockout = null;
        knockoutPublished = false;
        knockoutExperienceAwarded = false;
    }

    private ulong[] KnockoutAssistantIds()
    {
        if (resolvedKnockout == null) return Array.Empty<ulong>();
        var ids = new ulong[resolvedKnockout.AssistantIds.Count];
        for (int i = 0; i < ids.Length; i++) ids[i] = resolvedKnockout.AssistantIds[i];
        return ids;
    }
}

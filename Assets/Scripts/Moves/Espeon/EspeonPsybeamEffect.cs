using System.Collections;
using UnityEngine;

public class EspeonPsybeamEffect : EspeonMoveEffect
{
    [SerializeField] private BoxCollider beamArea;
    protected override IEnumerator Execute()
    {
        LockActions(); FaceRPC(Direction);
        // Databin S12 is Psybeam and plays spell1b.
        PlayAnimation(EspeonMoveRules.Animation(Actor, "spell1b"), 0.45f);
        yield return CastPause(0.25f);
        if (CastInterrupted) yield break;
        Vector3 origin = Actor.transform.position + Vector3.up * 0.8f;
        Vector3 direction = Direction.normalized;
        Physics.SyncTransforms();
        Pokemon primary = null; float best = float.MaxValue;
        foreach (var target in CombatTargeting.SweptSphereTargets(origin, origin + direction * EspeonMoveRules.PsybeamRange, EspeonMoveRules.PsybeamWidth * 0.5f))
        {
            float along = Vector3.Dot(target.transform.position - Actor.transform.position, direction);
            if (CanHit(target) && along >= 0 && along < best) { primary = target; best = along; }
        }
        if (beamArea != null) beamArea.size = new Vector3(EspeonMoveRules.PsybeamWidth, 2, EspeonMoveRules.PsybeamRange);
        BeamVisual(origin, primary != null ? primary.transform.position + Vector3.up * 0.8f : origin + direction * EspeonMoveRules.PsybeamRange, EspeonMoveRules.PsybeamWidth * 0.3f);
        if (primary != null)
        {
            StatusEffect? stun = null; StatChange? slow = null;
            foreach (var effect in primary.StatusEffects)
                if ((effect.Type == StatusType.Incapacitated || effect.Type == StatusType.Frozen || effect.Type == StatusType.Asleep)
                    && (!stun.HasValue || effect.Duration > stun.Value.Duration)) stun = effect;
            foreach (var stat in primary.StatChanges)
                if (!stat.IsBuff && stat.AffectedStat == Stat.Speed && stat.Amount > 0
                    && (!slow.HasValue || stat.Amount > slow.Value.Amount)) slow = stat;
            float maximumHpBonus = 0.06f * primary.GetMaxHp();
            Vector3 splitOrigin = primary.transform.position + Vector3.up * 0.8f;
            // Snapshot before the primary hit; killing it must not remove the splits.
            var secondary = CombatTargeting.SphereTargets(splitOrigin, EspeonMoveRules.PsybeamRange * EspeonMoveRules.SplitRangeRatio);
            Apply(primary, maximumHpBonus, stun.HasValue, false);
            foreach (var target in secondary)
            {
                if (target == primary || !CanHit(target) || !EspeonMoveRules.InSplitFan(splitOrigin, target.transform.position + Vector3.up * 0.8f, direction)) continue;
                float cap = target.Type == PokemonType.Player ? 2000 : 1000;
                Apply(target, Mathf.Min(maximumHpBonus, cap), stun.HasValue, true);
                if (slow.HasValue) Slow(target, slow.Value.Amount, slow.Value.Duration);
                if (stun.HasValue) Stun(target, stun.Value.Duration);
                BeamVisual(splitOrigin, target.transform.position + Vector3.up * 0.8f, 0.3f);
            }
        }
        yield return CastPause(0.2f);
    }
    private void Apply(Pokemon target, float maxHpBonus, bool stunned, bool secondary)
    {
        float bonus = maxHpBonus;
        bool canStun = !target.HasStatusEffect(StatusType.Unstoppable) && !target.HasStatusEffect(StatusType.HindranceResistance);
        if (Upgraded && stunned && (!secondary || canStun))
            bonus += target.Type == PokemonType.Player ? target.GetMaxHp() * 0.05f : Mathf.Min(1000, target.GetMaxHp() * 0.05f);
        Hit(target, AddFlat(EspeonMoveRules.Psybeam(Actor.NetworkObjectId), bonus));
    }
}

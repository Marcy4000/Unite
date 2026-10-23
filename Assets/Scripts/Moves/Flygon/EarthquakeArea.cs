using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EarthquakeArea : FlygonMoveObject
{
    [SerializeField] private ParticleSystem landParticles;
    private bool landed;
    public override FlygonCast Kind => FlygonCast.Earthquake;
    public void Land(Vector3 point)
    {
        if (!IsServer || landed || (point - Origin).sqrMagnitude > 16) return;
        landed = true; Destination = point;
    }
    protected override void PresentPhase(byte value)
    {
        if (value <= 0 || landParticles == null) return;
        landParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        landParticles.Play(true);
    }
    protected override IEnumerator Execute()
    {
        transform.position = Destination;
        float timeout = Time.time + 2;
        while (Live && !landed && Time.time < timeout)
        {
            if (Actor.Pokemon.HasAnyStatusEffect(FlygonMoveRules.Interrupts)) yield break;
            yield return null;
        }
        if (!Live || !landed) yield break;
        transform.position = Destination;
        var stacks = new Dictionary<Pokemon, int>();
        for (int wave = 0; wave < 3 && Live; wave++)
        {
            SetPhase((byte)(wave + 1));
            bool shield = false;
            foreach (Pokemon target in TargetsInArea())
            {
                shield |= wave == 2 && target.Type == PokemonType.Player;
                target.TakeDamageRPC(FlygonMoveRules.Damage(Actor.NetworkObjectId, Kind, wave));
                if (!CanHit(target)) continue;
                stacks.TryGetValue(target, out int count); stacks[target] = ++count;
                if (CanControl(target))
                {
                    RefreshStat(target, Stat.Speed, count == 3 ? (short)25 : (short)20, count == 3 ? 3.2f : 0.6f);
                    if (count == 3 && !target.HasStatusEffect(StatusType.HindranceResistance)) target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 1.2f, true, EffectId));
                }
            }
            if (Upgraded && shield) Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(Actor.Pokemon.GetMaxHp() * 0.08f), EffectId, 0, 3, true));
            yield return Wait(wave == 2 ? 0.45f : FlygonMoveRules.QuakeInterval);
        }
    }
}

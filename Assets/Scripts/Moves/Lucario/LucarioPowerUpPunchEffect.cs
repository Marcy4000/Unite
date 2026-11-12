using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class LucarioPowerUpPunchEffect : LucarioMoveEffect
{
    private bool charging, completed;
    private Vector3 aim;
    protected override int UnlockedMoveSlot => charging ? 0 : -1;
    public bool Charging => charging;
    protected override IEnumerator Execute()
    {
        charging = true; Combat.Punch = this; aim = Direction;
        LockActions(false); PlayAnimation("spell1a", LucarioMoveRules.PunchHoldLimit);
        double began = NetworkManager.ServerTime.Time; Actor.MovesController.SetLucarioPunchBegan(began);
        Actor.Pokemon.AddStatChange(new StatChange(30, Stat.DamageReduction, LucarioMoveRules.PunchHoldLimit, true, true, false, EffectId));
        Actor.Pokemon.AddStatChange(new StatChange(15, Stat.Speed, LucarioMoveRules.PunchHoldLimit, true, false, true, EffectId));
        if (Upgraded) Unstoppable(LucarioMoveRules.PunchHoldLimit);
        bool released = false, cancelled = false;
        while (!CastInterrupted && NetworkManager.ServerTime.Time - began < LucarioMoveRules.PunchHoldLimit)
        {
            Actor.MovesController.LucarioPunchInput(out aim, out released, out cancelled);
            if (aim.sqrMagnitude > 0.001f) FaceRPC(aim);
            ChargeVisualRPC(LucarioMoveRules.ChargeFraction((float)(NetworkManager.ServerTime.Time - began)));
            if (released) break;
            yield return null;
        }
        float held = (float)(NetworkManager.ServerTime.Time - began);
        bool failed = cancelled || !released || held >= LucarioMoveRules.PunchHoldLimit || CastInterrupted;
        charging = false; Actor.MovesController.SetLucarioPunchBegan(-1);
        Actor.Pokemon.RemoveAllStatChangeWithIDRPC(EffectId); Actor.Pokemon.RemoveStatusEffectWithID(EffectId);
        Actor.MovesController.StartLucarioPunchCooldown(failed); completed = true;
        if (failed) { UnlockActions(); yield break; }
        bool empowered = Combat.SpendAura();
        LockActions(); FaceRPC(aim); PhaseRPC(1);
        float distance = LucarioMoveRules.PunchRange(held);
        PlayAnimation(empowered ? "spell1a5" : "spell1a2", distance / LucarioMoveRules.PunchSpeed + LucarioMoveRules.DashRecovery);
        yield return Dash(aim, distance, LucarioMoveRules.PunchSpeed, LucarioMoveRules.PunchWidth, target =>
        {
            int missing = target.GetMaxHp() - target.CurrentHp;
            if (target.ResolveDamage(LucarioMoveRules.Punch(Actor.NetworkObjectId, held, Actor.Pokemon.CurrentLevel, missing, target.Type == PokemonType.Wild, empowered)) <= 0) return;
            Combat.ReadyBoosted(); Shove(target, aim, LucarioMoveRules.PunchShove, empowered ? 1 : 0.5f);
            Visual("Impact", target.transform.position, 1);
        });
        if (CastInterrupted) yield break;
        PhaseRPC(2); PlayAnimation("spell1a3", LucarioMoveRules.DashRecovery); yield return CastPause(LucarioMoveRules.DashRecovery); UnlockActions();
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ChargeVisualRPC(float fraction)
    {
        var visual = transform.Find("Graphics/Cast/ChargeOrb"); if (visual != null) visual.localScale = Vector3.one * Mathf.Lerp(0.3f, 1, fraction);
    }
    protected override void Cleanup()
    {
        if (IsServer && Actor != null && Actor.IsSpawned)
        {
            Actor.MovesController.SetLucarioPunchBegan(-1);
            if (!completed) Actor.MovesController.StartLucarioPunchCooldown(true);
            if (Combat != null && Combat.Punch == this) Combat.Punch = null;
        }
        base.Cleanup();
    }
}

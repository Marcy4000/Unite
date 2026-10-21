using UnityEngine;

// Serialized factory name retained for compatibility with existing licenses.
public class MarshadwPassive : PassiveBase
{
    private readonly MarshadowStackState stacks = new MarshadowStackState();
    private int effect;
    public int BuffStacks => stacks.Count;
    public override void Start(PlayerManager controller)
    {
        if (playerManager != null)
        {
            playerManager.Pokemon.OnDeath -= OnDeath;
            playerManager.MovesController.onMovePerformed -= OnMovePerformed;
        }
        base.Start(controller);
        controller.Pokemon.OnDeath += OnDeath;
        controller.MovesController.onMovePerformed += OnMovePerformed;
    }
    private void OnMovePerformed(MoveBase move)
    {
        // Reworked moves commit at cast start, not again when recasts expire.
        if (!(move is MarshadowMoveBase) && !(move is MarshadowShadowSneak) && !(move is MarshadowUnite)) OnMoveCommitted();
    }
    public void OnMoveCommitted()
    {
        if (playerManager.Pokemon.CurrentHp <= 0) return;
        stacks.Gain(Time.time); Apply();
    }
    public void OnBoostedHit() { OnMoveCommitted(); }
    private void Apply()
    {
        RemoveStats();
        if (stacks.Count > 0)
        {
            short amount = (short)(5 * stacks.Count);
            playerManager.Pokemon.AddStatChange(new StatChange(amount, Stat.Attack, MarshadowMoveRules.PassiveDuration, true, true, true, MarshadowMoveRules.AttackBuff));
            playerManager.Pokemon.AddStatChange(new StatChange(amount, Stat.AtkSpeed, MarshadowMoveRules.PassiveDuration, true, true, false, MarshadowMoveRules.AttackSpeedBuff));
            playerManager.Pokemon.AddStatChange(new StatChange(amount, Stat.LifeSteal, MarshadowMoveRules.PassiveDuration, true, true, false, MarshadowMoveRules.LifeStealBuff));
            MarshadowMoveRuntime.EndEffect(playerManager, ref effect);
            effect = MarshadowMoveRuntime.Effect(playerManager, MarshadowEffect.Passive, MarshadowMoveRules.PassiveDuration, stage: stacks.Count);
        }
    }
    private void RemoveStats()
    {
        playerManager.Pokemon.RemoveAllStatChangeWithIDRPC(MarshadowMoveRules.AttackBuff);
        playerManager.Pokemon.RemoveAllStatChangeWithIDRPC(MarshadowMoveRules.AttackSpeedBuff);
        playerManager.Pokemon.RemoveAllStatChangeWithIDRPC(MarshadowMoveRules.LifeStealBuff);
    }
    public override void Update()
    {
        if (stacks.Tick(Time.time)) { RemoveStats(); MarshadowMoveRuntime.EndEffect(playerManager, ref effect); }
    }
    private void OnDeath(DamageInfo _)
    {
        stacks.Clear(); RemoveStats(); MarshadowMoveRuntime.EndEffect(playerManager, ref effect);
    }
}

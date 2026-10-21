public class JoltCharge : MoveBase
{
    private int effect;
    public JoltCharge() { Name = "Charge"; Cooldown = 9f; }
    public override void Update() { }

    public override void Finish()
    {
        if (IsActive)
        {
            bool empowered = (playerManager.PassiveController.Passive as JoltPassive)?.IsPassiveReady == true;
            float duration = empowered ? 6f : 5f;
            // Preserve the full meter for the following offensive move.
            playerManager.Pokemon.RemoveAllStatChangeWithIDRPC(JoltMoveRules.ChargeBuff);
            playerManager.Pokemon.AddStatChange(new StatChange((short)(empowered ? 35 : 25), Stat.Attack, duration, true, true, true, JoltMoveRules.ChargeBuff));
            short defenses = (short)((empowered ? 40 : 30) + (IsUpgraded ? 10 : 0));
            playerManager.Pokemon.AddStatChange(new StatChange(defenses, Stat.Defense, duration, true, true, true, JoltMoveRules.ChargeBuff));
            playerManager.Pokemon.AddStatChange(new StatChange(defenses, Stat.SpDefense, duration, true, true, true, JoltMoveRules.ChargeBuff));
            playerManager.Pokemon.AddStatChange(new StatChange((short)(empowered ? 35 : 30), Stat.Speed, empowered ? 5f : 2.5f, true, true, true, JoltMoveRules.ChargeBuff));
            // A heavy, brief slow instead of a root. The extra deduction cancels
            // Charge's own bonus: net -75% before the shared movement-speed tax.
            JoltMoveRuntime.RefreshStat(playerManager.Pokemon, new StatChange((short)(JoltMoveRules.ChargeSlow + (empowered ? 35 : 30)), Stat.Speed,
                JoltMoveRules.ChargeSlowDuration, true, false, true, JoltMoveRules.ChargeSlowId));
            JoltMoveRuntime.EndEffect(playerManager, ref effect);
            effect = JoltMoveRuntime.Effect(playerManager, JoltEffect.Charge, empowered, IsUpgraded);
            playerManager.AnimationManager.PlayAnimation("pm0135_00_ba21_tokusyu02");
            wasMoveSuccessful = true;
        }
        base.Finish();
    }

    public override void ResetMove()
    {
        IsActive = false;
        JoltMoveRuntime.EndEffect(playerManager, ref effect);
        if (playerManager != null && playerManager.IsSpawned)
        {
            playerManager.Pokemon.RemoveAllStatChangeWithIDRPC(JoltMoveRules.ChargeBuff);
            playerManager.Pokemon.RemoveAllStatChangeWithIDRPC(JoltMoveRules.ChargeSlowId);
        }
    }
}

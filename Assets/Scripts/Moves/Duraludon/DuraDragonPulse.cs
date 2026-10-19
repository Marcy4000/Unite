using UnityEngine;

public class DuraDragonPulse : MoveBase
{
    private float chargeStarted;
    private bool charging;
    private bool configured, configuredUpgrade;
    private const ushort ChargeSlowId = 8840;
    private DuraBoostedObj Combat => (playerManager.MovesController.BasicAttack as DuraBasicAtk)?.Combat;

    public DuraDragonPulse()
    {
        Name = "Dragon Pulse";
        Cooldown = DuraMoveRules.PulseCooldown;
    }

    public override void Start(PlayerManager controller)
    {
        if (charging) return;
        base.Start(controller);
        if (Combat == null) { base.Cancel(); return; }
        Configure();
        charging = true;
        chargeStarted = Time.time;
        playerManager.Pokemon.AddStatChange(new StatChange(30, Stat.Speed, 0, false, false, true, ChargeSlowId));
        playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Busy);
        playerManager.MovesController.AddMoveStatus(1, ActionStatusType.Charging);
        playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Charging);
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);
        playerManager.AnimationManager.PlayAnimation("ani_spell1b1_bat_0884");
        Aim.Instance.InitializeSimpleCircle(DuraMoveRules.PulseRadius(0));
        playerManager.HPBar.ShowGenericGuage(true);
    }

    private void Configure()
    {
        if (Combat != null && (!configured || configuredUpgrade != IsUpgraded))
        {
            Combat.EnableDragonPulseRPC(IsUpgraded);
            configured = true;
            configuredUpgrade = IsUpgraded;
        }
    }

    public override void Update()
    {
        if (playerManager == null) return;
        Configure(); // Marks are enabled on learning, not on first cast.
        if (!charging) return;
        if (playerManager.Pokemon.HasStatusEffect(StatusType.Incapacitated)
            || playerManager.Pokemon.HasStatusEffect(StatusType.Asleep)
            || playerManager.Pokemon.HasStatusEffect(StatusType.Frozen)
            || playerManager.Pokemon.HasStatusEffect(StatusType.Bound))
        {
            Cancel();
            return;
        }
        float elapsed = Time.time - chargeStarted;
        float charge = Mathf.Clamp01(elapsed / DuraMoveRules.PulseChargeTime);
        Aim.Instance.InitializeSimpleCircle(DuraMoveRules.PulseRadius(charge));
        playerManager.HPBar.UpdateGenericGuageValue(charge, 1f);
        if (elapsed >= DuraMoveRules.PulseChargeTime) Finish();
    }

    public override void Finish()
    {
        if (!IsActive || !charging) return;
        float charge = Mathf.Clamp01((Time.time - chargeStarted) / DuraMoveRules.PulseChargeTime);
        if (Combat != null)
        {
            Combat.DragonPulseRPC(charge);
            playerManager.AnimationManager.PlayAnimation("ani_spell1b2_bat_0884");
            wasMoveSuccessful = true;
        }
        EndCharge();
        base.Finish();
    }

    private void EndCharge()
    {
        if (!charging) return;
        charging = false;
        playerManager.Pokemon.RemoveStatChangeWithIDRPC(ChargeSlowId);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Busy);
        playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Charging);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Charging);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        playerManager.HPBar.ShowGenericGuage(false);
        Aim.Instance.HideSimpleCircle();
    }

    public override void Cancel()
    {
        EndCharge();
        base.Cancel();
    }

    public override void ResetMove() { Cancel(); }
}

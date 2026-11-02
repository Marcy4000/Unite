using UnityEngine;

public abstract class EspeonMove : PrefabMove
{
    private Sprite bonusIcon;
    public override Sprite IconOverride => playerManager != null && EspeonMoveRules.MoveOne(MoveId) && playerManager.MovesController.EspeonBonusReady
        ? bonusIcon != null ? bonusIcon : bonusIcon = Resources.Load<Sprite>(EspeonMoveRules.Visuals + (MoveId == AvailableMoves.EspeonPsyshock ? "BonusPsyshock" : "BonusStoredPower")) : null;
    public override string PrefabPath => EspeonMoveRules.Folder + MoveId + ".prefab";
    // The server commits cooldowns after validating ammunition/bonus casts.
    public override bool CooldownOnFinish => false;
    public override void Start(PlayerManager controller)
    {
        if (MoveId == AvailableMoves.EspeonStoredPower && !controller.MovesController.EspeonBonusReady
            && controller.MovesController.EspeonBlastCount == 0) return;
        base.Start(controller);
    }
    public override void Update()
    {
        base.Update();
        if (IsActive && MoveId == AvailableMoves.EspeonStoredPower && !playerManager.MovesController.EspeonBonusReady
            && playerManager.MovesController.EspeonBlastCount == 0) Cancel();
    }
}
public class EspeonSwift : EspeonMove
{
    public override AvailableMoves MoveId => AvailableMoves.EspeonSwift;
    public override MoveAimStyle AimStyle => MoveAimStyle.Ground;
    public override float Range => EspeonMoveRules.FocusRange;
    public override float AreaRadius => EspeonMoveRules.FocusRadius;
    public EspeonSwift() { Name = "Swift"; Cooldown = 6; }
}
public class EspeonPsyshock : EspeonSwift
{
    public override AvailableMoves MoveId => AvailableMoves.EspeonPsyshock;
    public EspeonPsyshock() { Name = "Psyshock"; Cooldown = 5; }
}
public class EspeonGrowl : EspeonMove
{
    public override AvailableMoves MoveId => AvailableMoves.EspeonGrowl;
    public override MoveAimStyle AimStyle => MoveAimStyle.Target;
    public override float Range => EspeonMoveRules.TargetRange;
    public EspeonGrowl() { Name = "Growl"; Cooldown = 7; }
}
public class EspeonFutureSight : EspeonGrowl
{
    public override AvailableMoves MoveId => AvailableMoves.EspeonFutureSight;
    public EspeonFutureSight() { Name = "Future Sight"; Cooldown = 7; }
}
public class EspeonStoredPower : EspeonMove
{
    public override AvailableMoves MoveId => AvailableMoves.EspeonStoredPower;
    public override MoveAimStyle AimStyle => MoveAimStyle.Circle;
    public override float Range => EspeonMoveRules.StoredRange;
    public EspeonStoredPower() { Name = "Stored Power"; Cooldown = 5; }
}
public class EspeonPsybeam : EspeonMove
{
    public override AvailableMoves MoveId => AvailableMoves.EspeonPsybeam;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => EspeonMoveRules.PsybeamRange;
    public EspeonPsybeam() { Name = "Psybeam"; Cooldown = 7; }
}
public class EspeonPsychicSolare : EspeonMove
{
    public override AvailableMoves MoveId => AvailableMoves.EspeonPsychicSolare;
    public override MoveAimStyle AimStyle => MoveAimStyle.Circle;
    public override float Range => EspeonMoveRules.UniteRadius;
    public EspeonPsychicSolare() { Name = "Psychic Solare"; Cooldown = 0; }
}

using UnityEngine;

public class CresseliaUnite : CombatMove
{
    public override AvailableMoves MoveId => AvailableMoves.CresseliaUnite;
    public override string PrefabPath => null;
    public override MoveAimStyle AimStyle => MoveAimStyle.Circle;
    public override float Range => CresseliaMoveRules.UniteRadius;
    public override bool CooldownOnEffectEnd => true;
    public override bool IsOffensive => true;
    public override bool LockDuringLoad => true;
    public bool CanRecast => playerManager?.MovesController?.CresseliaRuntime?.CanRecastUnite ?? false;

    public CresseliaUnite()
    {
        Name = "Crescent Crash";
        Cooldown = 0f;
    }

    public override void Start(PlayerManager controller)
    {
        _ = controller?.MovesController?.CresseliaRuntime;
        base.Start(controller);
    }

}

using UnityEngine;

public class GlaceUnite : GlaceonMove
{
    public override AvailableMoves MoveId => AvailableMoves.GlaceonUnite;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => 6;
    public override string PrefabPath => GlaceonMoveRules.Folder + "GlaceonUnite.prefab";
    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        if (IsActive)
        {
            ActorAim.HideSkillshotAim();
            ActorAim.InitializeGlaceonUniteAim();
        }
    }
    public override void Finish() { base.Finish(); ActorAim.HideGlaceonUniteAim(); }
    public override void Cancel() { base.Cancel(); if (ActorAim != null) ActorAim.HideGlaceonUniteAim(); }
    public GlaceUnite() { Name = "Glacial Stage"; Cooldown = GlaceonMoveRules.Cooldown(MoveId); }
}

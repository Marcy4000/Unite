using UnityEngine;

public class AbsolFeint : PrefabMove
{
    private bool right;
    public AbsolFeint() { Name = "Feint"; Cooldown = AbsolMoveRules.FeintCooldown; }
    public override AvailableMoves MoveId => AvailableMoves.AbsolFeint;
    public override string PrefabPath => AbsolMoveRules.Folder + "AbsolFeint.prefab";
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => AbsolMoveRules.DashDistance;
    public override float IndicatorRange => AbsolMoveRules.DashIndicatorRange;
    public override string IndicatorPath => "AimIndicators/AbsolArc";
    public override Vector3 IndicatorScale => new Vector3(AbsolMoveRules.DashBow, 1, IndicatorRange);
    public override bool MirrorsIndicator => !right;
    protected override void OnCommitted() { right = !right; }
}

public class AbsolNightSlash : PrefabMove
{
    private bool second;
    public AbsolNightSlash() { Name = "Night Slash"; Cooldown = AbsolMoveRules.NightCooldown; }
    public override AvailableMoves MoveId => AvailableMoves.AbsolNightSlash;
    public override string PrefabPath => AbsolMoveRules.Folder + "AbsolNightSlash.prefab";
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => AbsolMoveRules.DashDistance;
    public override float IndicatorRange => AbsolMoveRules.DashIndicatorRange;
    public override bool CooldownOnEffectEnd => true;
    public override string IndicatorPath => second ? null : "AimIndicators/AbsolArc";
    public override Vector3 IndicatorScale => new Vector3(AbsolMoveRules.DashBow, 1, IndicatorRange);
    protected override void OnExecutionState(bool finished) { second = !finished; }
    public override void ResetMove() { base.ResetMove(); second = false; }
}

public class AbsolPursuit : PrefabMove
{
    public AbsolPursuit() { Name = "Pursuit"; Cooldown = AbsolMoveRules.PursuitCooldown; }
    public override AvailableMoves MoveId => AvailableMoves.AbsolPursuit;
    public override string PrefabPath => AbsolMoveRules.Folder + "AbsolPursuit.prefab";
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => AbsolMoveRules.PursuitDistance;
    public override float IndicatorRange => AbsolMoveRules.PursuitIndicatorRange;
    public override bool IsOffensive => false;
}

public class AbsolSlash : PrefabMove
{
    public AbsolSlash() { Name = "Slash"; Cooldown = AbsolMoveRules.SlashCooldown; }
    public override AvailableMoves MoveId => AvailableMoves.AbsolSlash;
    public override string PrefabPath => AbsolMoveRules.Folder + "AbsolSlash.prefab";
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => AbsolMoveRules.SlashRange;
    public override string IndicatorPath => "AimIndicators/AbsolSlash";
    public override Vector3 IndicatorScale => new Vector3(Range, 1, Range);
}

public class AbsolPsychoCut : PrefabMove
{
    public AbsolPsychoCut() { Name = "Psycho Cut"; Cooldown = AbsolMoveRules.PsychoCooldown; }
    public override AvailableMoves MoveId => AvailableMoves.AbsolPsychoCut;
    public override string PrefabPath => AbsolMoveRules.Folder + "AbsolPsychoCut.prefab";
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => AbsolMoveRules.PsychoRange;
    public override float CameraReturnDelay => AbsolMoveRules.PsychoCameraReturnDelay;
}

public class AbsolSuckerPunch : PrefabMove
{
    public AbsolSuckerPunch() { Name = "Sucker Punch"; Cooldown = AbsolMoveRules.SuckerCooldown; }
    public override AvailableMoves MoveId => AvailableMoves.AbsolSuckerPunch;
    public override string PrefabPath => AbsolMoveRules.Folder + "AbsolSuckerPunch.prefab";
    public override MoveAimStyle AimStyle => MoveAimStyle.Target;
    public override float Range => AbsolMoveRules.SuckerRange;
    public override float TargetRangeTolerance => 0;
}

public class AbsolMidnightSlash : PrefabMove
{
    public AbsolMidnightSlash() { Name = "Midnight Slash"; Cooldown = 0; }
    public override AvailableMoves MoveId => AvailableMoves.AbsolMidnightSlash;
    public override string PrefabPath => AbsolMoveRules.Folder + "AbsolMidnightSlash.prefab";
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => AbsolMoveRules.UniteLength;
    public override float IndicatorRange => AbsolMoveRules.UniteIndicatorRange;
    public override float CameraReturnDelay => AbsolMoveRules.UniteCameraReturnDelay;
    public override bool LockDuringLoad => true;
    public override string IndicatorPath => "AimIndicators/AbsolMidnightSlash";
    public override Vector3 IndicatorScale => new Vector3(AbsolMoveRules.UniteWidth, 1, IndicatorRange);
}

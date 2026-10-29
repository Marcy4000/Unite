using UnityEngine;

public abstract class NinetalesMove : PrefabMove
{
    public override string PrefabPath => NinetalesMoveRules.Folder + MoveId + ".prefab";
    public override bool LockDuringLoad => true;
}
public class NinetalesPowderSnow : NinetalesMove
{
    public NinetalesPowderSnow() { Name = "Powder Snow"; Cooldown = 5; }
    public override AvailableMoves MoveId => AvailableMoves.NinetalesPowderSnow;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => NinetalesMoveRules.ConeRange;
    public override string IndicatorPath => "AimIndicators/NinetalesCone";
    public override Vector3 IndicatorScale => new Vector3(Range, 1, Range);
}
public class NinetalesIcyWind : NinetalesMove
{
    public NinetalesIcyWind() { Name = "Icy Wind"; Cooldown = 8; }
    public override AvailableMoves MoveId => AvailableMoves.NinetalesIcyWind;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => NinetalesMoveRules.IcyRange;
    public override string IndicatorPath => "AimIndicators/NinetalesWind";
    public override Vector3 IndicatorScale => new Vector3(NinetalesMoveRules.IcyWidth, 1, Range);
}
public class NinetalesAvalanche : NinetalesMove
{
    public NinetalesAvalanche() { Name = "Avalanche"; Cooldown = 7; }
    public override AvailableMoves MoveId => AvailableMoves.NinetalesAvalanche;
    public override MoveAimStyle AimStyle => MoveAimStyle.Ground;
    public override float Range => NinetalesMoveRules.AvalancheRange;
    public override float AreaRadius => NinetalesMoveRules.CollapseRadius;
}
public class NinetalesDazzlingGleam : NinetalesMove
{
    public NinetalesDazzlingGleam() { Name = "Dazzling Gleam"; Cooldown = 7; }
    public override AvailableMoves MoveId => AvailableMoves.NinetalesDazzlingGleam;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => NinetalesMoveRules.ConeRange;
    public override string IndicatorPath => "AimIndicators/NinetalesCone";
    public override Vector3 IndicatorScale => new Vector3(Range, 1, Range);
}
public class NinetalesBlizzard : NinetalesMove
{
    public NinetalesBlizzard() { Name = "Blizzard"; Cooldown = 7; }
    public override AvailableMoves MoveId => AvailableMoves.NinetalesBlizzard;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => NinetalesMoveRules.BlizzardRange;
    public override string IndicatorPath => "AimIndicators/NinetalesWind";
    public override Vector3 IndicatorScale => new Vector3(NinetalesMoveRules.BlizzardWidth, 1, Range);
}
public class NinetalesAuroraVeil : NinetalesMove
{
    public NinetalesAuroraVeil() { Name = "Aurora Veil"; Cooldown = 12; }
    public override AvailableMoves MoveId => AvailableMoves.NinetalesAuroraVeil;
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => NinetalesMoveRules.VeilDash;
    public override bool IsOffensive => false;
    public override float EffectiveCooldown(bool upgraded) => upgraded ? 10 : 12;
    public override void Update() { Cooldown = IsUpgraded ? 10 : 12; base.Update(); }
}
public class NinetalesSnowGlobe : NinetalesMove
{
    public NinetalesSnowGlobe() { Name = "Snow Globe"; Cooldown = 0; }
    public override AvailableMoves MoveId => AvailableMoves.NinetalesSnowGlobe;
    public override MoveAimStyle AimStyle => MoveAimStyle.Circle;
    public override float Range => NinetalesMoveRules.UniteRadius;
}

public class FlareonHeatWave : FlareonMoveBase
{
    public override FlareonCast Kind => FlareonCast.HeatWave;
    public FlareonHeatWave() { Name = "Heat Wave"; Cooldown = FlareonMoveRules.Cooldown(Kind); }
}

public class FlareonFlareBlitz : FlareonMoveBase
{
    public override FlareonCast Kind => FlareonCast.FlareBlitz;
    public FlareonFlareBlitz() { Name = "Flare Blitz"; Cooldown = FlareonMoveRules.Cooldown(Kind); }
}

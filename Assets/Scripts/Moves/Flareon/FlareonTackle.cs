public class FlareonTackle : FlareonMoveBase
{
    public override FlareonCast Kind => FlareonCast.Tackle;
    public FlareonTackle() { Name = "Tackle"; Cooldown = FlareonMoveRules.Cooldown(Kind); }
}

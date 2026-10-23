public class FlareonSwift : FlareonMoveBase
{
    public override FlareonCast Kind => FlareonCast.Swift;
    public FlareonSwift() { Name = "Swift"; Cooldown = FlareonMoveRules.Cooldown(Kind); }
}

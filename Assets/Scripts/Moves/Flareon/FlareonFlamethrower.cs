public class FlareonFlamethrower : FlareonMoveBase
{
    public override FlareonCast Kind => FlareonCast.Flamethrower;
    public FlareonFlamethrower() { Name = "Flamethrower"; Cooldown = FlareonMoveRules.Cooldown(Kind); }
}

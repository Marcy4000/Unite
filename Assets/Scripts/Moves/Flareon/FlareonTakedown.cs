public class FlareonTakedown : FlareonMoveBase
{
    public override FlareonCast Kind => FlareonCast.Takedown;
    public FlareonTakedown() { Name = "Takedown"; Cooldown = FlareonMoveRules.Cooldown(Kind); }
}

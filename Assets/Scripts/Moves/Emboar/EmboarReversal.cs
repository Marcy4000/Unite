// Keep the existing script/enum identity so saved licenses and preview references remain valid.
public class EmboarReversal : EmboarMoveBase
{
    public override EmboarCast Kind => EmboarCast.Flamethrower;
    public EmboarReversal() { Name = "Flamethrower"; Cooldown = 7; }
}

public class EmboarSmog : EmboarMoveBase
{
    public override EmboarCast Kind => EmboarCast.Smog;
    public EmboarSmog() { Name = "Smog"; Cooldown = 8; }
}

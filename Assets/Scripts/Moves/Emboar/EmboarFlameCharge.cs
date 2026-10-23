public class EmboarFlameCharge : EmboarMoveBase
{
    public override EmboarCast Kind => EmboarCast.FlameCharge;
    public EmboarFlameCharge() { Name = "Flame Charge"; Cooldown = 9; }
}

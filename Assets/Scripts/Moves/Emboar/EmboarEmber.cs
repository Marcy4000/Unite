public class EmboarEmber : EmboarMoveBase
{
    public override EmboarCast Kind => EmboarCast.Ember;
    public EmboarEmber() { Name = "Ember"; Cooldown = 6.5f; }
}

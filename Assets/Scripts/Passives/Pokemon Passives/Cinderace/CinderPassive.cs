// Blaze is evaluated on the server by MovesController.Cinderace, including for
// remote players. The owner-only passive must not apply duplicate stat changes.
public class CinderPassive : PassiveBase
{
    public override void Update() { }
}

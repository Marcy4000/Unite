// Gameplay lives on the server in MovesController.Meowstic. The owner-only
// PassiveController must not independently remove/reflect replicated effects.
public class MeowsticPassiveM : PassiveBase
{
    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        controller.MovesController.EnsureMeowsticDecoration();
    }
    public override void Update() { }
}

public class CresseliaPassive : PassiveBase
{
    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        controller.MovesController.EnsureCresseliaDecoration();
    }
    // Escort speed and Lunar Rhythm are authoritative on MovesController.
    // This owner-only passive owns presentation, never gameplay timers.
    public override void Update() { }
}

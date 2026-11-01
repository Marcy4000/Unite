public class CresseliaPassive : PassiveBase
{
    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        _ = controller?.MovesController?.CresseliaRuntime;
        controller?.MovesController?.EnsureCresseliaDecoration();
    }
    public override void Update() { }
}

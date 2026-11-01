public class VaporeonPassive : PassiveBase
{
    public override void Start(PlayerManager controller) { base.Start(controller); }
    public override void Update() { }
    public override void Dispose() { playerManager = null; }
}

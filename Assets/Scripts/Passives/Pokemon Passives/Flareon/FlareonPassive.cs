public class FlareonPassive : PassiveBase
{
    public int Stacks => (playerManager?.MovesController?.Combat?.Character as FlareonCharacterRuntime)?.Stacks ?? 0;
    public override void Update() { }
    public override void Dispose() => playerManager = null;
}

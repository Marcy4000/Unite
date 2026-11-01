public class SylvPassive : PassiveBase
{
    private SylveonCharacterRuntime Character => playerManager?.MovesController?.Combat?.Character as SylveonCharacterRuntime;
    public int Stacks => Character?.PassiveStacks ?? 0;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        _ = Character;
    }

    public override void Update() { }
    public override void Dispose() => playerManager = null;
}

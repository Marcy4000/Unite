public class WiseGlasses : HeldItemBase
{
    private StatChange spAtkBoost = new StatChange(7, Stat.SpAttack, 0f, false, true, true, 0, false);

    public override void Initialize(PlayerManager controller)
    {
        base.Initialize(controller);
        playerManager.Pokemon.AddStatChange(spAtkBoost);
    }

    public override void Update()
    {
        // Nothing
    }

    public override void Reset()
    {
        // Nothing
    }
}

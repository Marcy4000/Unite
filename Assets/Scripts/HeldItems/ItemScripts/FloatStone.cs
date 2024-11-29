public class FloatStone : HeldItemBase
{
    private bool gaveBoost;
    private StatChange speedBoost = new StatChange(20, Stat.Speed, 0f, false, true, true, 16);

    public override void Update()
    {
        if (playerManager.Pokemon.IsOutOfCombat && !gaveBoost)
        {
            playerManager.Pokemon.AddStatChange(speedBoost);
            gaveBoost = true;
        }
        else if (!playerManager.Pokemon.IsOutOfCombat && gaveBoost)
        {
            playerManager.Pokemon.RemoveStatChangeRPC(speedBoost);
            gaveBoost = false;
        }
    }

    public override void Reset()
    {
        // Nothing
    }
}

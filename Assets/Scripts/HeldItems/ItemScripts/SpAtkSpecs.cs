public class SpAtkSpecs : HeldItemBase
{
    private int stackAmount = 0;
    private StatChange stack = new StatChange(16, Stat.SpAttack, 0f, false, true, false, 0, false);

    public override void Initialize(PlayerManager controller)
    {
        base.Initialize(controller);
        controller.onGoalScored += OnGoalScored;
    }

    private void OnGoalScored(int amount)
    {
        if (stackAmount >= 5)
        {
            return;
        }

        playerManager.Pokemon.AddStatChange(stack);
        stackAmount++;
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

public class GoalGetter : BattleItemBase
{
    private float boostDuration = 10f;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        wasUseSuccessful = false;

        Cooldown = 70f;
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }
    }

    public override void Finish()
    {
        if (IsActive)
        {
            wasUseSuccessful = true;
            playerManager.AddScoreBoostRPC(new ScoreBoost(0, ScoreSpeedFactor.GoalGetter, boostDuration, true));
        }
        base.Finish();
    }
}

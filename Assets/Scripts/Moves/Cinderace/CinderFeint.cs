public class CinderFeint : MoveBase
{
    private StatusEffect invincibleEffect = new StatusEffect(StatusType.Invincible, 3f, true, 0);
    private StatChange speedBoost = new StatChange(40, Stat.Speed, 2f, true, true, true, 0);

    public CinderFeint()
    {
        Name = "Feint";
        Cooldown = 9.0f;
    }

    public override void Update()
    {
        // Nothing but must be implemented
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.AnimationManager.PlayAnimation("ani_spell2b_bat_0815");
            playerManager.Pokemon.AddStatusEffect(invincibleEffect);
            playerManager.Pokemon.AddStatChange(speedBoost);
            wasMoveSuccessful = true;
        }
        base.Finish();
    }
}

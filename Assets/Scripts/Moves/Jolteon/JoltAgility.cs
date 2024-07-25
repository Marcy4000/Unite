public class JoltAgility : MoveBase
{
    private StatusEffect hinderanceResistance = new StatusEffect(StatusType.HindranceResistance, 0.5f, true, 0);

    private StatChange speedBoost = new StatChange(35, Stat.Speed, 3f, true, true, true, 0);

    public JoltAgility()
    {
        Name = "Agility";
        Cooldown = 7f;
    }

    public override void Update()
    {
        
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.Pokemon.AddStatusEffect(hinderanceResistance);
            playerManager.Pokemon.AddStatChange(speedBoost);
            wasMoveSuccessful = true;
        }
        base.Finish();
    }

    public override void ResetMove()
    {
    }
}

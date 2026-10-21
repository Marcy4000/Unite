public class JoltAgility : MoveBase
{
    private int effect;
    public JoltAgility() { Name = "Agility"; Cooldown = 7f; }
    public override void Update() { }
    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.Pokemon.AddStatusEffect(new StatusEffect(StatusType.HindranceResistance, 0.5f, true, 0));
            playerManager.Pokemon.AddStatChange(new StatChange(35, Stat.Speed, 3f, true, true, true, 0));
            JoltMoveRuntime.EndEffect(playerManager, ref effect);
            effect = JoltMoveRuntime.Effect(playerManager, JoltEffect.Agility);
            wasMoveSuccessful = true;
        }
        base.Finish();
    }
    public override void ResetMove() { IsActive = false; JoltMoveRuntime.EndEffect(playerManager, ref effect); }
}

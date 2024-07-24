public class XAttack : BattleItemBase
{
    private StatChange atkBoost = new StatChange(20, Stat.Attack, 7f, true, true, true, 0);
    private StatChange spAtkBoost = new StatChange(20, Stat.SpAttack, 7f, true, true, true, 0);
    private StatChange atkSpdBoost = new StatChange(25, Stat.AtkSpeed, 7f, true, true, true, 0);

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        wasUseSuccessful = false;

        Cooldown = 40f;
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
            playerManager.Pokemon.AddStatChange(atkBoost);
            playerManager.Pokemon.AddStatChange(spAtkBoost);
            playerManager.Pokemon.AddStatChange(atkSpdBoost);
        }
        base.Finish();
    }
}

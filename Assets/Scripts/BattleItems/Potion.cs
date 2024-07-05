using UnityEngine;

public class Potion : BattleItemBase
{
    private float healAmount = 0.20f;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        wasUseSuccessful = false;

        Cooldown = 30f;
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
            playerManager.Pokemon.HealDamage(Mathf.RoundToInt((playerManager.Pokemon.GetMaxHp() * healAmount) + 160f));
        }
        base.Finish();
    }
}

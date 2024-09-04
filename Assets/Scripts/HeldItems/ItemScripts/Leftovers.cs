using UnityEngine;

public class Leftovers : HeldItemBase
{
    private float tickCooldown = 1f;

    public override void Initialize(PlayerManager controller)
    {
        base.Initialize(controller);
        Name = "Leftovers";
    }

    public override void Update()
    {
        if (!playerManager.Pokemon.IsOutOfCombat || playerManager.PlayerState != PlayerState.Alive || playerManager.Pokemon.IsHPFull())
            return;

        if (tickCooldown > 0)
        {
            tickCooldown -= Time.deltaTime;
        }
        else
        {
            playerManager.Pokemon.HealDamage(Mathf.FloorToInt(playerManager.Pokemon.GetMaxHp() * 0.04f));
            tickCooldown = 1f;
        }
    }

    public override void Reset()
    {
        tickCooldown = 1f;
    }
}

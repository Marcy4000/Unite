using UnityEngine;

public class ShellBell : HeldItemBase
{
    private float cooldown = 0f;

    public override void Initialize(PlayerManager controller)
    {
        base.Initialize(controller);
        playerManager.Pokemon.OnDamageDealt += OnDamageDealt;
    }

    private void OnDamageDealt(ulong targetID, DamageInfo damage)
    {
        if (cooldown > 0f)
        {
            return;
        }

        if (!damage.proprieties.HasFlag(DamageProprieties.IsBasicAttack) && !damage.proprieties.HasFlag(DamageProprieties.IsMuscleBand))
        {
            int healAmount = 75 + Mathf.FloorToInt(playerManager.Pokemon.GetSpAttack() * 0.45f);

            playerManager.Pokemon.HealDamage(healAmount);

            cooldown = 10f;
        }
    }

    public override void Update()
    {
        if (cooldown > 0f)
        {
            cooldown -= Time.deltaTime;
        }
    }

    public override void Reset()
    {
        // Nothing
    }
}

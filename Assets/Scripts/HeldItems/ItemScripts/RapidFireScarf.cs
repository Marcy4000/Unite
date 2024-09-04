using UnityEngine;

public class RapidFireScarf : HeldItemBase
{
    private float stackCooldown = 0f;
    private float cooldown = 0f;

    private int stacks;

    private StatChange atkSpdBuff = new StatChange(30, Stat.AtkSpeed, 5f, true, true, true, 0);

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

        if (damage.proprieties.HasFlag(DamageProprieties.IsBasicAttack))
        {
            AddStack();

            if (stacks >= 3)
            {
                playerManager.Pokemon.AddStatChange(atkSpdBuff);
                cooldown = 10f;
            }
        }
    }

    public override void Update()
    {
        if (cooldown > 0f)
        {
            cooldown -= Time.deltaTime;
        }

        if (stackCooldown > 0f && stacks > 0)
        {
            stackCooldown -= Time.deltaTime;
        }

        if (stackCooldown <= 0f && stacks > 0)
        {
            stacks = 0;
        }
    }

    private void AddStack()
    {
        stacks++;
        stackCooldown = 3f;
    }

    public override void Reset()
    {
        // Nothing
    }
}

using UnityEngine;
using Unity.Netcode;

public class DrainCrown : HeldItemBase
{
    public override void Initialize(PlayerManager controller)
    {
        base.Initialize(controller);
        playerManager.Pokemon.OnDamageDealt += OnDamageDealt;
    }

    private void OnDamageDealt(ulong targetID, DamageInfo damage)
    {
        if (damage.type != DamageType.Physical)
        {
            return;
        }

        if (damage.proprieties.HasFlag(DamageProprieties.IsBasicAttack) && !damage.proprieties.HasFlag(DamageProprieties.IsMuscleBand))
        {
            Pokemon target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].GetComponent<Pokemon>();
            int lifesteal = Mathf.FloorToInt(target.CalculateDamage(damage, playerManager.Pokemon) * 0.13f);

            if (playerManager.Pokemon.IsHPFull())
            {
                return;
            }

            playerManager.Pokemon.HealDamageRPC(lifesteal);
        }
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

using UnityEngine;
using Unity.Netcode;

public class ScopeLens : HeldItemBase
{
    private float cooldown;

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

        if (damage.proprieties.HasFlag(DamageProprieties.IsBasicAttack) && damage.proprieties.HasFlag(DamageProprieties.WasCriticalHit))
        {
            Pokemon target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].GetComponent<Pokemon>();
            int additionalDamage = Mathf.FloorToInt(playerManager.Pokemon.GetAttack() * 0.75f);

            target.TakeDamageRPC(new DamageInfo(playerManager.NetworkObjectId, 0f, 0, (short)additionalDamage, DamageType.Physical, DamageProprieties.IsMuscleBand));
            cooldown = 1f;
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

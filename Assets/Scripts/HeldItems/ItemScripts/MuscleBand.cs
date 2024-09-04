using UnityEngine;
using Unity.Netcode;

public class MuscleBand : HeldItemBase
{
    public override void Initialize(PlayerManager controller)
    {
        base.Initialize(controller);
        playerManager.Pokemon.OnDamageDealt += OnDamageDealt;
    }

    private void OnDamageDealt(ulong targetID, DamageInfo damage)
    {
        if (damage.proprieties.HasFlag(DamageProprieties.IsBasicAttack) && !damage.proprieties.HasFlag(DamageProprieties.IsMuscleBand))
        {
            Pokemon target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].GetComponent<Pokemon>();
            int additionalDamage = Mathf.FloorToInt(target.GetMaxHp() * 0.03f);
            additionalDamage = Mathf.Clamp(additionalDamage, 0, 360);

            target.TakeDamage(new DamageInfo(playerManager.NetworkObjectId, 0f, 0, (short)additionalDamage, DamageType.Physical, DamageProprieties.IsMuscleBand));
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

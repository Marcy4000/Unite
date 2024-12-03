using UnityEngine;
using Unity.Netcode;

public class ChoiceSpecs : HeldItemBase
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
            Pokemon target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].GetComponent<Pokemon>();
            int additionalDamage = 60 + Mathf.FloorToInt(playerManager.Pokemon.GetSpAttack() * 0.4f);

            target.TakeDamageRPC(new DamageInfo(playerManager.NetworkObjectId, 0f, 0, (short)additionalDamage, DamageType.Special, DamageProprieties.IsMuscleBand));

            cooldown = 8f;
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

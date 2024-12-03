using Unity.Netcode;
using UnityEngine;

public class AquaRingObject : NetworkBehaviour
{
    // TODO: After reworking shields, aqua ring should be canceled when the shield breaks

    private ulong targetId;
    private PlayerManager target;

    private DamageInfo healAmount;

    private float healCooldown = 0.7f;
    private float ringDuration = 6f;

    private bool initialized;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(ulong targetId, DamageInfo heal)
    {
        this.targetId = targetId;
        healAmount = heal;
        target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetId].GetComponent<PlayerManager>();
        target.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(target.Pokemon.GetMaxHp()*0.20f), 2, 1, 6f, true));
        target.Pokemon.OnHpOrShieldChange += CheckIfShouldBreakEarly;
        target.Pokemon.OnDeath += OnTargetDeath;
        initialized = true;
    }

    private void OnTargetDeath(DamageInfo info)
    {
        target.Pokemon.OnHpOrShieldChange -= CheckIfShouldBreakEarly;
        target.Pokemon.OnDeath -= OnTargetDeath;
        NetworkObject.Despawn(true);
    }

    private void CheckIfShouldBreakEarly()
    {
        if (!target.Pokemon.HasShieldWithID(2))
        {
            initialized = false;
            target.Pokemon.OnHpOrShieldChange -= CheckIfShouldBreakEarly;
            NetworkObject.Despawn(true);
        }
    }

    private void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        healCooldown -= Time.deltaTime;
        ringDuration -= Time.deltaTime;
        transform.position = target.transform.position;

        if (healCooldown <= 0)
        {
            target.Pokemon.HealDamageRPC(healAmount);

            healCooldown = 0.7f;
        }

        if (ringDuration <= 0)
        {
            target.Pokemon.OnHpOrShieldChange -= CheckIfShouldBreakEarly;
            NetworkObject.Despawn(true);
        }
    }
}

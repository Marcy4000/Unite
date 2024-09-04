using UnityEngine;
using Unity.Netcode;

public class RazorClaw : HeldItemBase
{
    private float damageCooldown;

    public override void Initialize(PlayerManager controller)
    {
        base.Initialize(controller);
        playerManager.Pokemon.OnDamageDealt += OnDamageDealt;
        playerManager.MovesController.onMovePerformed += OnMovePerformed;
    }

    private void OnDamageDealt(ulong targetID, DamageInfo damage)
    {
        if (damageCooldown <= 0f)
        {
            return;
        }

        if (damage.proprieties.HasFlag(DamageProprieties.IsBasicAttack))
        {
            Pokemon target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].GetComponent<Pokemon>();
            int additionalDamage = Mathf.FloorToInt(20 + (playerManager.Pokemon.GetAttack() * 0.5f));

            target.TakeDamage(new DamageInfo(playerManager.NetworkObjectId, 0f, 0, (short)additionalDamage, DamageType.Physical, DamageProprieties.IsMuscleBand));
            damageCooldown = 0f;
        }
    }

    private void OnMovePerformed(MoveBase move)
    {
        damageCooldown = 3f;
    }

    public override void Update()
    {
        if (damageCooldown > 0f)
        {
            damageCooldown -= Time.deltaTime;
        }
    }

    public override void Reset()
    {
        // Nothing
    }
}

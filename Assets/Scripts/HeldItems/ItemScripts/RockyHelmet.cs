using UnityEngine;

public class RockyHelmet : HeldItemBase
{
    private float cooldown = 0f;

    public override void Initialize(PlayerManager controller)
    {
        base.Initialize(controller);
        playerManager.Pokemon.OnDamageTaken += OnDamageTaken;
    }

    private void OnDamageTaken(DamageInfo damage)
    {
        if (cooldown > 0)
        {
            return;
        }

        int damageDealt = playerManager.Pokemon.CalculateDamage(damage);

        if (damageDealt > Mathf.FloorToInt(playerManager.Pokemon.GetMaxHp() * 0.1f))
        {
            DamageInfo recoilDamage = new DamageInfo(playerManager.NetworkObjectId, 1f, 0, (short)Mathf.RoundToInt(playerManager.Pokemon.GetMaxHp() * 0.04f), DamageType.True);
            Collider[] hitColliders = Physics.OverlapSphere(playerManager.transform.position, 4f);

            foreach (Collider hitCollider in hitColliders)
            {
                if (Aim.Instance.CanPokemonBeTargeted(hitCollider.gameObject, AimTarget.NonAlly, playerManager.CurrentTeam))
                {
                    Pokemon hitPlayer = hitCollider.GetComponent<Pokemon>();
                    hitPlayer.TakeDamage(recoilDamage);
                }
            }

            cooldown = 2f;
        }
    }

    public override void Update()
    {
        if (cooldown > 0)
        {
            cooldown -= Time.deltaTime;
        }
    }

    public override void Reset()
    {
        // Nothing
    }
}

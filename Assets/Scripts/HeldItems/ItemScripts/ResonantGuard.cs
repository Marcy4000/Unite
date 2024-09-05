using Unity.Netcode;
using UnityEngine;

public class ResonantGuard : HeldItemBase
{
    private float cooldown;

    public override void Initialize(PlayerManager controller)
    {
        base.Initialize(controller);
        playerManager.Pokemon.OnDamageDealt += OnDamageDealt;
    }

    private void OnDamageDealt(ulong attackedID, DamageInfo damage)
    {
        if (cooldown > 0f)
        {
            return;
        }

        Pokemon attackedPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[attackedID].GetComponent<Pokemon>();

        if (attackedPokemon.Type != PokemonType.Player)
        {
            return;
        }

        int shieldAmount = 100 + Mathf.FloorToInt(playerManager.Pokemon.GetMaxHp() * 0.06f);
        ShieldInfo shield = new ShieldInfo(shieldAmount, 0, 0, 3f, true);

        GameObject[] teammates = Aim.Instance.AimInCircleAtPosition(playerManager.transform.position, 10f, AimTarget.Ally, playerManager.OrangeTeam);

        if (teammates.Length > 0)
        {
            Pokemon[] teamPokemons = new Pokemon[teammates.Length];

            for (int i = 0; i < teammates.Length; i++)
            {
                teamPokemons[i] = teammates[i].GetComponent<Pokemon>();
            }

            Pokemon lowestHpTarget = teamPokemons[0];

            for (int i = 1; i < teamPokemons.Length; i++)
            {
                if (teamPokemons[i].CurrentHp < lowestHpTarget.CurrentHp)
                {
                    lowestHpTarget = teamPokemons[i];
                }
            }

            lowestHpTarget.AddShieldRPC(shield);
        }

        playerManager.Pokemon.AddShieldRPC(shield);

        cooldown = 10f;
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

using Unity.Netcode;
using UnityEngine;

public class FlareonPassive : PassiveBase
{
    private bool isEvolved = false;

    private StatChange eeveeStatBoost = new StatChange(10, Stat.Attack, 5, true, true, true, 22);

    private StatChange flareAtkStack = new StatChange(5, Stat.Attack, 0, false, true, true, 21);
    private int stackCount = 0;
    private float stackTimer = 0;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        playerManager.Pokemon.OnEvolution += OnEvolution;
        playerManager.Pokemon.OnDamageDealt += OnDamageDealt;
        playerManager.Pokemon.OnHpOrShieldChange += OnHpOrShieldChange;
    }

    private void OnEvolution()
    {
        isEvolved = true;
        playerManager.Pokemon.RemoveStatChangeWithIDRPC(22);
        playerManager.Pokemon.OnHpOrShieldChange -= OnHpOrShieldChange;
    }

    private void OnDamageDealt(ulong attackedId, DamageInfo damageInfo)
    {
        Pokemon attackedPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[attackedId].GetComponent<Pokemon>();

        if (isEvolved && attackedPokemon.Type != PokemonType.Wild)
        {
            if (!attackedPokemon.HasStatusEffect(StatusType.Burned))
            {
                return;
            }

            AddAttackStack();
        }
    }

    private void OnHpOrShieldChange()
    {
        if (playerManager.Pokemon.CurrentHp < Mathf.FloorToInt(playerManager.Pokemon.GetMaxHp() * 0.5f))
        {
            if (playerManager.Pokemon.StatChanges.Contains(eeveeStatBoost))
            {
                return;
            }

            playerManager.Pokemon.AddStatChange(eeveeStatBoost);
        }
    }

    private void AddAttackStack()
    {
        stackTimer = 5;

        if (stackCount >= 3)
        {
            return;
        }

        stackCount++;
        playerManager.Pokemon.AddStatChange(flareAtkStack);
    }

    private void ResetAttackStacks()
    {
        stackCount = 0;
        playerManager.Pokemon.RemoveAllStatChangeWithIDRPC(21);
    }

    public override void Update()
    {
        if (!isEvolved)
        {
            return;
        }

        if (stackTimer > 0)
        {
            stackTimer -= Time.deltaTime;
            if (stackTimer <= 0)
            {
                ResetAttackStacks();
            }
        }
    }
}

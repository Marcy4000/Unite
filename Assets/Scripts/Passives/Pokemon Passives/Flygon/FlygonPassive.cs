using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class FlygonPassive : PassiveBase
{
    private bool isEvolved = false;
    private float damageCd = 3f;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        playerManager.Pokemon.OnEvolution += OnEvolution;
        playerManager.Pokemon.OnDamageDealt += OnDamageDealt;
        playerManager.Pokemon.OnStatusChange += OnStatusChange;
    }

    private void OnEvolution()
    {
        isEvolved = true;
    }

    private void OnDamageDealt(ulong attackedId, DamageInfo damage)
    {
        if (!isEvolved)
        {
            Pokemon attackedPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[attackedId].GetComponent<Pokemon>();
            if (Vector3.Distance(playerManager.transform.position, attackedPokemon.transform.position) > 4.5f || damageCd > 0)
            {
                return;
            }

            damageCd = 3f;
            playerManager.StartCoroutine(ExtraDamage(attackedPokemon, attackedPokemon.CalculateDamage(damage, playerManager.Pokemon)));
        }
    }

    private IEnumerator ExtraDamage(Pokemon attackedPokemon, int damage)
    {
        yield return new WaitForSeconds(0.5f);

        attackedPokemon.TakeDamageRPC(new DamageInfo(playerManager.NetworkObjectId, 0f, 0, (short)Mathf.FloorToInt(damage * 0.05f), DamageType.True));
    }

    private void OnStatusChange(StatusEffect statusEffect, bool added)
    {
        if (!isEvolved)
        {
            return;
        }

        if (statusEffect.Type == StatusType.Immobilized || statusEffect.Type == StatusType.Asleep || statusEffect.Type == StatusType.Incapacitated || statusEffect.Type == StatusType.VisionObscuring || statusEffect.Type == StatusType.Frozen)
        {
            if (added && statusEffect.IsTimed)
            {
                playerManager.Pokemon.UpdateStatusEffectTimeRPC(statusEffect, statusEffect.Duration - (statusEffect.Duration * 0.30f));
            }
        }
    }

    public override void Update()
    {
        if (damageCd > 0)
        {
            damageCd -= Time.deltaTime;
        }

        if (!isEvolved)
        {
            return;
        }
    }
}

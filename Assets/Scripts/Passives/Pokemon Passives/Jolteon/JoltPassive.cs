using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class JoltPassive : PassiveBase
{
    private bool isEvolved = false;
    private StatChange eeveeSpdBoost = new StatChange(10, Stat.Speed, 0, false, true, true, 9);

    private float passiveCharge;
    public bool IsPassiveReady => passiveCharge >= 100f;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        playerManager.Pokemon.OnEvolution += OnEvolution;
        playerManager.Pokemon.AddStatChange(eeveeSpdBoost);
        playerManager.Pokemon.onDamageDealt += (target) =>
        {
            Pokemon targetPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[target].GetComponent<Pokemon>();
            if (targetPokemon.Type == PokemonType.Player)
            {
                passiveCharge += 5f;
            }
        };
        playerManager.Pokemon.onOtherPokemonKilled += (target) =>
        {
            Pokemon targetPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[target].GetComponent<Pokemon>();
            if (targetPokemon.Type == PokemonType.Player)
            {
                passiveCharge += 15f;
            }
        };
    }

    private void OnEvolution()
    {
        isEvolved = true;
        playerManager.Pokemon.RemoveStatChangeWithID(eeveeSpdBoost.ID);
        playerManager.HPBar.ShowGenericGuage(true);
        playerManager.HPBar.UpdateGenericGuageValue(passiveCharge, 100f);
    }

    public override void Update()
    {
        if (!isEvolved)
        {
            return;
        }

        passiveCharge += Time.deltaTime;

        if (playerManager.PlayerMovement.IsMoving)
        {
            passiveCharge += 2f * Time.deltaTime;
        }

        if (passiveCharge > 100f)
        {
            passiveCharge = 100f;
        }

        if (Mathf.Abs((passiveCharge / 100f) - playerManager.AnimationManager.Animator.GetFloat("PassiveAmount")) > 0.05f)
        {
            playerManager.AnimationManager.SetFloat("PassiveAmount", passiveCharge / 100f);
        }

        playerManager.HPBar.UpdateGenericGuageValue(passiveCharge, 100f);
    }

    public void ResetPassive()
    {
        passiveCharge = 0f;
    }
}

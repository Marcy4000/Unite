using Unity.Netcode;
using UnityEngine;

public class JoltPassive : PassiveBase
{
    private bool isEvolved = false;
    private StatChange eeveeSpdBoost = new StatChange(10, Stat.Speed, 0, false, true, true, 9);

    private float passiveCharge;
    public bool IsPassiveReady => passiveCharge >= 100f;

    private bool isBoostedByUnite = false;
    private float boostDuration = 5f;

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
                passiveCharge += 8f;
            }
            else
            {
                passiveCharge += 4f;
            }
        };
        playerManager.Pokemon.onOtherPokemonKilled += (target) =>
        {
            Pokemon targetPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[target].GetComponent<Pokemon>();
            if (targetPokemon.Type == PokemonType.Player)
            {
                passiveCharge += 20f;
            }

            if (targetPokemon.HasStatusEffect(8))
            {
                playerManager.Pokemon.AddStatChange(new StatChange(30, Stat.Speed, 1f, true, true, true, 0));
            }
        };

        playerManager.Pokemon.OnDeath += (damage) =>
        {
            ResetPassiveCharge();
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
        if (!isEvolved || playerManager.PlayerState == PlayerState.Dead)
        {
            return;
        }

        if (isBoostedByUnite)
        {
            boostDuration -= Time.deltaTime;
            if (boostDuration <= 0)
            {
                isBoostedByUnite = false;
            }
        }

        passiveCharge += 2f * Time.deltaTime;

        if (playerManager.PlayerMovement.IsMoving)
        {
            passiveCharge += 4f * Time.deltaTime;
        }

        if (passiveCharge > 100f)
        {
            passiveCharge = 100f;
        }

        if (Mathf.Abs((passiveCharge / 100f) - playerManager.AnimationManager.Animator.GetFloat("PassiveAmount")) > 0.03f)
        {
            playerManager.AnimationManager.SetFloat("PassiveAmount", passiveCharge / 100f);
        }

        playerManager.HPBar.UpdateGenericGuageValue(passiveCharge, 100f);
    }

    public void ResetPassiveCharge()
    {
        if (isBoostedByUnite)
        {
            return;
        }
        passiveCharge = 0f;
    }

    public void ReducePassiveCharge(float amount)
    {
        if (isBoostedByUnite)
        {
            return;
        }

        passiveCharge -= amount;
        if (passiveCharge < 0f)
        {
            passiveCharge = 0f;
        }
    }

    public void UniteFillCharge(float duration)
    {
        isBoostedByUnite = true;
        passiveCharge = 100f;
        boostDuration = duration;
    }
}

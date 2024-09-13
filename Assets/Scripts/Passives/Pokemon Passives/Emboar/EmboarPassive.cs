using Unity.Netcode;
using UnityEngine;

public class EmboarPassive : PassiveBase
{
    private bool isEvolved = false;

    private StatChange defBuff = new StatChange(20, Stat.Defense, 0f, false, true, true, 18);

    private const float RECKLESS_ACTIVE_TIME = 15f;
    private const float RECKLESS_COOLDOWN = 5f;

    private bool isRecklessActive = false;
    private bool isRecklessOnCooldown = false;

    private int recklessCharge = 0;
    private float cooldown = 0f;

    private float recklessCooldown = 0f;

    public bool IsRecklessActive => isRecklessActive;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);

        playerManager.Pokemon.OnEvolution += OnEvolution;

        playerManager.Pokemon.AddStatChange(defBuff);
    }

    private void OnEvolution()
    {
        if (playerManager.Pokemon.CurrentLevel == 6)
        {
            isEvolved = true;
            playerManager.Pokemon.RemoveStatChangeWithIDRPC(defBuff.ID);
            playerManager.Pokemon.OnDamageTaken += OnDamageTaken;
            playerManager.Pokemon.OnDamageDealt += OnDamageDealt;
            playerManager.MovesController.onMovePerformed += OnMovePerformed;

            playerManager.HPBar.ShowGenericGuage(true);
        }
    }

    private void OnDamageTaken(DamageInfo damageInfo)
    {
        if (isRecklessActive || isRecklessOnCooldown)
        {
            return;
        }

        recklessCharge += 5;

        recklessCharge = Mathf.Clamp(recklessCharge, 0, 100);

        if (recklessCharge >= 100)
        {
            isRecklessActive = true;
            cooldown = RECKLESS_ACTIVE_TIME;
        }
    }

    private void OnDamageDealt(ulong attackedID, DamageInfo damageInfo)
    {
        if (isRecklessActive || isRecklessOnCooldown)
        {
            return;
        }

        Pokemon attackedPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[attackedID].GetComponent<Pokemon>();

        if (attackedPokemon.Type == PokemonType.Player)
        {
            recklessCharge += 8;
        }
        else
        {
            recklessCharge += 2;
        }

        recklessCharge = Mathf.Clamp(recklessCharge, 0, 100);

        if (recklessCharge >= 100)
        {
            isRecklessActive = true;
            cooldown = RECKLESS_ACTIVE_TIME;
        }
    }

    private void OnMovePerformed(MoveBase move)
    {
        if (isRecklessActive)
        {
            cooldown = 0f;
        }
    }

    public void SetRecklessCharge(int value)
    {
        recklessCharge = Mathf.Clamp(value, 0, 100);

        if (recklessCharge >= 100)
        {
            isRecklessActive = true;
            cooldown = RECKLESS_ACTIVE_TIME;
        }
    }

    public override void Update()
    {
        if (!isEvolved)
        {
            return;
        }

        if (recklessCooldown > 0f)
        {
            recklessCooldown -= Time.deltaTime;
        }
        else if (isRecklessOnCooldown)
        {
            isRecklessOnCooldown = false;
            recklessCharge = 0;
        }

        if (cooldown > 0f)
        {
            cooldown -= Time.deltaTime;
        }
        else if (isRecklessActive)
        {
            isRecklessOnCooldown = true;
            recklessCooldown = 10f;
            isRecklessActive = false;
            recklessCharge = 0;
        }

        if (isRecklessActive)
        {
            playerManager.HPBar.UpdateGenericGuageValue(cooldown, RECKLESS_ACTIVE_TIME);
        }
        else if (isRecklessOnCooldown)
        {
            playerManager.HPBar.UpdateGenericGuageValue(recklessCooldown, RECKLESS_COOLDOWN);
        }
        else
        {
            playerManager.HPBar.UpdateGenericGuageValue(recklessCharge, 100);
        }
    }
}

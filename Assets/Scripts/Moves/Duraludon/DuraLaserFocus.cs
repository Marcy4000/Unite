using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class DuraLaserFocus : MoveBase
{
    private StatChange dmgReduction = new StatChange(20, Stat.DamageReduction, 0.6f, true, true, false, 0);

    private bool initialized = false;
    private bool effectApplied = false;
    private byte autoAttacksCounter = 0;

    public DuraLaserFocus()
    {
        Name = "Laser Focus";
        Cooldown = 9f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        if (!initialized)
            playerManager.Pokemon.OnDamageDealt += OnDamageDealt;

        initialized = true;
    }

    private void OnDamageDealt(ulong targetID, DamageInfo damage)
    {
        if (!initialized || !effectApplied)
            return;

        if ((damage.proprieties & DamageProprieties.IsBasicAttack) != 0 && (damage.proprieties & DamageProprieties.IsMuscleBand) == 0)
        {
            autoAttacksCounter++;
            if (autoAttacksCounter > 3)
            {
                effectApplied = false;
                autoAttacksCounter = 0;

                return;
            }

            Pokemon target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].GetComponent<Pokemon>();
            target.TakeDamageRPC(new DamageInfo(playerManager.NetworkObjectId, 0.27f, 3, 60, DamageType.Physical, DamageProprieties.None));
        }
    }

    public override void Update()
    {
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.StartCoroutine(ExecuteMove());
            wasMoveSuccessful = true;
        }
        base.Finish();
    }

    private IEnumerator ExecuteMove()
    {
        playerManager.Pokemon.AddStatChange(dmgReduction);
        playerManager.AnimationManager.PlayAnimation("ani_spell1_bat_0884");
        yield return new WaitForSeconds(0.6f);
        effectApplied = true;
        float timer = 0f;

        while (timer < 8f)
        {
            timer += Time.deltaTime;

            if (!effectApplied)
                yield break;

            yield return null;
        }

        effectApplied = false;
    }

    public override void ResetMove()
    {
        effectApplied = false;
        autoAttacksCounter = 0;
    }
}

using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MeowsticPassiveM : PassiveBase
{
    private bool isEvolved = false;
    private bool canBlockFirstEffect = true;
    private bool isReducingFurtherEffects = false;

    private float cooldown = 0.6f;

    private Collider[] playersInArea = new Collider[10];
    private LayerMask playerLayer;

    private int lastEnemiesInArea = 0;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        playerManager.Pokemon.OnEvolution += OnEvolution;
        playerManager.Pokemon.OnStatChange += OnStatChange;
        playerLayer = LayerMask.GetMask("Players");
    }

    private void OnStatChange(NetworkListEvent<StatChange> changeEvent)
    {
        if (changeEvent.Type != NetworkListEvent<StatChange>.EventType.Add || cooldown > 0f)
            return;

        if (canBlockFirstEffect)
        {
            if (!changeEvent.Value.IsBuff)
            {
                cooldown = 0.6f;
                playerManager.Pokemon.RemoveStatChangeRPC(changeEvent.Value);
                canBlockFirstEffect = false;
                isReducingFurtherEffects = true;
                playerManager.StartCoroutine(EspurrCooldown());
            }
        }
        else if (isReducingFurtherEffects)
        {
            cooldown = 0.6f;

            playerManager.Pokemon.RemoveStatChangeRPC(changeEvent.Value);
            StatChange newChange = changeEvent.Value;
            newChange.Amount -= (short)(newChange.Amount * 0.6f);
            playerManager.Pokemon.AddStatChange(newChange);
        }
    }

    private IEnumerator EspurrCooldown()
    {
        yield return new WaitForSeconds(3f);
        isReducingFurtherEffects = false;

        yield return new WaitForSeconds(20f);
        canBlockFirstEffect = true;
    }

    private void OnEvolution()
    {
        isEvolved = true;
        playerManager.Pokemon.OnStatChange -= OnStatChange;
    }

    public override void Update()
    {
        if (cooldown > 0)
        {
            cooldown -= Time.deltaTime;
        }

        if (!isEvolved)
        {
            return;
        }

        if (cooldown <= 0)
        {
            int numPlayers = Physics.OverlapSphereNonAlloc(playerManager.transform.position, 7f, playersInArea, playerLayer);
            int enemiesInArea = 0;
            for (int i = 0; i < numPlayers; i++)
            {
                PlayerManager player = playersInArea[i].GetComponent<PlayerManager>();
                if (player.OrangeTeam == playerManager.OrangeTeam)
                {
                    continue;
                }

                enemiesInArea++;
            }

            if (enemiesInArea != lastEnemiesInArea)
            {
                lastEnemiesInArea = enemiesInArea;
                playerManager.Pokemon.RemoveStatChangeWithID(13);
                if (enemiesInArea > 0)
                {
                    playerManager.Pokemon.AddStatChange(new StatChange((short)(enemiesInArea * 5), Stat.Cdr, 0, false, true, true, 13));
                }
            }

            cooldown = 1f;
        }
    }
}

using UnityEngine;

public class CresseliaPassive : PassiveBase
{
    private float cooldown = 0.6f;

    private Collider[] playersInArea = new Collider[10];
    private LayerMask playerLayer;

    private int lastAlliesInArea = 0;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        playerLayer = LayerMask.GetMask("Players");
    }

    public override void Update()
    {
        if (cooldown > 0)
        {
            cooldown -= Time.deltaTime;
        }
        else
        {
            int numPlayers = Physics.OverlapSphereNonAlloc(playerManager.transform.position, 6f, playersInArea, playerLayer);
            int alliesInArea = 0;
            for (int i = 0; i < numPlayers; i++)
            {
                PlayerManager player = playersInArea[i].GetComponent<PlayerManager>();
                if (player.OrangeTeam != playerManager.OrangeTeam || player == playerManager)
                {
                    continue;
                }

                alliesInArea++;
            }

            if (alliesInArea != lastAlliesInArea)
            {
                lastAlliesInArea = alliesInArea;
                playerManager.Pokemon.RemoveStatChangeWithIDRPC(13);
                if (alliesInArea > 0)
                {
                    playerManager.Pokemon.AddStatChange(new StatChange(20, Stat.Speed, 0, false, true, true, 13));
                }
            }

            cooldown = 0.6f;
        }
    }
}

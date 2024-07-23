using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarshadowUnite : MoveBase
{
    private MarshadwPassive passive;

    public MarshadowUnite()
    {
        Name = "Mirror Shadow";
        Cooldown = 0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        passive = playerManager.PassiveController.Passive as MarshadwPassive;
    }

    public override void Update()
    {
        
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.StartCoroutine(LearnNewUnite());
        }
        base.Finish();
    }

    private IEnumerator LearnNewUnite()
    {
        playerManager.AnimationManager.PlayAnimation("pm0883_ba21_tokusyu02");
        playerManager.PlayerMovement.CanMove = false;
        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        yield return new WaitForSeconds(0.9f);

        PlayerManager randomPokemon = GetRandonEnemy();

        passive.LearnNewTempUnite(randomPokemon.Pokemon.BaseStats.GetUniteMove());

        yield return new WaitForSeconds(1.1f);
        
        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        playerManager.PlayerMovement.CanMove = true;
    }

    private PlayerManager GetRandonEnemy()
    {
        PlayerManager randomPokemon;

        List<PlayerManager> enemies = new List<PlayerManager>();

        GameManager.Instance.Players.ForEach(player =>
        {
            if (player.OrangeTeam != playerManager.OrangeTeam)
            {
                enemies.Add(player);
            }
        });

        bool found = false;

        do
        {
            randomPokemon = GameManager.Instance.Players[Random.Range(0, GameManager.Instance.Players.Count)];
            if (GameManager.Instance.Players.Count < 2)
            {
                break;
            }

            if (!enemies.Contains(randomPokemon) && enemies.Count > 0)
            {
                continue;
            }

            found = true;
        } while (!found);

        return randomPokemon;
    }
}

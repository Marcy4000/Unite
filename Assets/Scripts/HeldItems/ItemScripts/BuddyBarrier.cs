using UnityEngine;

public class BuddyBarrier : HeldItemBase
{
    private float cooldown;

    public override void Initialize(PlayerManager controller)
    {
        base.Initialize(controller);
        playerManager.MovesController.onMovePerformed += OnMovePerformed;
    }

    private void OnMovePerformed(MoveBase move)
    {
        if (cooldown > 0f)
        {
            return;
        }

        if (move == playerManager.MovesController.GetMove(MoveType.UniteMove))
        {
            ShieldInfo shield = new ShieldInfo(Mathf.FloorToInt(playerManager.Pokemon.GetMaxHp() * 0.25f), 6, 0, 5f, true);

            GameObject[] teammates = Aim.Instance.AimInCircleAtPosition(playerManager.transform.position, 10f, AimTarget.Ally, playerManager.CurrentTeam);

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

            cooldown = 30f;
        }
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

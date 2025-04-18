using UnityEngine;

public class MarshadwPassive : PassiveBase
{
    // This ability is raises your Phy.Atk, basic attack speed, and life steal every time the user uses a move, stacking up to 3 times.

    private int buffStacks = 0;

    private float buffTimer = 3;

    private StatChange atkBuff = new StatChange(5, Stat.Attack, 0f, false, true, true, 14);
    private StatChange atkSpeedBuff = new StatChange(5, Stat.AtkSpeed, 0f, false, true, true, 15);
    private StatChange lifeStealBuff = new StatChange(5, Stat.LifeSteal, 0f, false, true, true, 16);

    private MoveAsset normalUnite;

    private MoveBase tempUniteMove;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        controller.MovesController.onMovePerformed += OnMovePerformed;
        normalUnite = playerManager.Pokemon.BaseStats.GetUniteMove();
    }

    private void OnMovePerformed(MoveBase move)
    {
        if (buffStacks < 3)
        {
            buffStacks++;
            playerManager.Pokemon.AddStatChange(atkBuff);
            playerManager.Pokemon.AddStatChange(atkSpeedBuff);
            playerManager.Pokemon.AddStatChange(lifeStealBuff);
        }

        if (move == tempUniteMove && tempUniteMove != null)
        {
            playerManager.MovesController.LearnMove(normalUnite);
            tempUniteMove = null;
        }
    }

    public override void Update()
    {
        if (buffStacks > 0)
        {
            buffTimer -= Time.deltaTime;
            if (buffTimer <= 0)
            {
                buffStacks = 0;
                playerManager.Pokemon.RemoveAllStatChangeWithIDRPC(atkBuff.ID);
                playerManager.Pokemon.RemoveAllStatChangeWithIDRPC(atkSpeedBuff.ID);
                playerManager.Pokemon.RemoveAllStatChangeWithIDRPC(lifeStealBuff.ID);
                
                buffTimer = 3;
            }
        }
    }

    public void LearnNewTempUnite(MoveAsset newUnite)
    {
        if (newUnite == null)
        {
            return;
        }

        playerManager.MovesController.LearnMove(newUnite);
        tempUniteMove = playerManager.MovesController.GetMove(MoveType.UniteMove);
        playerManager.MovesController.IncrementUniteCharge(newUnite.uniteEnergyCost);
    }
}

using UnityEngine;

public class EnergyAmp : HeldItemBase
{
    private float cooldown;

    private StatChange atkBoost = new StatChange(21, Stat.Attack, 4f, true, true, true, 0);
    private StatChange spAtkBoost = new StatChange(21, Stat.SpAttack, 4f, true, true, true, 0);

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
            playerManager.Pokemon.AddStatChange(atkBoost);
            playerManager.Pokemon.AddStatChange(spAtkBoost);
            cooldown = 15f;
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

using System.Collections.Generic;

public class XSpeed : BattleItemBase
{
    private StatChange speedBoost = new StatChange(45, Stat.Speed, 6f, true, true, true, 0);

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        wasUseSuccessful = false;

        Cooldown = 40f;
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }
    }

    public override void Finish()
    {
        if (IsActive)
        {
            wasUseSuccessful = true;
            playerManager.Pokemon.AddStatChange(speedBoost);
            List<StatChange> statsToRemove = new List<StatChange>();
            foreach (var stat in playerManager.Pokemon.StatChanges)
            {
                if (stat.AffectedStat == Stat.Speed && stat.IsTimed && !stat.IsBuff)
                {
                    statsToRemove.Add(stat);
                }
            }

            foreach (var stat in statsToRemove)
            {
                playerManager.Pokemon.RemoveStatChangeRPC(stat);
            }
        }
        base.Finish();
    }
}

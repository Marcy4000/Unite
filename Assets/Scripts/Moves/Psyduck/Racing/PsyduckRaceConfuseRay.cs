public class PsyduckRaceConfuseRay : MoveBase
{
    public PsyduckRaceConfuseRay() { Name = "Confuse Ray"; Cooldown = 30f; }
    public override void Update() { }
    public override void Finish()
    {
        if (!IsActive) return;
        playerManager.MovesController.SpawnPsyduckRaceItemRPC(AvailableMoves.PsyduckRaceConfuseRay);
        base.Finish();
        playerManager.MovesController.LearnMove(RaceManager.Instance.EmptyMove);
    }
    public override void ResetMove() { }
}

public class PsyduckRaceFreezeRay : MoveBase
{
    public PsyduckRaceFreezeRay() { Name = "Freeze Ray"; Cooldown = 30f; }
    public override void Update() { }
    public override void Finish()
    {
        if (!IsActive) return;
        playerManager.MovesController.SpawnPsyduckRaceItemRPC(AvailableMoves.PsyduckRaceFreezeRay);
        base.Finish();
        playerManager.MovesController.LearnMove(RaceManager.Instance.EmptyMove);
    }
    public override void ResetMove() { }
}

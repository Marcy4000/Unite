public class PsyduckRaceBarrier : MoveBase
{
    public PsyduckRaceBarrier() { Name = "Barrier"; Cooldown = 30f; }
    public override void Update() { }
    public override void Finish()
    {
        if (!IsActive) return;
        playerManager.MovesController.SpawnPsyduckRaceItemRPC(AvailableMoves.PsyduckRaceBarrier);
        base.Finish();
        playerManager.MovesController.LearnMove(RaceManager.Instance.EmptyMove);
    }
    public override void ResetMove() { }
}

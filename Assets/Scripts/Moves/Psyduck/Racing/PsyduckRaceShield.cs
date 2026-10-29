public class PsyduckRaceShield : MoveBase
{
    public PsyduckRaceShield() { Name = "Shield"; Cooldown = 30f; }
    public override void Update() { }
    public override void Finish()
    {
        if (!IsActive) return;
        playerManager.MovesController.SpawnPsyduckRaceItemRPC(AvailableMoves.PsyduckRaceShield);
        base.Finish();
        playerManager.MovesController.LearnMove(RaceManager.Instance.EmptyMove);
    }
    public override void ResetMove() { }
}

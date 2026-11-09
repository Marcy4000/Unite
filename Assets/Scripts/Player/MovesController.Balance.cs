using Unite.Updates;

public partial class MovesController
{
    private float PatchedMoveCooldown(AvailableMoves id, float fallback)
    {
        if (pokemon == null || pokemon.BaseStats == null) return fallback;
        MoveAsset learned = null;
        bool upgraded = false;
        foreach (var row in pokemon.BaseStats.LearnableMoves)
        {
            if (row.level > pokemon.CurrentLevel) continue;
            foreach (var asset in row.moves)
            {
                if (asset == null || asset.move != id) continue;
                learned = asset;
                upgraded |= row.isUpgraded;
            }
        }
        var patch = learned != null ? BalanceRuntime.Move(learned.BalanceId) : null;
        return patch == null || !patch.overrideCooldown ? fallback : upgraded ? patch.upgradedCooldown : patch.cooldown;
    }
}

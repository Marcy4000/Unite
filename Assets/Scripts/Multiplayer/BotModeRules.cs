namespace Unite.Multiplayer
{
    public enum BotGameMode { Unsupported, Lanes, Volleyball, Racing, Royale }

    public static class BotModeRules
    {
        public static bool NeedsBackup(bool alive, bool canMove, bool incoming, float distance, float radius, float remaining) =>
            !alive || (incoming && distance > radius && (!canMove || distance > System.Math.Max(0, remaining - .5f) * 6));
        public static BotGameMode ForScene(string scene) => scene switch
        {
            "RemoatStadium" or "TheiaSkyRuins" => BotGameMode.Lanes,
            "ElectrodeVolleyball" => BotGameMode.Volleyball,
            "PsyduckRacing" => BotGameMode.Racing,
            "UniteRoyale" => BotGameMode.Royale,
            _ => BotGameMode.Unsupported
        };
        public static bool Supports(string scene, int teams, int slots) => ForScene(scene) switch
        {
            BotGameMode.Lanes => teams == 2 && slots == 5,
            BotGameMode.Volleyball => teams == 2 && slots == 3,
            BotGameMode.Racing => teams == 1 && slots == 6,
            BotGameMode.Royale => teams == 4 && slots == 1,
            _ => false
        };
    }
}

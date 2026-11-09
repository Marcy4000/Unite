namespace Unite.Multiplayer
{
    public enum BotGameMode { Unsupported, Lanes, Volleyball, Racing, Royale }

    public static class BotModeRules
    {
        public static bool NeedsBackup(bool alive, bool canMove, bool incoming, float distance, float radius, float remaining) =>
            !alive || (incoming && distance > radius && (!canMove || distance > System.Math.Max(0, remaining - .5f) * 6));
    }
}

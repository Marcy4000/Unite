namespace Unite.Multiplayer
{
    public static class SpectatorSlotRules
    {
        public const int Capacity = 5;
        public const string TeamValue = "Spectator";

        public static bool IsValidSlot(int position) => position >= 0 && position < Capacity;
        public static int SessionCapacity(int combatantCapacity, bool custom) =>
            combatantCapacity + (custom ? Capacity : 0);
    }
}

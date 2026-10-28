using System;

namespace Unite.Multiplayer
{
    // Blue opens with one pick, followed by alternating pairs. The final pair
    // is clamped to the remaining slots: 5v5 is 1-2-2-2-2-1.
    public static class DraftTurnRules
    {
        public static float BotDelaySeconds(bool banning, bool humansStillChoosing, float variation)
        {
            float jitter = Math.Clamp(variation, -1f, 1f);
            if (banning) return humansStillChoosing ? 7f + jitter : 4f + jitter;
            return humansStillChoosing ? 7.5f + jitter * 1.5f : 4f + jitter;
        }

        public static bool CanAutoPick(bool pendingHumanChoice, float now, float actAt) => !pendingHumanChoice && now >= actAt;

        public static bool IsBlueTurn(int turn) => turn % 2 == 0;
        public static int PickStart(int turn) => turn == 0 ? 0 : turn - 1;
        public static int PickCount(int teamSize, int turn) => Math.Max(0, Math.Min(turn == 0 ? 1 : 2, teamSize - PickStart(turn)));
        public static int PickTurns(int blueSize, int orangeSize)
        {
            int lastBlue = blueSize > 0 ? 2 * (blueSize / 2) : -1;
            int lastOrange = orangeSize > 0 ? 2 * ((orangeSize - 1) / 2) + 1 : -1;
            return Math.Max(lastBlue, lastOrange) + 1;
        }
    }
}

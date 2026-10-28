using System;
using System.Collections.Generic;
using System.Linq;

namespace Unite.Multiplayer
{
    // Pure allocation rules: no service calls, client IDs, scene objects or Unity RNG.
    public static class BotAllocationRules
    {
        public static readonly string[] StandardLanes = { "top", "mid", "bottom" };
        private static readonly int[] StandardCapacity = { 2, 1, 2 };

        public static Dictionary<string, string> AllocateLanes(
            IEnumerable<string> humanLanes, IReadOnlyDictionary<string, string> botLanes)
        {
            var humans = humanLanes.ToArray();
            var ids = botLanes.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var counts = StandardLanes.Select(lane => humans.Count(value => value == lane)).ToArray();
            var chosen = new int[ids.Length];
            var best = new int[ids.Length];
            int bestCost = int.MaxValue;
            void Search(int index, int changes)
            {
                if (index == ids.Length)
                {
                    int imbalance = 0;
                    for (int lane = 0; lane < counts.Length; lane++)
                    {
                        int difference = counts[lane] - StandardCapacity[lane];
                        // An undeclared human should not make the only jungler
                        // disappear merely because top wins an otherwise equal tie.
                        imbalance += difference * difference * (lane == 1 ? 2 : 1);
                    }
                    int cost = imbalance * 100 + changes;
                    if (cost >= bestCost) return;
                    bestCost = cost;
                    Array.Copy(chosen, best, chosen.Length);
                    return;
                }
                for (int lane = 0; lane < StandardLanes.Length; lane++)
                {
                    chosen[index] = lane;
                    counts[lane]++;
                    Search(index + 1, changes + (botLanes[ids[index]] == StandardLanes[lane] ? 0 : 1));
                    counts[lane]--;
                }
            }
            if (ids.Length > 5) throw new ArgumentOutOfRangeException(nameof(botLanes));
            Search(0, 0);
            return ids.Select((id, index) => new { id, lane = StandardLanes[best[index]] })
                .ToDictionary(value => value.id, value => value.lane);
        }

        public static Dictionary<string, short> AllocateCharacters(IEnumerable<short> available,
            IEnumerable<short> humanChoices, IReadOnlyDictionary<string, short> preferences)
        {
            var remaining = available.Distinct().Where(id => id >= 0).OrderBy(id => id).ToList();
            var humans = new HashSet<short>(humanChoices);
            remaining.RemoveAll(humans.Contains);
            if (remaining.Count < preferences.Count)
                throw new InvalidOperationException("Not enough supported Pokemon remain to fill this team without duplicates.");
            var result = new Dictionary<string, short>();
            // Preserve uncontested private preferences before filling displaced bots.
            foreach (var pair in preferences.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                if (remaining.Remove(pair.Value)) result.Add(pair.Key, pair.Value);
            foreach (var id in preferences.Keys.OrderBy(id => id, StringComparer.Ordinal))
                if (!result.ContainsKey(id))
                {
                    result.Add(id, remaining[0]);
                    remaining.RemoveAt(0);
                }
            return result;
        }

        public static ulong MembershipHash(IEnumerable<string> humanIds)
        {
            ulong hash = 14695981039346656037UL;
            foreach (var id in humanIds.OrderBy(id => id, StringComparer.Ordinal))
            {
                foreach (char character in id) { hash ^= character; hash *= 1099511628211UL; }
                hash ^= 0xff; hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}

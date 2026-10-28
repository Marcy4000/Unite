using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Bots/Roster and behavior")]
public class BotRosterConfig : ScriptableObject
{
    public BotLoadout[] loadouts = new BotLoadout[0];
    public string[] trainerOutfits = new string[0];
    [Range(0, 1)] public float recoverBelow = 0.30f;
    [Range(0, 1)] public float resumeAbove = 0.58f;
    [Range(0, 1)] public float recallBelow = 0.14f;
    [Range(0, 1)] public float finalObjectiveAttendance = 0.97f;
    [Min(1)] public float taskCommitmentSeconds = 8;
    [Min(1)] public float safeRecallEnemyDistance = 13;
    private static BotRosterConfig instance;
    public static BotRosterConfig Instance => instance != null ? instance : instance = Resources.Load<BotRosterConfig>("Bots/Roster");

    public short[] CharacterPool()
    {
        var result = new List<short>();
        foreach (var build in loadouts)
        {
            if (build == null || !build.IsValid) continue;
            short id = CharactersList.Instance.GetCharacterID(build.pokemon);
            if (id >= 0 && !result.Contains(id)) result.Add(id);
        }
        return result.ToArray();
    }

    public short ChooseBuild(short character, System.Random random)
    {
        float total = 0;
        for (int i = 0; i < loadouts.Length; i++)
            if (Matches(i, character)) total += loadouts[i].weight;
        double roll = random.NextDouble() * total;
        for (short i = 0; i < loadouts.Length; i++)
            if (Matches(i, character) && (roll -= loadouts[i].weight) < 0) return i;
        return -1;
    }
    private bool Matches(int index, short character) => loadouts[index] != null && loadouts[index].IsValid &&
        CharactersList.Instance.GetCharacterID(loadouts[index].pokemon) == character;
    public BotLoadout GetBuild(int index) => index >= 0 && index < loadouts.Length ? loadouts[index] : null;
}

using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "MapInfo", menuName = "Create New Map")]
public class MapInfo : ScriptableObject
{
    [Header("Map Identity")]
    [Tooltip("Display name of the map.")]
    public string mapName;
    [Tooltip("Icon shown for the map in the lobby.")]
    public Sprite mapIcon;
    [Tooltip("Unity scene name associated with this map.")]
    public string sceneName;
    [Tooltip("Addressables key for the map scene.")]
    public string mapSceneKey;

    [Space]
    [Header("Teams & Results")]
    [Tooltip("Maximum number of players per team.")]
    public int maxTeamSize;

    [Tooltip("Result screen sprite for the blue team.")]
    public AssetReferenceSprite mapResultsBlue;
    [Tooltip("Result screen sprite for the orange team.")]
    public AssetReferenceSprite mapResultsOrange;

    [Space]
    [Header("Game Rules")]
    [Tooltip("Game mode for this map.")]
    public GameMode gameMode;
    [Tooltip("Total match duration in seconds.")]
    public float gameTime;
    [Tooltip("Final stretch phase duration in seconds.")]
    public float finalStretchTime;
    [Tooltip("Maximum score achievable.")]
    public ushort maxScore;

    [Space]
    [Header("Audio")]
    [Tooltip("Background music for normal gameplay.")]
    public DefaultAudioMusic normalMusic;
    [Tooltip("Background music for the final stretch phase.")]
    public DefaultAudioMusic finalStretchMusic;

    [Space]
    [Header("Selection & Restrictions")]
    [Tooltip("Character selection type.")]
    public CharacterSelectType characterSelectType;
    [Tooltip("Restrictions for starting the match.")]
    public StartRestriction startRestriction;

    [Space]
    [Header("Description")]
    [TextArea(3, 10)]
    [Tooltip("Description of the map shown in the lobby.")]
    public string description;

    [Space]
    [Header("Respawn Settings")]
    [Tooltip("Base respawn duration for each level (index = level, value = seconds). Used as the starting value in the respawn time calculation.")]
    public int[] respawnLevelDurations = new int[15] { 4, 4, 4, 4, 5, 6, 8, 9, 10, 11, 12, 15, 17, 19, 19 };
    [Tooltip("Kills since last death are divided by this value before being added to the respawn time.")]
    public float killsDivisor = 2f;
    [Tooltip("Minimum value that the kills contribution can add to the respawn time.")]
    public float killsClampMin = 0f;
    [Tooltip("Maximum value that the kills contribution can add to the respawn time.")]
    public float killsClampMax = 10f;
    [Tooltip("Points since last death are divided by this value before being added to the respawn time.")]
    public float pointsDivisor = 60f;
    [Tooltip("Minimum value that the points contribution can add to the respawn time.")]
    public float pointsClampMin = 0f;
    [Tooltip("Maximum value that the points contribution can add to the respawn time.")]
    public float pointsClampMax = 10f;
    [Tooltip("Minimum total respawn time allowed after all calculations.")]
    public int timeClampMin = 0;
    [Tooltip("Maximum total respawn time allowed after all calculations.")]
    public int timeClampMax = 45;
    [Tooltip("Custom thresholds for respawn time based on the remaining match time. Each threshold defines a time range and the respawn time value to use if the remaining time falls within that range.")]
    public RespawnTimeThreshold[] respawnTimeThresholds;
}

[System.Serializable]
public class RespawnTimeThreshold
{
    [Tooltip("Inclusive minimum remaining match time (in seconds) for this threshold.")]
    public float minTime;
    [Tooltip("Exclusive maximum remaining match time (in seconds) for this threshold.")]
    public float maxTime;
    [Tooltip("Respawn time value to use if the remaining time is within this range.")]
    public int value;
}

public enum GameMode : byte
{
    Timed,
    Timeless
}

public enum CharacterSelectType : byte
{
    BlindPick,
    Draft,
    PsyduckRacing
}

public enum StartRestriction : byte
{
    None,
    SameTeamSizes,
    FullTeams
}
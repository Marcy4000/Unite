using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "MapInfo", menuName = "Create New Map")]
public class MapInfo : ScriptableObject
{
    public string mapName;
    public Sprite mapIcon;
    public string sceneName;
    public string mapSceneKey;

    public int maxTeamSize;

    [Space]

    public AssetReferenceSprite mapResultsBlue;
    public AssetReferenceSprite mapResultsOrange;

    [Space]

    public GameMode gameMode;
    public float gameTime;
    public float finalStretchTime;
    public ushort maxScore;

    [Space]

    public DefaultAudioMusic normalMusic;
    public DefaultAudioMusic finalStretchMusic;

    [Space]

    public CharacterSelectType characterSelectType;

    [TextArea(3, 10)]
    public string description;
}

public enum GameMode : byte
{
    Timed,
    Timeless
}

public enum CharacterSelectType 
{
    BlindPick,
    Draft,
    PsyduckRacing
}
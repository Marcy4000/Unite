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

    public float gameTime;
    public float finalStretchTime;

    [Space]

    public DefaultAudioMusic normalMusic;
    public DefaultAudioMusic finalStretchMusic;

    [Space]

    public bool useDraftMode;

    [TextArea(3, 10)]
    public string description;
}

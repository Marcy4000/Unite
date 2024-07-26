using UnityEngine;

[CreateAssetMenu(fileName = "MapInfo", menuName = "Create New Map")]
public class MapInfo : ScriptableObject
{
    public string mapName;
    public Sprite mapIcon;
    public string sceneName;
    public string mapSceneKey;

    [Space]

    public float gameTime;
    public float finalStretchTime;

    [Space]

    public DefaultAudioMusic normalMusic;
    public DefaultAudioMusic finalStretchMusic;

    [Space]

    [TextArea(3, 10)]
    public string description;
}

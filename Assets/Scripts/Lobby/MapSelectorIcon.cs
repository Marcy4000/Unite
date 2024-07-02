using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapSelectorIcon : MonoBehaviour
{
    [SerializeField] Image mapIcon;
    [SerializeField] TMP_Text mapNameText;

    [SerializeField] MapInfo defaultMap;

    Sprite MapIcon => mapIcon.sprite;
    string MapName => mapNameText.text;

    private void Start()
    {
        if (defaultMap != null)
        {
            InitializeElement(defaultMap);
        }
    }

    public void InitializeElement(MapInfo mapInfo)
    {
        mapIcon.sprite = mapInfo.mapIcon;
        mapNameText.text = mapInfo.mapName;
    }
}

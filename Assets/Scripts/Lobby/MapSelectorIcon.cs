using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapSelectorIcon : MonoBehaviour
{
    [SerializeField] private Image mapIcon;
    [SerializeField] private TMP_Text mapNameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Toggle toggle;

    [SerializeField] private MapInfo defaultMap;

    public MapInfo MapInfo => defaultMap;
    public bool IsSelected => toggle.isOn;

    public event Action<MapInfo, bool> OnToggleChanged;

    private void Start()
    {
        if (defaultMap != null)
        {
            InitializeElement(defaultMap, null);
        }
    }

    public void InitializeElement(MapInfo mapInfo, ToggleGroup toggleGroup)
    {
        defaultMap = mapInfo;
        mapIcon.sprite = mapInfo.mapIcon;
        mapNameText.text = mapInfo.mapName;
        playerCountText.text = $"{mapInfo.maxTeamSize} vs {mapInfo.maxTeamSize}";
        if (toggleGroup != null) toggle.group = toggleGroup;
        toggle.onValueChanged.AddListener(OnToggleValueChanged);
    }

    private void OnToggleValueChanged(bool value)
    {
        OnToggleChanged?.Invoke(defaultMap, value);
    }

    public void SetToggleEnabled(bool enabled)
    {
        toggle.interactable = enabled;
    }

    public void SelectToggle()
    {
        toggle.isOn = true;
    }
}

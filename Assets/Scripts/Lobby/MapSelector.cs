using JSAM;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MapSelector : MonoBehaviour
{
    [SerializeField] private GameObject mapSelectorPrefab;
    [SerializeField] private Transform mapSelectorHolder;
    [SerializeField] private ToggleGroup toggleGroup;

    private List<MapSelectorIcon> icons = new List<MapSelectorIcon>();
    private bool isHost;

    public void Initialize(bool isHost)
    {
        this.isHost = isHost;
        
        foreach (MapSelectorIcon icon in icons)
        {
            Destroy(icon.gameObject);
        }

        icons.Clear();

        foreach (MapInfo map in CharactersList.Instance.Maps)
        {
            GameObject mapSelector = Instantiate(mapSelectorPrefab, mapSelectorHolder);
            MapSelectorIcon mapIcon = mapSelector.GetComponent<MapSelectorIcon>();
            mapIcon.InitializeElement(map, toggleGroup);
            icons.Add(mapIcon);

            if (isHost)
            {
                mapIcon.OnToggleChanged += OnToggleChange;
            }
        }

        UpdateTogglesEnabled(isHost);
        UpdateSelectedMap(false);
    }

    private void OnToggleChange(MapInfo mapInfo, bool value)
    {
        if (value)
        {
            if (mapInfo.maxTeamSize*2 < LobbyController.Instance.Lobby.Players.Count)
            {
                UpdateSelectedMap();
                return;
            }
            LobbyController.Instance.ChangeLobbyMap(mapInfo);
        }
    }

    public void UpdateTogglesEnabled(bool enabled)
    {
        foreach (MapSelectorIcon icon in icons)
        {
            icon.SetToggleEnabled(enabled);
        }
    }

    public void UpdateSelectedMap(bool ignoreHost=true)
    {
        if (isHost && ignoreHost)
        {
            return;
        }

        foreach (MapSelectorIcon icon in icons)
        {
            if (icon.MapInfo == CharactersList.Instance.GetCurrentLobbyMap())
            {
                icon.SelectToggle();
                return;
            }
        }
    }

    public MapInfo GetSelectedMap()
    {
        foreach (MapSelectorIcon icon in icons)
        {
            if (icon.IsSelected)
            {
                return icon.MapInfo;
            }
        }

        return null;
    }
}

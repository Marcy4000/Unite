using UnityEngine;
using System.Collections.Generic;

public class MinimapManager : MonoBehaviour
{
    public static MinimapManager Instance;
    private readonly Dictionary<PlayerManager, MapPlayerIcon> playerIcons = new();

    public RectTransform minimapRect;

    [SerializeField] private RectTransform[] minimapLayers;

    [SerializeField] private GameObject playerIconPrefab;
    [SerializeField] private GameObject goalzoneIconPrefab;
    [SerializeField] private GameObject objectiveIconPrefab;
    [SerializeField] private GameObject wildPokemonIconPrefab;
    [SerializeField] private GameObject pingIconPrefab;

    public float worldSizeX = 100f;
    public float worldSizeZ = 100f;

    private void Awake()
    {
        Instance = this;
    }

    public void CreatePlayerIcon(PlayerManager player)
    {
        if (player == null || player.Pokemon == null) return;
        if (playerIcons.TryGetValue(player, out var existing) && existing != null) return;
        GameObject icon = Instantiate(playerIconPrefab, minimapLayers[0]);
        MapPlayerIcon playerIcon = icon.GetComponent<MapPlayerIcon>();
        playerIcons[player] = playerIcon;
        playerIcon.Initialize(player);
        playerIcon.MinimapIcon.Initialize(minimapRect, worldSizeX, worldSizeZ);
    }

    public void RemovePlayerIcon(PlayerManager player)
    {
        if (!playerIcons.TryGetValue(player, out var icon)) return;
        playerIcons.Remove(player);
        if (icon != null) Destroy(icon.gameObject);
    }

    private void OnDestroy()
    {
        playerIcons.Clear();
        if (Instance == this) Instance = null;
    }

    public void CreateGoalzoneIcon(GoalZone goalZone)
    {
        GameObject icon = Instantiate(goalzoneIconPrefab, minimapLayers[3]);
        MapGoalzoneIcon goalzoneIcon = icon.GetComponent<MapGoalzoneIcon>();
        goalzoneIcon.Initialize(goalZone);
        goalzoneIcon.MinimapIcon.Initialize(minimapRect, worldSizeX, worldSizeZ);
    }

    public void CreateObjectiveIcon(WildPokemon wildPokemon)
    {
        GameObject icon = Instantiate(objectiveIconPrefab, minimapLayers[1]);
        MapObjective objectiveIcon = icon.GetComponent<MapObjective>();
        objectiveIcon.Initialize(wildPokemon);
        objectiveIcon.MinimapIcon.Initialize(minimapRect, worldSizeX, worldSizeZ);

        objectiveIcon.DoAnimation();
    }

    public void CreateWildPokemonIcon(WildPokemonSpawner wildPokemonSpawner)
    {
        GameObject icon = Instantiate(wildPokemonIconPrefab, minimapLayers[2]);
        MapWildPokemon wildPokemonIcon = icon.GetComponent<MapWildPokemon>();
        wildPokemonIcon.Initialize(wildPokemonSpawner);
        wildPokemonIcon.MinimapIcon.Initialize(minimapRect, worldSizeX, worldSizeZ);
    }

    public void CreatePingIcon(Vector3 position)
    {
        GameObject icon = Instantiate(pingIconPrefab, minimapLayers[4]);
        MapPing pingIcon = icon.GetComponent<MapPing>();
        pingIcon.DoAnimation();
        pingIcon.MinimapIcon.Initialize(minimapRect, worldSizeX, worldSizeZ);
        pingIcon.MinimapIcon.UpdateIconPosition(position);
    }
}

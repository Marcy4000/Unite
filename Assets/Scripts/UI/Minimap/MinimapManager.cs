using UnityEngine;

public class MinimapManager : MonoBehaviour
{
    public static MinimapManager Instance;

    public RectTransform minimapRect;

    [SerializeField] private RectTransform[] minimapLayers;

    [SerializeField] private GameObject playerIconPrefab;
    [SerializeField] private GameObject goalzoneIconPrefab;
    [SerializeField] private GameObject objectiveIconPrefab;
    [SerializeField] private GameObject wildPokemonIconPrefab;

    public float worldSizeX = 100f;
    public float worldSizeZ = 100f;

    private void Awake()
    {
        Instance = this;
    }

    public void CreatePlayerIcon(PlayerManager player)
    {
        GameObject icon = Instantiate(playerIconPrefab, minimapLayers[0]);
        MapPlayerIcon playerIcon = icon.GetComponent<MapPlayerIcon>();
        playerIcon.Initialize(player);
        playerIcon.MinimapIcon.Initialize(minimapRect, worldSizeX, worldSizeZ);
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

        icon.transform.SetSiblingIndex(1);
    }

    public void CreateWildPokemonIcon(WildPokemonSpawner wildPokemonSpawner)
    {
        GameObject icon = Instantiate(wildPokemonIconPrefab, minimapLayers[2]);
        MapWildPokemon wildPokemonIcon = icon.GetComponent<MapWildPokemon>();
        wildPokemonIcon.Initialize(wildPokemonSpawner);
        wildPokemonIcon.MinimapIcon.Initialize(minimapRect, worldSizeX, worldSizeZ);

        icon.transform.SetSiblingIndex(1);
    }
}

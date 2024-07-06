using UnityEngine;

public class MinimapManager : MonoBehaviour
{
    public static MinimapManager Instance;

    public RectTransform minimapRect;

    [SerializeField] private GameObject playerIconPrefab;
    [SerializeField] private GameObject goalzoneIconPrefab;
    [SerializeField] private GameObject objectiveIconPrefab;

    public float worldSizeX = 100f;
    public float worldSizeZ = 100f;

    private void Awake()
    {
        Instance = this;
    }

    public void CreatePlayerIcon(PlayerManager player)
    {
        GameObject icon = Instantiate(playerIconPrefab, minimapRect);
        MapPlayerIcon playerIcon = icon.GetComponent<MapPlayerIcon>();
        playerIcon.Initialize(player);
        playerIcon.MinimapIcon.Initialize(minimapRect, worldSizeX, worldSizeZ);
    }

    public void CreateGoalzoneIcon(GoalZone goalZone)
    {
        GameObject icon = Instantiate(goalzoneIconPrefab, minimapRect);
        MapGoalzoneIcon goalzoneIcon = icon.GetComponent<MapGoalzoneIcon>();
        goalzoneIcon.Initialize(goalZone);
        goalzoneIcon.MinimapIcon.Initialize(minimapRect, worldSizeX, worldSizeZ);
    }

    public void CreateObjectiveIcon(WildPokemon wildPokemon)
    {
        GameObject icon = Instantiate(objectiveIconPrefab, minimapRect);
        MapObjective objectiveIcon = icon.GetComponent<MapObjective>();
        objectiveIcon.Initialize(wildPokemon);
        objectiveIcon.MinimapIcon.Initialize(minimapRect, worldSizeX, worldSizeZ);

        icon.transform.SetSiblingIndex(1);
    }
}

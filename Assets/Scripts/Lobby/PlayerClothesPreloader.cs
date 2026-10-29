using System.Collections.Generic;
using Player = Unite.Multiplayer.GameSessionPlayer;
using Lobby = Unite.Multiplayer.GameSessionView;
using UnityEngine;

public class PlayerClothesPreloader : MonoBehaviour
{
    public static PlayerClothesPreloader Instance { get; private set; }

    [SerializeField] private GameObject trainerModelPrefab;

    private Lobby currentLobby;
    private Dictionary<string, TrainerModel> playerModels = new Dictionary<string, TrainerModel>();

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        LobbyController.Instance.onLobbyUpdate += OnLobbyUpdated;
    }

    private void OnDestroy()
    {
        if (LobbyController.Instance != null)
            LobbyController.Instance.onLobbyUpdate -= OnLobbyUpdated;
        if (Instance == this) Instance = null;
    }

    private void OnLobbyUpdated(Lobby lobby)
    {
        currentLobby = lobby;
        if (currentLobby == null)
        {
            ClearAllModels();
            return;
        }
        UpdateLoadedModels();
    }

    private void UpdateLoadedModels()
    {
        foreach (var player in currentLobby.Players)
        {
            if (!playerModels.TryGetValue(player.Id, out var model) || model == null)
            {
                playerModels.Remove(player.Id);
                LoadPlayerModel(player);
            }
            else
            {
                TryUpdatingPlayerModel(player);
            }
        }

        var departedPlayers = new List<string>();
        foreach (var playerModel in playerModels)
        {
            if (!currentLobby.Players.Exists(player => player.Id == playerModel.Key))
            {
                departedPlayers.Add(playerModel.Key);
            }
        }
        foreach (var playerId in departedPlayers)
            UnloadPlayerModel(playerId);
    }

    private void LoadPlayerModel(Player player)
    {
        if (trainerModelPrefab == null || !player.Data.TryGetValue("ClothingInfo", out var clothing)) return;
        var playerModel = Instantiate(trainerModelPrefab, transform).GetComponent<TrainerModel>();
        playerModel.transform.position = new Vector3(0, -100, 0);
        playerModel.name = player.Id;
        playerModels.Add(player.Id, playerModel);
        var playerClothes = PlayerClothesInfo.Deserialize(clothing.Value);

        playerModel.InitializeClothes(playerClothes);
        DontDestroyOnLoad(playerModel.gameObject);
    }

    private void TryUpdatingPlayerModel(Player player)
    {
        if (!player.Data.TryGetValue("ClothingInfo", out var clothing)) return;
        var playerClothes = PlayerClothesInfo.Deserialize(clothing.Value);
        var playerModel = playerModels[player.Id];

        if (!playerModel.PlayerClothesInfo.Equals(playerClothes))
        {
            playerModel.InitializeClothes(playerClothes);
        }
    }

    private void UnloadPlayerModel(string playerId)
    {
        if (playerModels.ContainsKey(playerId))
        {
            if (playerModels[playerId] != null) Destroy(playerModels[playerId].gameObject);
            playerModels.Remove(playerId);
        }
    }

    public void ClearAllModels()
    {
        foreach (var playerModel in playerModels)
        {
            if (playerModel.Value != null) Destroy(playerModel.Value.gameObject);
        }
        playerModels.Clear();
    }

    public GameObject GetPlayerModel(string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return null;
        if (playerModels.TryGetValue(playerId, out var model) && model != null)
        {
            return model.gameObject;
        }
        playerModels.Remove(playerId);
        var player = currentLobby?.Players.Find(candidate => candidate.Id == playerId);
        if (player == null) return null;
        LoadPlayerModel(player);
        return playerModels.TryGetValue(playerId, out model) && model != null ? model.gameObject : null;
    }
}

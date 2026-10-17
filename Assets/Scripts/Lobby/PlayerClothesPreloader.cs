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
            if (!playerModels.ContainsKey(player.Id))
            {
                LoadPlayerModel(player);
            }
            else if (playerModels.ContainsKey(player.Id))
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
        var playerModel = Instantiate(trainerModelPrefab, transform).GetComponent<TrainerModel>();
        playerModel.transform.position = new Vector3(0, -100, 0);
        playerModel.name = player.Id;
        playerModels.Add(player.Id, playerModel);
        var playerClothes = PlayerClothesInfo.Deserialize(player.Data["ClothingInfo"].Value);

        playerModel.InitializeClothes(playerClothes);
        DontDestroyOnLoad(playerModel.gameObject);
    }

    private void TryUpdatingPlayerModel(Player player)
    {
        var playerClothes = PlayerClothesInfo.Deserialize(player.Data["ClothingInfo"].Value);
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
            Destroy(playerModels[playerId].gameObject);
            playerModels.Remove(playerId);
        }
    }

    public void ClearAllModels()
    {
        foreach (var playerModel in playerModels)
        {
            Destroy(playerModel.Value.gameObject);
        }
        playerModels.Clear();
    }

    public GameObject GetPlayerModel(string playerId)
    {
        if (playerModels.ContainsKey(playerId))
        {
            return playerModels[playerId].gameObject;
        }
        return null;
    }
}

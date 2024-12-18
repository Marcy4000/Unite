using Unity.Services.Lobbies.Models;
using UnityEngine;

public class GameInfoUI : MonoBehaviour
{
    [SerializeField] private GameObject playerInfoPrefab;
    [SerializeField] private Transform blueTeamSpawn, orangeTeamSpawn;

    private Lobby currentLobby;

    public void Initialize()
    {
        currentLobby = LobbyController.Instance.Lobby;

        Player[] blueTeam = LobbyController.Instance.GetTeamPlayers(Team.Blue);
        Player[] orangeTeam = LobbyController.Instance.GetTeamPlayers(Team.Orange);

        foreach (var player in blueTeam)
        {
            PlayerInfoUI info = Instantiate(playerInfoPrefab, blueTeamSpawn).GetComponent<PlayerInfoUI>();
            info.SetPlayerInfo(player, PlayerInfoType.Normal);
        }

        foreach (var player in orangeTeam)
        {
            PlayerInfoUI info = Instantiate(playerInfoPrefab, orangeTeamSpawn).GetComponent<PlayerInfoUI>();
            info.SetPlayerInfo(player, PlayerInfoType.Normal);
        }
    }
}

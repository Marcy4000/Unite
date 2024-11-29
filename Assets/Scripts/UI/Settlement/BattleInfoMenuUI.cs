using Unity.Services.Lobbies.Models;
using UnityEngine;

public class BattleInfoMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;

    [SerializeField] private Transform blueTeamList, orangeTeamList;

    [SerializeField] private PlayerInfoType playerInfoType;

    private Lobby currentLobby;

    public void InitializeMenu()
    {
        currentLobby = LobbyController.Instance.Lobby;

        Player[] blueTeam = LobbyController.Instance.GetTeamPlayers(Team.Blue);
        Player[] orangeTeam = LobbyController.Instance.GetTeamPlayers(Team.Orange);

        foreach (var player in blueTeam)
        {
            PlayerInfoUI info = Instantiate(playerPrefab, blueTeamList).GetComponent<PlayerInfoUI>();
            info.SetPlayerInfo(player, playerInfoType);
        }

        foreach (var player in orangeTeam)
        {
            PlayerInfoUI info = Instantiate(playerPrefab, orangeTeamList).GetComponent<PlayerInfoUI>();
            info.SetPlayerInfo(player, playerInfoType);
        }
    }
}

using TMPro;
using Unity.Services.Lobbies.Models;
using Unity.VisualScripting;
using UnityEngine;

public class SettlementTeamPlayer : MonoBehaviour
{
    [SerializeField] private TMP_Text playerName;
    [SerializeField] private TMP_Text pokemonName;

    public void Initialize(Player player)
    {
        if (player == null) {
            playerName.text = "";
            pokemonName.text = "";
            return;
        }

        if (player.Id == LobbyController.Instance.Player.Id)
        {
            playerName.color = Color.yellow;
        }

        playerName.text = player.Data["PlayerName"].Value;
        pokemonName.text = player.Data["SelectedCharacter"].Value;

        pokemonName.text.FirstCharacterToUpper();
    }
}

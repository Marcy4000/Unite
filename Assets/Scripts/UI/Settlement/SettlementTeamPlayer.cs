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

        CharacterInfo character = CharactersList.Instance.GetCharacterFromID(NumberEncoder.FromBase64<short>(player.Data["SelectedCharacter"].Value));
        if (character != null)
            pokemonName.text = character.pokemonName;

        pokemonName.text.FirstCharacterToUpper();
    }
}

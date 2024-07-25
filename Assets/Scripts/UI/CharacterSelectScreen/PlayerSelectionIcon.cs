using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSelectionIcon : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image characterIcon;
    [SerializeField] private Image battleItem;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Sprite[] backgrounds;

    [SerializeField] private Sprite defaultBackground;

    private Player assignedPlayer;

    public void Initialize(Player player)
    {
        assignedPlayer = player;
        playerNameText.text = player.Data["PlayerName"].Value;
        if (assignedPlayer.Id == LobbyController.Instance.Player.Id)
        {
            playerNameText.color = Color.yellow;
        }
        characterIcon.gameObject.SetActive(false);
        LobbyController.Instance.onLobbyUpdate += UpdatePlayerData;
        UpdatePlayerData(LobbyController.Instance.Lobby);
    }

    private void OnDisable()
    {
        LobbyController.Instance.onLobbyUpdate -= UpdatePlayerData;
    }

    private void UpdatePlayerData(Lobby lobby)
    {
        assignedPlayer = lobby.Players.Find(x => x.Id == assignedPlayer.Id);

        CharacterInfo info = CharactersList.Instance.GetCharacterFromString(assignedPlayer.Data["SelectedCharacter"].Value);

        if (info != null)
        {
            characterIcon.gameObject.SetActive(true);
            battleItem.gameObject.SetActive(true);
            background.sprite = GetBackgroundFromClass(info.pClass);
            characterIcon.sprite = info.icon;
            battleItem.sprite = CharactersList.Instance.GetBattleItemByID(int.Parse(assignedPlayer.Data["BattleItem"].Value)).icon;
        }
        else
        {
            background.sprite = defaultBackground;
            characterIcon.gameObject.SetActive(false);
            battleItem.gameObject.SetActive(false);
        }
    }

    private Sprite GetBackgroundFromClass(PokemonClass pClass)
    {
        switch (pClass)
        {
            case PokemonClass.Attacker:
                return backgrounds[0];
            case PokemonClass.Defender:
                return backgrounds[1];
            case PokemonClass.Supporter:
                return backgrounds[2];
            case PokemonClass.AllRounder:
                return backgrounds[3];
            case PokemonClass.Speedster:
                return backgrounds[4];
            default:
                return null;
        }
    }
}

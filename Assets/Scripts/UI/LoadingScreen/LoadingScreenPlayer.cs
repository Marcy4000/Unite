using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreenPlayer : MonoBehaviour
{
    [SerializeField] private Image portrait, playerBar, battleItem;
    [SerializeField] private TMP_Text playerName, pokemonName;

    [SerializeField] private Sprite blueSprite, orangeSprite;
    [SerializeField] private GameObject localPlayerImage;

    [SerializeField] private Image loadingBar;

    private Player currentPlayer;

    public Player CurrentPlayer => currentPlayer;

    public void SetPlayerData(Player player)
    {
        currentPlayer = player;
        CharacterInfo info = CharactersList.instance.GetCharacterFromString(player.Data["SelectedCharacter"].Value);

        portrait.sprite = info.portrait;
        playerName.text = player.Data["PlayerName"].Value;
        pokemonName.text = info.pokemonName;
        bool orangeTeam = player.Data["PlayerTeam"].Value == "Orange";
        playerBar.sprite = orangeTeam ? orangeSprite : blueSprite;
        battleItem.sprite = CharactersList.instance.GetBattleItemByID(int.Parse(player.Data["BattleItem"].Value)).icon;
        localPlayerImage.SetActive(player.Id == LobbyController.Instance.Player.Id);
    }

    public void UpdateProgressBar(float prev, float progress)
    {
        loadingBar.fillAmount = progress;
    }
}

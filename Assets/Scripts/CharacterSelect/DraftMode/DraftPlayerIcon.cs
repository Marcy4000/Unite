using DG.Tweening;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public enum DraftPlayerState : byte
{
    Idle, Banning, Picking, Confirmed
}

public class DraftPlayerIcon : MonoBehaviour
{
    [SerializeField] private GameObject glowObject, playerHolder, selectedCharacterHolder, heldItemsHolder, battleItemHolder, banUIHolder, playerHead, confirmCheck;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image selectedCharacterIcon, bannedCharacterIcon, battleItemSprite;

    [SerializeField] private Sprite emptyBattleItem;

    [Space]

    [SerializeField] private float playerBGIdlePos, playerBGSelectedPos;

    private DraftPlayerState state;

    private Player assignedPlayer;

    public Player AssignedPlayer => assignedPlayer;

    public void Initialize(Player player)
    {
        assignedPlayer = player;
        playerNameText.text = player.Data["PlayerName"].Value;
        selectedCharacterHolder.gameObject.SetActive(false);
        state = DraftPlayerState.Idle;
    }

    public void UpdateIconState(DraftPlayerState state)
    {
        this.state = state;
        switch (state)
        {
            case DraftPlayerState.Idle:
                UpdateHighlitedState(false);
                banUIHolder.SetActive(false);
                confirmCheck.SetActive(false);
                playerHead.SetActive(true);
                battleItemHolder.SetActive(false);
                heldItemsHolder.SetActive(false);
                break;
            case DraftPlayerState.Banning:
                UpdateHighlitedState(true);
                banUIHolder.SetActive(true);
                confirmCheck.SetActive(false);
                playerHead.SetActive(true);
                battleItemHolder.SetActive(false);
                heldItemsHolder.SetActive(false);
                break;
            case DraftPlayerState.Picking:
                UpdateHighlitedState(true);
                banUIHolder.SetActive(false);
                confirmCheck.SetActive(false);
                playerHead.SetActive(true);
                battleItemHolder.SetActive(false);
                heldItemsHolder.SetActive(false);
                break;
            case DraftPlayerState.Confirmed:
                UpdateHighlitedState(false);
                banUIHolder.SetActive(false);
                confirmCheck.SetActive(true);
                playerHead.SetActive(false);
                battleItemHolder.SetActive(true);
                heldItemsHolder.SetActive(true);
                break;
        }
    }

    private void UpdateHighlitedState(bool highlighted)
    {
        if (highlighted)
        {
            playerHolder.GetComponent<RectTransform>().DOAnchorPosX(playerBGSelectedPos, 0.3f);
            glowObject.SetActive(true);
        }
        else
        {
            playerHolder.GetComponent<RectTransform>().DOAnchorPosX(playerBGIdlePos, 0.3f);
            glowObject.SetActive(false);
        }
    }

    public void UpdateSelectedCharacter(CharacterInfo info)
    {
        if (info == null)
        {
            selectedCharacterHolder.gameObject.SetActive(false);
            return;
        }
        selectedCharacterHolder.gameObject.SetActive(true);
        selectedCharacterIcon.sprite = info.icon;
    }

    public void UpdateBannedCharacter(CharacterInfo info)
    {
        if (info == null)
        {
            bannedCharacterIcon.gameObject.SetActive(false);
            return;
        }
        bannedCharacterIcon.gameObject.SetActive(true);
        bannedCharacterIcon.sprite = info.icon;
    }

    public void UpdateBattleItem(BattleItemAsset battleItem)
    {
        if (battleItemSprite == null) return;

        if (battleItem == null)
        {
            battleItemSprite.sprite = emptyBattleItem;
            return;
        }
        battleItemSprite.sprite = battleItem.icon;
    }

    public void UpdatePlayerData(Lobby lobby)
    {
        assignedPlayer = lobby.Players.Find(x => x.Id == assignedPlayer.Id);

        CharacterInfo info = CharactersList.Instance.GetCharacterFromString(assignedPlayer.Data["SelectedCharacter"].Value);

        UpdateSelectedCharacter(info);
        UpdateBattleItem(CharactersList.Instance.GetBattleItemByID(int.Parse(assignedPlayer.Data["BattleItem"].Value)));
    }
}

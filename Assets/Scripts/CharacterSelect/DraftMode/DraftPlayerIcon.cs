using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public enum DraftPlayerState : byte
{
    Idle, Banning, Picking, Confirmed, BanningIdle, BanningConfirmed
}

public class DraftPlayerIcon : MonoBehaviour
{
    [SerializeField] private GameObject playerHolder, selectedCharacterHolder, battleItemHolder, banUIHolder, confirmCheck;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image selectedCharacterIcon, bannedCharacterIcon, battleItemSprite, glowObject;
    [SerializeField] private PlayerHeadUI playerHead;
    [SerializeField] private PlayerHeldItemsIcons heldItemsHolder;

    [SerializeField] private Sprite emptyBattleItem;

    [Space]

    [SerializeField] private float playerBGIdlePos, playerBGSelectedPos;

    private DraftPlayerState state;
    private CharacterInfo selectedCharacter;
    private CharacterInfo bannedCharacter;

    private bool autoSyncCharacter = true;

    private Player assignedPlayer;

    public Player AssignedPlayer => assignedPlayer;
    public CharacterInfo SelectedCharacter => selectedCharacter;
    public CharacterInfo BannedCharacter => bannedCharacter;

    public void Initialize(Player player, bool autoSyncCharacter=true)
    {
        assignedPlayer = player;
        playerNameText.text = player.Data["PlayerName"].Value;
        this.autoSyncCharacter = autoSyncCharacter;
        UpdateSelectedCharacter(null);
        UpdateBannedCharacter(null);
        playerHead.InitializeHead(PlayerClothesInfo.Deserialize(player.Data["ClothingInfo"].Value));
        UpdateIconState(DraftPlayerState.Idle);
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
                playerHead.gameObject.SetActive(true);
                battleItemHolder.SetActive(false);
                if (heldItemsHolder != null)
                    heldItemsHolder.gameObject.SetActive(false);
                break;
            case DraftPlayerState.Banning:
                UpdateHighlitedState(true);
                banUIHolder.SetActive(true);
                confirmCheck.SetActive(false);
                playerHead.gameObject.SetActive(true);
                battleItemHolder.SetActive(false);
                if (heldItemsHolder != null)
                    heldItemsHolder.gameObject.SetActive(false);
                break;
            case DraftPlayerState.Picking:
                UpdateHighlitedState(true);
                banUIHolder.SetActive(false);
                confirmCheck.SetActive(false);
                playerHead.gameObject.SetActive(true);
                battleItemHolder.SetActive(false);
                if (heldItemsHolder != null)
                    heldItemsHolder.gameObject.SetActive(false);
                break;
            case DraftPlayerState.Confirmed:
                UpdateHighlitedState(false);
                banUIHolder.SetActive(false);
                confirmCheck.SetActive(true);
                playerHead.gameObject.SetActive(false);
                battleItemHolder.SetActive(true);
                if (heldItemsHolder != null)
                    heldItemsHolder.gameObject.SetActive(true);
                break;
            case DraftPlayerState.BanningIdle:
                UpdateHighlitedState(false);
                banUIHolder.SetActive(true);
                confirmCheck.SetActive(false);
                playerHead.gameObject.SetActive(true);
                battleItemHolder.SetActive(false);
                if (heldItemsHolder != null)
                    heldItemsHolder.gameObject.SetActive(false);
                break;
            case DraftPlayerState.BanningConfirmed:
                UpdateHighlitedState(false);
                banUIHolder.SetActive(true);
                confirmCheck.SetActive(true);
                playerHead.gameObject.SetActive(true);
                battleItemHolder.SetActive(false);
                if (heldItemsHolder != null)
                    heldItemsHolder.gameObject.SetActive(false);
                break;
        }
    }

    private void UpdateHighlitedState(bool highlighted)
    {
        if (highlighted)
        {
            playerHolder.GetComponent<RectTransform>().DOAnchorPosX(playerBGSelectedPos, 0.3f);
            glowObject.gameObject.SetActive(true);

            glowObject.DOColor(new Color(1, 1, 1, 0.5f), 1f).SetLoops(-1, LoopType.Yoyo);
        }
        else
        {
            playerHolder.GetComponent<RectTransform>().DOAnchorPosX(playerBGIdlePos, 0.3f);
            glowObject.gameObject.SetActive(false);

            glowObject.DOKill();
            glowObject.color = Color.white;
        }
    }

    public void UpdateSelectedCharacter(CharacterInfo info)
    {
        selectedCharacter = info;

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
        bannedCharacter = info;

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

    public void UpdateHeldItems(List<HeldItemInfo> heldItems)
    {
        if (heldItemsHolder == null) return;

        if (heldItems == null || heldItems.Count == 0)
        {
            heldItemsHolder.gameObject.SetActive(false);
            return;
        }

        heldItemsHolder.SetIcons(heldItems);
    }

    public void UpdatePlayerData(Lobby lobby)
    {
        assignedPlayer = lobby.Players.Find(x => x.Id == assignedPlayer.Id);

        if (autoSyncCharacter)
        { 
            CharacterInfo info = CharactersList.Instance.GetCharacterFromID(NumberEncoder.FromBase64<short>(assignedPlayer.Data["SelectedCharacter"].Value));
            UpdateSelectedCharacter(info);
        }

        UpdateBattleItem(CharactersList.Instance.GetBattleItemByID(int.Parse(assignedPlayer.Data["BattleItem"].Value)));

        UpdateHeldItems(HeldItemDatabase.DeserializeHeldItems(assignedPlayer.Data["HeldItems"].Value));
    }
}

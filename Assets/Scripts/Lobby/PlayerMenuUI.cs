using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class PlayerMenuUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerName;
    [SerializeField] private TrainerCardUI trainerCard;
    [SerializeField] private RectTransform background;

    private Player currentPlayer;

    public Player CurrentPlayer => currentPlayer;

    private void Start()
    {
        currentPlayer = LobbyController.Instance.Player;
        playerName.text = currentPlayer.Data["PlayerName"].Value;
        trainerCard.Initialize(PlayerClothesInfo.Deserialize(currentPlayer.Data["ClothingInfo"].Value));
    }

    private void OnEnable()
    {
        DoFadeInAnimation();
    }

    private void DoFadeInAnimation()
    {
        background.anchoredPosition = new Vector2(-513.5554f, background.anchoredPosition.y);
        background.sizeDelta = new Vector2(656.8892f, background.rect.height);

        background.DOAnchorPosX(1.1009f, 0.5f);
        background.DOSizeDelta(new Vector2(1686.202f, background.rect.height), 0.5f);
    }
}

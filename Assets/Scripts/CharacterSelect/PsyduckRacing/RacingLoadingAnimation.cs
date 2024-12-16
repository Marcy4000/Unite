using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RacingLoadingAnimation : MonoBehaviour
{
    [SerializeField] private RectTransform cardsHolder;
    [SerializeField] private TrainerCardUI[] trainerCards;

    private void Start()
    {
        Unity.Services.Lobbies.Models.Player[] players = LobbyController.Instance.Lobby.Players.ToArray();

        for (int i = 0; i < trainerCards.Length; i++)
        {
            if (i < players.Length)
            {
                PlayerClothesInfo playerClothesInfo = PlayerClothesInfo.Deserialize(players[i].Data["ClothingInfo"].Value);
                trainerCards[i].gameObject.SetActive(true);
                trainerCards[i].Initialize(playerClothesInfo);
            }
            else
            {
                trainerCards[i].gameObject.SetActive(false);
            }
        }

        cardsHolder.anchoredPosition = new Vector2(14.05f, 0f);

        cardsHolder.DOAnchorPosX(-40.66f, 5f).SetDelay(1f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.Linear);
    }
}

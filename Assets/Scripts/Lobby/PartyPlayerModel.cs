using System.Collections;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class PartyPlayerModel : MonoBehaviour
{
    [SerializeField] private TrainerModel trainerModel;
    [SerializeField] private TMPro.TMP_Text trainerNameText;
    [SerializeField] private GameObject lobbyOwnerIcon;

    [SerializeField] private string[] maleVictoryAnimations;
    [SerializeField] private string[] femaleVictoryAnimations;

    public TrainerModel TrainerModel => trainerModel;

    private string playerClothes;
    private string playerID;

    public void Initialize(Player player)
    {
        string clothes = player.Data["ClothingInfo"].Value;

        if (clothes == playerClothes && player.Id == playerID)
        {
            return;
        }

        playerID = player.Id;
        playerClothes = clothes;
        trainerNameText.text = player.Data["PlayerName"].Value;
        trainerModel.InitializeClothes(PlayerClothesInfo.Deserialize(clothes));
        lobbyOwnerIcon.SetActive(player.Id == LobbyController.Instance.Lobby.HostId);
    }

    public void PlayVictoryAnimation()
    {
        if (trainerModel.gameObject.activeSelf)
        {
            int animIndex = Random.Range(0, trainerModel.PlayerClothesInfo.IsMale ? maleVictoryAnimations.Length : femaleVictoryAnimations.Length);
            trainerModel.ActiveAnimator.Play(trainerModel.PlayerClothesInfo.IsMale ? maleVictoryAnimations[animIndex] : femaleVictoryAnimations[animIndex]);
        }
    }
}

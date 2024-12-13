using DG.Tweening;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class RacePositionUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerName, racePositionText;
    [SerializeField] private GameObject crownIcon;
    [SerializeField] private RectTransform holder;

    [SerializeField] private Image background, placeBG;

    private RaceLapCounter raceLapCounter;
    private bool localPlayer;

    public RaceLapCounter RaceLapCounter => raceLapCounter;

    public void AssignPlayer(RaceLapCounter lapCounter)
    {
        raceLapCounter = lapCounter;
        Unity.Services.Lobbies.Models.Player lobbyPlayer = NetworkManager.Singleton.SpawnManager.SpawnedObjects[raceLapCounter.AssignedPlayerID].GetComponent<PlayerManager>().LobbyPlayer;

        playerName.text = lobbyPlayer.Data["PlayerName"].Value;

        localPlayer = lobbyPlayer.Id == LobbyController.Instance.Player.Id;

        if (localPlayer)
        {
            background.color = new Color(0f, 0f, 0f, 0.97f);
            placeBG.color = new Color(184f / 255f, 105f / 255f, 255f / 255f, 255f / 255f);
            racePositionText.color = new Color(1f, 1f, 1f, 1f);
        }

        raceLapCounter.OnPlaceChanged += UpdateShownPosition;

        UpdateShownPosition(raceLapCounter.CurrentPlace);
    }

    private void DoAnimation()
    {
        holder.anchoredPosition = new Vector2(-40, 0);

        holder.DOAnchorPosX(0, 0.25f).SetEase(Ease.OutBack);
    }

    private void UpdateShownPosition(short position)
    {
        string prevPosition = racePositionText.text;

        racePositionText.text = position.ToString();
        crownIcon.SetActive(position == 1);


        if (racePositionText.text != prevPosition && localPlayer)
        {
            DoAnimation();
        }
    }
}

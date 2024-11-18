using DG.Tweening;
using JSAM;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleNotificationUI : MonoBehaviour
{
    [SerializeField] private GameObject closeBattle;
    [SerializeField] private GameObject lead;
    [SerializeField] private GameObject hugeLead;
    [SerializeField] private GameObject struggling;
    [SerializeField] private GameObject reallyStruggling;

    [Space]

    [SerializeField] private List<float> notificationTimes = new List<float>();

    private void Start()
    {
        HideAllNotification();
    }

    private void Update()
    {
        if (notificationTimes.Count == 0)
        {
            return;
        }

        for (int i = notificationTimes.Count; i > 0; i--)
        {
            int index = i - 1;
            if (GameManager.Instance.GameTime >= notificationTimes[index])
            {
                ShowNotification();
                notificationTimes.RemoveAt(index);
            }
        }
    }

    private void ShowNotification()
    {
        bool localPlayerOrangeTeam = LobbyController.Instance.GetLocalPlayerTeam();
        int pointDifference = localPlayerOrangeTeam ? GameManager.Instance.OrangeTeamScore - GameManager.Instance.BlueTeamScore : GameManager.Instance.BlueTeamScore - GameManager.Instance.OrangeTeamScore;

        HideAllNotification();

        RectTransform activeNotification = null;

        if (pointDifference <= -100)
        {
            activeNotification = reallyStruggling.GetComponent<RectTransform>();
            FadeIn(activeNotification);
            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerDontgiveup);
        }
        else if (pointDifference <= -21)
        {
            activeNotification = struggling.GetComponent<RectTransform>();
            FadeIn(activeNotification);
            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerYouCanDoIt);
        }
        else if (pointDifference >= -20 && pointDifference <= 20)
        {
            activeNotification = closeBattle.GetComponent<RectTransform>();
            FadeIn(activeNotification);
            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerYoureInTrouble);
        }
        else if (pointDifference >= 21 && pointDifference < 100)
        {
            activeNotification = lead.GetComponent<RectTransform>();
            FadeIn(activeNotification);
            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerFire);
        }
        else if (pointDifference >= 100)
        {
            activeNotification = hugeLead.GetComponent<RectTransform>();
            FadeIn(activeNotification);
            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerFire);
        }

        StartCoroutine(FadeOut(activeNotification));
    }

    private void FadeIn(RectTransform rectTransform)
    {
        rectTransform.gameObject.SetActive(true);
        rectTransform.localScale = Vector3.one * 1.7f;
        rectTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
    }

    private IEnumerator FadeOut(RectTransform rectTransform)
    {
        yield return new WaitForSeconds(3f);

        yield return rectTransform.DOScale(Vector3.one / 1.7f, 0.5f).SetEase(Ease.InBack).WaitForCompletion();

        HideAllNotification();
    }

    private void HideAllNotification()
    {
        closeBattle.SetActive(false);
        lead.SetActive(false);
        hugeLead.SetActive(false);
        struggling.SetActive(false);
        reallyStruggling.SetActive(false);
    }
}

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
            if (GameManager.Instance.GameTime <= notificationTimes[index])
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

        if (pointDifference <= -100)
        {
            reallyStruggling.SetActive(true);
        }
        else if (pointDifference <= -21)
        {
            struggling.SetActive(true);
        }
        else if (pointDifference >= -20 && pointDifference <= 20)
        {
            closeBattle.SetActive(true);
        }
        else if (pointDifference >= 21 && pointDifference < 100)
        {
            lead.SetActive(true);
        }
        else if (pointDifference >= 100)
        {
            hugeLead.SetActive(true);
        }

        Invoke(nameof(HideAllNotification), 3f);
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

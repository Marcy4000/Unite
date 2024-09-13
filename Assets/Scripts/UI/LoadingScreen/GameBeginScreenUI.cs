using DG.Tweening;
using System.Collections;
using UI.ThreeDimensional;
using UnityEngine;

public class GameBeginScreenUI : MonoBehaviour
{
    [SerializeField] private RectTransform[] blueTeamPlayers;
    [SerializeField] private RectTransform[] orangeTeamPlayers;

    [SerializeField] private GameObject bluePlayerHolder;
    [SerializeField] private GameObject orangePlayerHolder;

    [SerializeField] private GameObject lineObject;

    [SerializeField] private RectTransform blueTeam;
    [SerializeField] private RectTransform orangeTeam;

    private UIObject3D[] blueTeamObjects;
    private UIObject3D[] orangeTeamObjects;

    private Coroutine fadeInRoutine;

    private void Start()
    {
        blueTeamObjects = new UIObject3D[blueTeamPlayers.Length];
        orangeTeamObjects = new UIObject3D[orangeTeamPlayers.Length];

        for (int i = 0; i < blueTeamPlayers.Length; i++)
        {
            blueTeamObjects[i] = blueTeamPlayers[i].GetComponent<UIObject3D>();
        }

        for (int i = 0; i < orangeTeamPlayers.Length; i++)
        {
            orangeTeamObjects[i] = orangeTeamPlayers[i].GetComponent<UIObject3D>();
        }

        gameObject.SetActive(false);
    }

    public void InitializeUI(int bluePlayers, int orangePlayers)
    {
        for (int i = 0; i < blueTeamPlayers.Length; i++)
        {
            blueTeamPlayers[i].gameObject.SetActive(true);
        }

        for (int i = 0; i < orangeTeamPlayers.Length; i++)
        {
            orangeTeamPlayers[i].gameObject.SetActive(true);
        }

        for (int i = bluePlayers; i < blueTeamPlayers.Length; i++)
        {
            blueTeamPlayers[i].gameObject.SetActive(false);
        }

        for (int i = orangePlayers; i < orangeTeamPlayers.Length; i++)
        {
            orangeTeamPlayers[i].gameObject.SetActive(false);
        }
    }

    public void FadeIn()
    {
        fadeInRoutine = StartCoroutine(FadeInRoutine());
    }

    public void FadeOut()
    {
        StartCoroutine(FadeOutRoutine());
    }

    public IEnumerator FadeInRoutine()
    {
        lineObject.SetActive(false);
        bluePlayerHolder.SetActive(false);
        orangePlayerHolder.SetActive(false);

        blueTeam.anchoredPosition = new Vector2(-Screen.width, 0);
        blueTeam.DOAnchorPosX(0, 0.3f);

        orangeTeam.anchoredPosition = new Vector2(Screen.width, 0);
        orangeTeam.DOAnchorPosX(0, 0.3f);

        yield return new WaitForSeconds(0.3f);

        lineObject.SetActive(true);
        bluePlayerHolder.SetActive(true);
        orangePlayerHolder.SetActive(true);

        int totalTweens = 0;
        int tweensCompleted = 0;

        foreach (RectTransform player in blueTeamPlayers)
        {
            if (!player.gameObject.activeSelf)
            {
                continue;
            }

            totalTweens++;

            int originalX = (int)player.anchoredPosition.x;
            player.anchoredPosition = new Vector2(-Screen.width-500, player.anchoredPosition.y);
            player.DOAnchorPosX(originalX, 0.25f).onComplete += () => { tweensCompleted++; };
            player.DOPause();
        }

        foreach (RectTransform player in orangeTeamPlayers)
        {
            if (!player.gameObject.activeSelf)
            {
                continue;
            }

            totalTweens++;

            int originalX = (int)player.anchoredPosition.x;
            player.anchoredPosition = new Vector2(Screen.width+500, player.anchoredPosition.y);
            player.DOAnchorPosX(originalX, 0.25f).onComplete += () => { tweensCompleted++; };
            player.DOPause();
        }

        foreach (RectTransform player in blueTeamPlayers)
        {
            if (!player.gameObject.activeSelf)
            {
                continue;
            }

            player.DOPlay();

            yield return new WaitForSeconds(0.07f);
        }

        foreach (RectTransform player in orangeTeamPlayers)
        {
            if (!player.gameObject.activeSelf)
            {
                continue;
            }

            player.DOPlay();

            yield return new WaitForSeconds(0.07f);
        }

        while (tweensCompleted < totalTweens)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        fadeInRoutine = null;
    }

    private IEnumerator FadeOutRoutine()
    {
        while (fadeInRoutine != null)
        {
            yield return null;
        }

        blueTeam.DOAnchorPosX(-Screen.width, 0.3f);
        orangeTeam.DOAnchorPosX(Screen.width, 0.3f);

        lineObject.SetActive(false);

        yield return new WaitForSeconds(0.3f);

        gameObject.SetActive(false);
    }
}

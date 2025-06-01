using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UI.ThreeDimensional;
using UnityEngine;
using UnityEngine.UI;

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
    private Coroutine fadeOutRoutine;

    private CanvasScaler canvasScaler;

    private Dictionary<int, string> playerAnimationsBlueMale = new Dictionary<int, string>()
    {
        {0, "ani_body02_40040_lob_male" },
        {1, "ani_obpos5idle_40040_lob_male" },
        {2, "ani_body01_40350_lob_male" },
        {3, "ani_win05idle_40350_lob_male" },
        {4, "ani_win03idle_40350_lob_male" },
    };

    private Dictionary<int, string> playerAnimationsOrangeMale = new Dictionary<int, string>()
    {
        {0, "ani_body03_40040_lob_male" },
        {1, "ani_win03idle_40350_lob_male" },
        {2, "ani_body15_40350_lob_male" },
        {3, "ani_pose9_40350_lob_male" },
        {4, "ani_body02_40040_lob_male" },
    };

    private Dictionary<int, string> playerAnimationsBlueFemale = new Dictionary<int, string>()
    {
        {0, "ani_body02_50350_lob_female" },
        {1, "ani_body03_50424_lob_female" },
        {2, "ani_win03idle_50040_lob_female" },
        {3, "ani_win07idle_lob_female" },
        {4, "ani_win05idle_50350_lob_female" },
    };

    private Dictionary<int, string> playerAnimationsOrangeFemale = new Dictionary<int, string>()
    {
        {0, "ani_ready01_lob_female" },
        {1, "ani_win05idle_50350_lob_female" },
        {2, "ani_ready03_lob_female" },
        {3, "ani_pose9_05350_lob_female" },
        {4, "ani_body02_50350_lob_female" },
    };

    private void Start()
    {
        canvasScaler = GetComponentInParent<CanvasScaler>();

        blueTeamObjects = new UIObject3D[blueTeamPlayers.Length];
        orangeTeamObjects = new UIObject3D[orangeTeamPlayers.Length];

        for (int i = 0; i < blueTeamPlayers.Length; i++)
        {
            blueTeamObjects[i] = blueTeamPlayers[i].GetComponentInChildren<UIObject3D>();
        }

        for (int i = 0; i < orangeTeamPlayers.Length; i++)
        {
            orangeTeamObjects[i] = orangeTeamPlayers[i].GetComponentInChildren<UIObject3D>();
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

        StartCoroutine(InitializePlayerModels());
    }

    public void FadeIn()
    {
        if (fadeInRoutine != null)
        {
            return;
        }
        fadeInRoutine = StartCoroutine(FadeInRoutine());
    }

    public void FadeOut()
    {
        if (fadeOutRoutine != null)
        {
            return;
        }
        fadeOutRoutine = StartCoroutine(FadeOutRoutine());
    }

    public IEnumerator FadeInRoutine()
    {
        lineObject.SetActive(false);
        bluePlayerHolder.SetActive(false);
        orangePlayerHolder.SetActive(false);

        blueTeam.anchoredPosition = new Vector2(-canvasScaler.referenceResolution.x, 0);
        blueTeam.DOAnchorPosX(0, 0.3f);

        orangeTeam.anchoredPosition = new Vector2(canvasScaler.referenceResolution.x, 0);
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

        blueTeam.DOAnchorPosX(-canvasScaler.referenceResolution.x, 0.3f);
        orangeTeam.DOAnchorPosX(canvasScaler.referenceResolution.x, 0.3f);

        lineObject.SetActive(false);

        yield return new WaitForSeconds(0.3f);

        fadeOutRoutine = null;
        gameObject.SetActive(false);
    }

    private IEnumerator InitializePlayerModels()
    {
        Unity.Services.Lobbies.Models.Player[] blueTeamPlayers = LobbyController.Instance.GetTeamPlayers(Team.Blue);
        Unity.Services.Lobbies.Models.Player[] orangeTeamPlayers = LobbyController.Instance.GetTeamPlayers(Team.Orange);

        for (int i = 0; i < blueTeamPlayers.Length; i++)
        {
            TrainerModel trainerModel = PlayerClothesPreloader.Instance.GetPlayerModel(blueTeamPlayers[i].Id).GetComponent<TrainerModel>();

            trainerModel.ActiveAnimator.Play(trainerModel.IsMale ? playerAnimationsBlueMale[i] : playerAnimationsBlueFemale[i]);

            blueTeamObjects[i].ObjectPrefab = trainerModel.transform;
        }

        for (int i = 0; i < orangeTeamPlayers.Length; i++)
        {
            TrainerModel trainerModel = PlayerClothesPreloader.Instance.GetPlayerModel(orangeTeamPlayers[i].Id).GetComponent<TrainerModel>();

            trainerModel.ActiveAnimator.Play(trainerModel.IsMale ? playerAnimationsOrangeMale[i] : playerAnimationsOrangeFemale[i]);

            orangeTeamObjects[i].ObjectPrefab = trainerModel.transform;
        }

        yield return null;

        for (int i = 0; i < blueTeamPlayers.Length; i++)
        {
            blueTeamObjects[i].Render();
        }

        for (int i = 0; i < orangeTeamPlayers.Length; i++)
        {
            orangeTeamObjects[i].Render();
        }
    }
}

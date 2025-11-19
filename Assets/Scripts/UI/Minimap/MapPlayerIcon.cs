using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapPlayerIcon : MonoBehaviour
{
    [SerializeField] private Image pokemonIcon, background, scoreCircle;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Sprite blueBG, orangeBG;

    private MinimapIcon minimapIcon;
    private PlayerManager player;

    private bool isTeammate = false;

    public MinimapIcon MinimapIcon => minimapIcon;

    public void Initialize(PlayerManager player)
    {
        minimapIcon = GetComponent<MinimapIcon>();
        this.player = player;
        pokemonIcon.sprite = player.Pokemon.Portrait;
        StartCoroutine(UpdateBackgroundSprite());
        minimapIcon.SetTarget(player.transform);

        player.Pokemon.OnDeath += (info) => { HideIcon(); };
        player.OnRespawn += () => {
            if (isTeammate)
            {
                ShowIcon();
            }
        };
        Team localPlayerTeam = LobbyController.Instance.GetLocalPlayerTeam();
        isTeammate = player.CurrentTeam.IsOnSameTeam(localPlayerTeam);
        if (!isTeammate)
        {
            player.Vision.OnVisibilityChanged += SetVisibility;
            SetVisibility(false);
        }
        player.Pokemon.OnEvolution += () => { pokemonIcon.sprite = player.Pokemon.Portrait; };
    }

    private IEnumerator UpdateBackgroundSprite()
    {
        yield return new WaitForSeconds(0.2f);
        background.sprite = player.CurrentTeam.IsOnSameTeam(LobbyController.Instance.GetLocalPlayerTeam()) ? blueBG : orangeBG;
    }

    private void Update()
    {
        if (player == null) return;

        if (player.CurrentEnergy >= 40)
        {
            scoreCircle.fillAmount = player.ScoreGuageValue;
            scoreText.text = player.CurrentEnergy.ToString();
        }
        else
        {
            scoreCircle.fillAmount = 0;
            scoreText.text = "";
        }
    }

    public void HideIcon()
    {
        pokemonIcon.gameObject.SetActive(false);
        background.gameObject.SetActive(false);
        scoreCircle.gameObject.SetActive(false);
        scoreText.gameObject.SetActive(false);
    }

    public void ShowIcon()
    {
        pokemonIcon.gameObject.SetActive(true);
        background.gameObject.SetActive(true);
        scoreCircle.gameObject.SetActive(true);
        scoreText.gameObject.SetActive(true);
    }

    public void SetVisibility(bool isVisible)
    {
        pokemonIcon.gameObject.SetActive(isVisible);
        background.gameObject.SetActive(isVisible);
        scoreCircle.gameObject.SetActive(isVisible);
        scoreText.gameObject.SetActive(isVisible);
    }
}

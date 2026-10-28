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
    private Pokemon source;
    private Vision sourceVision;
    private int displayedEnergy = -1;

    private bool isTeammate = false;

    public MinimapIcon MinimapIcon => minimapIcon;

    public void Initialize(PlayerManager player)
    {
        minimapIcon = GetComponent<MinimapIcon>();
        this.player = player;
        source = player.Pokemon;
        sourceVision = player.Vision;
        scoreText.text = "";
        minimapIcon.SetTarget(player.transform);

        source.OnHpChange += RefreshVisibility;
        source.OnEvolution += RefreshPresentation;
        source.OnPokemonInitialized += RefreshPresentation;
        source.OnTeamChange += OnTeamChanged;
        sourceVision.OnVisibilityChanged += OnVisionChanged;
        RefreshPresentation();
    }

    private void OnDestroy()
    {
        if (source != null)
        {
            source.OnHpChange -= RefreshVisibility;
            source.OnEvolution -= RefreshPresentation;
            source.OnPokemonInitialized -= RefreshPresentation;
            source.OnTeamChange -= OnTeamChanged;
        }
        if (sourceVision != null) sourceVision.OnVisibilityChanged -= OnVisionChanged;
    }

    private void OnTeamChanged(TeamMember _) => RefreshPresentation();
    private void OnVisionChanged(bool _) => RefreshVisibility();

    private void RefreshPresentation()
    {
        if (player == null || source == null || LobbyController.Instance == null) return;
        isTeammate = player.CurrentTeam.IsOnSameTeam(LobbyController.Instance.GetLocalPlayerTeam());
        background.sprite = isTeammate ? blueBG : orangeBG;
        if (source.BaseStats != null) pokemonIcon.sprite = source.Portrait;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (player == null || player.Pokemon == null) { SetVisibility(false); return; }
        SetVisibility(isTeammate || player.Vision.IsRendered);
    }

    private void Update()
    {
        if (player == null || player.Pokemon == null) { Destroy(gameObject); return; }

        if (player.CurrentEnergy >= 40)
        {
            scoreCircle.fillAmount = player.ScoreGuageValue;
            if (displayedEnergy != player.CurrentEnergy) scoreText.text = player.CurrentEnergy.ToString();
        }
        else
        {
            scoreCircle.fillAmount = 0;
            if (displayedEnergy >= 40) scoreText.text = "";
        }
        displayedEnergy = player.CurrentEnergy;
    }

    public void HideIcon()
    {
        SetVisibility(false);
    }

    public void ShowIcon()
    {
        SetVisibility(true);
    }

    public void SetVisibility(bool isVisible)
    {
        // Vision can still update while a player is KO'd.
        isVisible = isVisible && player != null && player.Pokemon != null && player.Pokemon.CurrentHp > 0;
        pokemonIcon.gameObject.SetActive(isVisible);
        background.gameObject.SetActive(isVisible);
        scoreCircle.gameObject.SetActive(isVisible);
        scoreText.gameObject.SetActive(isVisible);
    }
}

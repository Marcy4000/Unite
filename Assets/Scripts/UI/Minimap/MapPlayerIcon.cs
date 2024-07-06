using UnityEngine;
using UnityEngine.UI;

public class MapPlayerIcon : MonoBehaviour
{
    [SerializeField] private Image pokemonIcon, background;
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
        background.sprite = player.OrangeTeam ? orangeBG : blueBG;
        minimapIcon.SetTarget(player.transform);

        player.Pokemon.OnDeath += (info) => { HideIcon(); };
        player.onRespawn += () => {
            if (isTeammate)
            {
                ShowIcon();
            }
        };
        bool localPlayerTeam = LobbyController.Instance.Player.Data["PlayerTeam"].Value == "Orange";
        isTeammate = player.OrangeTeam == localPlayerTeam;
        if (!isTeammate)
        {
            player.Vision.onVisibilityChanged += SetVisibility;
            SetVisibility(false);
        }
        player.Pokemon.OnEvolution += () => { pokemonIcon.sprite = player.Pokemon.Portrait; };
    }

    public void HideIcon()
    {
        pokemonIcon.gameObject.SetActive(false);
        background.gameObject.SetActive(false);
    }

    public void ShowIcon()
    {
        pokemonIcon.gameObject.SetActive(true);
        background.gameObject.SetActive(true);
    }

    public void SetVisibility(bool isVisible)
    {
        pokemonIcon.gameObject.SetActive(isVisible);
        background.gameObject.SetActive(isVisible);
    }
}

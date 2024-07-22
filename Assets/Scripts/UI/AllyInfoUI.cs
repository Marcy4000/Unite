using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AllyInfoUI : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private Image hpBar;
    [SerializeField] private GameObject deathImage;
    [SerializeField] private TMP_Text deathTimer;

    private PlayerManager allyPokemon;

    public void InitializeUI(PlayerManager player)
    {
        allyPokemon = player;
        portrait.sprite = player.Pokemon.Portrait;
        hpBar.fillAmount = (float)player.Pokemon.CurrentHp / player.Pokemon.GetMaxHp();
        player.Pokemon.OnHpOrShieldChange += UpdateHpBar;
        player.Pokemon.OnEvolution += UpdatePortrait;
        player.Pokemon.OnDeath += OnDeath;
        player.OnRespawn += OnRespawn;
        if (GameManager.Instance.TryGetPlayerNetworkManager(player.OwnerClientId, out PlayerNetworkManager playerNetworkManager))
        {
            playerNetworkManager.OnDeathTimerChanged += UpdateDeathTimer;
        }

        deathImage.SetActive(false);
    }

    private void UpdateDeathTimer(float time)
    {
        deathTimer.text = Mathf.RoundToInt(time).ToString();
    }

    private void OnDeath(DamageInfo info)
    {
        deathImage.SetActive(true);
    }

    private void OnRespawn()
    {
        deathImage.SetActive(false);
    }

    public void UpdateHpBar()
    {
        hpBar.fillAmount = (float)allyPokemon.Pokemon.CurrentHp / allyPokemon.Pokemon.GetMaxHp();
    }

    public void UpdatePortrait()
    {
        portrait.sprite = allyPokemon.Pokemon.Portrait;
    }
}

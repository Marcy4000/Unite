using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AllyInfoUI : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private Image hpBar;
    [SerializeField] private GameObject deathImage;
    [SerializeField] private TMP_Text deathTimer;
    [SerializeField] private GameObject uniteReadyDot;

    private PlayerManager allyPokemon;
    private Pokemon subscribedPokemon;
    private int shownSeconds = -1;

    public void InitializeUI(PlayerManager player)
    {
        Unsubscribe();
        allyPokemon = player;
        shownSeconds = -1;
        if (player == null || player.Pokemon == null) return;
        subscribedPokemon = player.Pokemon;
        subscribedPokemon.OnPokemonInitialized += RefreshPokemon;
        subscribedPokemon.OnHpChange += UpdateHpBar;
        subscribedPokemon.OnEvolution += UpdatePortrait;
        subscribedPokemon.OnDeath += OnDeath;
        player.OnRespawn += OnRespawn;
        if (player.MovesController != null && player.MovesController.IsSpawned) player.MovesController.RequestUniteReadyRpc();
        RefreshPokemon();
        Update();
    }

    private void RefreshPokemon()
    {
        UpdatePortrait();
        UpdateHpBar();
    }

    private void Update()
    {
        if (allyPokemon == null) return;
        bool dead = allyPokemon.PlayerState == PlayerState.Dead;
        deathImage.SetActive(dead);
        if (uniteReadyDot != null) uniteReadyDot.SetActive(!dead && allyPokemon.MovesController != null && allyPokemon.MovesController.UniteIsReady);
        if (dead)
        {
            int seconds = Mathf.CeilToInt(allyPokemon.DeathTimer);
            if (seconds != shownSeconds) { shownSeconds = seconds; deathTimer.SetText(seconds.ToString()); }
        }
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
        if (subscribedPokemon == null || subscribedPokemon.BaseStats == null) { hpBar.fillAmount = 0; return; }
        int maxHp = subscribedPokemon.GetMaxHp();
        hpBar.fillAmount = maxHp > 0 ? Mathf.Clamp01((float)subscribedPokemon.CurrentHp / maxHp) : 0;
    }

    public void UpdatePortrait()
    {
        portrait.sprite = subscribedPokemon != null ? subscribedPokemon.Portrait : null;
        portrait.enabled = portrait.sprite != null;
    }

    private void Unsubscribe()
    {
        if (subscribedPokemon != null)
        {
            subscribedPokemon.OnPokemonInitialized -= RefreshPokemon;
            subscribedPokemon.OnHpChange -= UpdateHpBar;
            subscribedPokemon.OnEvolution -= UpdatePortrait;
            subscribedPokemon.OnDeath -= OnDeath;
        }
        if (allyPokemon != null) allyPokemon.OnRespawn -= OnRespawn;
        subscribedPokemon = null;
    }

    private void OnDestroy() => Unsubscribe();
}

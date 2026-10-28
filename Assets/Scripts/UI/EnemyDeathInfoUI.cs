using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LayoutElement))]
public class EnemyDeathInfoUI : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text deathTimer;
    [SerializeField] private GameObject holder;

    private PlayerManager allyPokemon;
    private Pokemon subscribedPokemon;
    private int shownSeconds = -1;

    private LayoutElement layoutElement;

    private void Awake()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (layoutElement == null) layoutElement = GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = !visible;
        holder.SetActive(visible);
    }

    public void InitializeUI(PlayerManager player)
    {
        Unsubscribe();
        allyPokemon = player;
        shownSeconds = -1;
        SetVisible(false);
        if (player == null || player.Pokemon == null) return;
        subscribedPokemon = player.Pokemon;
        subscribedPokemon.OnPokemonInitialized += UpdatePortrait;
        subscribedPokemon.OnEvolution += UpdatePortrait;
        subscribedPokemon.OnDeath += OnDeath;
        player.OnRespawn += OnRespawn;
        UpdatePortrait();
        Update();
    }

    private void Update()
    {
        if (allyPokemon == null) return;
        bool dead = allyPokemon.PlayerState == PlayerState.Dead;
        SetVisible(dead);
        if (dead)
        {
            int seconds = Mathf.CeilToInt(allyPokemon.DeathTimer);
            if (seconds != shownSeconds) { shownSeconds = seconds; deathTimer.SetText(seconds.ToString()); }
        }
    }

    private void OnDeath(DamageInfo info)
    {
        SetVisible(true);
    }

    private void OnRespawn()
    {
        SetVisible(false);
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
            subscribedPokemon.OnPokemonInitialized -= UpdatePortrait;
            subscribedPokemon.OnEvolution -= UpdatePortrait;
            subscribedPokemon.OnDeath -= OnDeath;
        }
        if (allyPokemon != null) allyPokemon.OnRespawn -= OnRespawn;
        subscribedPokemon = null;
    }

    private void OnDestroy() => Unsubscribe();
}

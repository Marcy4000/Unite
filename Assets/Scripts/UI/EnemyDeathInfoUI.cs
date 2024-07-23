using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyDeathInfoUI : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text deathTimer;
    [SerializeField] private GameObject holder;

    private PlayerManager allyPokemon;

    public void InitializeUI(PlayerManager player)
    {
        allyPokemon = player;
        portrait.sprite = player.Pokemon.Portrait;
        player.Pokemon.OnEvolution += UpdatePortrait;
        player.Pokemon.OnDeath += OnDeath;
        player.OnRespawn += OnRespawn;
        if (GameManager.Instance.TryGetPlayerNetworkManager(player.OwnerClientId, out PlayerNetworkManager playerNetworkManager))
        {
            playerNetworkManager.OnDeathTimerChanged += UpdateDeathTimer;
        }

        holder.SetActive(false);
    }

    private void UpdateDeathTimer(float time)
    {
        deathTimer.text = Mathf.RoundToInt(time).ToString();
    }

    private void OnDeath(DamageInfo info)
    {
        holder.SetActive(true);
    }

    private void OnRespawn()
    {
        holder.SetActive(false);
    }

    public void UpdatePortrait()
    {
        portrait.sprite = allyPokemon.Pokemon.Portrait;
    }
}

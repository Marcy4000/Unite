using System.Collections;
using UnityEngine;

public class FocusBand : HeldItemBase
{
    private float cooldown;
    private int recoveryGeneration;

    public override void Initialize(PlayerManager controller)
    {
        base.Initialize(controller);
        playerManager.Pokemon.OnHpChange += OnHpOrShieldChange;
        playerManager.Pokemon.OnDeath += OnDeath;
    }

    private void OnHpOrShieldChange()
    {
        if (cooldown > 0f || playerManager.Pokemon.CurrentHp <= 0)
        {
            return;
        }

        if (playerManager.Pokemon.CurrentHp < Mathf.FloorToInt(playerManager.Pokemon.GetMaxHp() * 0.25f))
        {
            cooldown = 80f;
            playerManager.StartCoroutine(RecoverHP(++recoveryGeneration));
        }
    }

    private void OnDeath(DamageInfo info) => recoveryGeneration++;

    private IEnumerator RecoverHP(int generation)
    {
        for (int i = 0; i < 3; i++)
        {
            if (generation != recoveryGeneration || playerManager == null
                || !playerManager.Pokemon.IsSpawned || playerManager.Pokemon.CurrentHp <= 0) yield break;
            playerManager.Pokemon.HealDamageRPC(Mathf.RoundToInt(playerManager.Pokemon.GetMaxHp() * 0.12f));
            yield return new WaitForSeconds(1f);
        }
    }

    public override void Update()
    {
        if (cooldown > 0f)
        {
            cooldown -= Time.deltaTime;
        }
    }

    public override void Reset()
    {
        recoveryGeneration++;
    }
}

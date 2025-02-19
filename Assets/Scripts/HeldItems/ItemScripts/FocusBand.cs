using System.Collections;
using UnityEngine;

public class FocusBand : HeldItemBase
{
    private float cooldown;

    public override void Initialize(PlayerManager controller)
    {
        base.Initialize(controller);
        playerManager.Pokemon.OnHpChange += OnHpOrShieldChange;
    }

    private void OnHpOrShieldChange()
    {
        if (cooldown > 0f)
        {
            return;
        }

        if (playerManager.Pokemon.CurrentHp < Mathf.FloorToInt(playerManager.Pokemon.GetMaxHp() * 0.25f))
        {
            playerManager.StartCoroutine(RecoverHP());
            cooldown = 80f;
        }
    }

    private IEnumerator RecoverHP()
    {
        for (int i = 0; i < 3; i++)
        {
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
        // Nothing
    }
}

using UnityEngine;

public class Potion : BattleItemBase
{
    private float healAmount = 0.20f;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        wasUseSuccessful = false;

        Cooldown = 30f;
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }
    }

    public override void Finish()
    {
        if (IsActive)
        {
            wasUseSuccessful = true;
            playerManager.Pokemon.HealDamageRPC(Mathf.RoundToInt((playerManager.Pokemon.GetMaxHp() * healAmount) + 160f));
            SpatialAudioManager.Instance.PlaySpatialSound(DefaultAudioSounds.Play_Props_Potion_ZhiLiao, playerManager.Vision, AudioAudibility.Everyone, playerManager);
        }
        base.Finish();
    }
}

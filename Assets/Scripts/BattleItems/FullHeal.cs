public class FullHeal : BattleItemBase
{
    StatusEffect statusEffect = new StatusEffect(StatusType.Unstoppable, 1.5f, true, 0);

    public FullHeal()
    {
        Name = "Full Heal";
        Cooldown = 40;
    }

    public override void Update()
    {
        
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.Pokemon.ClearStatusEffectsRPC();
            playerManager.Pokemon.AddStatusEffect(statusEffect);
            SpatialAudioManager.Instance.PlaySpatialSound(DefaultAudioSounds.Play_Props_FullHeal_JingHua, playerManager.Vision, AudioAudibility.Everyone, playerManager);
            wasUseSuccessful = true;
        }
        base.Finish();
    }
}

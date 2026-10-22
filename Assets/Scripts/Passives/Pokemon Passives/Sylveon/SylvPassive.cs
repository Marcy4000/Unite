using UnityEngine;

public class SylvPassive : PassiveBase
{
    private int stacks;
    private float expires;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        controller.Pokemon.OnDamageTaken += OnDamage;
        controller.Pokemon.OnDamageDealt += OnDealt;
        controller.Pokemon.OnDeath += OnDeath;
        controller.Pokemon.OnEvolution += Apply;
    }
    private void OnDealt(ulong target, DamageInfo damage) { OnDamage(damage); }
    private void OnDamage(DamageInfo damage)
    {
        if (playerManager.Pokemon.CurrentHp <= 0 || damage.proprieties.HasFlag(DamageProprieties.IsHeal)) return;
        stacks = Mathf.Min(4, (Time.time < expires ? stacks : 0) + 1);
        expires = Time.time + 1.5f;
        Apply();
    }
    private void Apply()
    {
        Pokemon pokemon = playerManager.Pokemon;
        pokemon.RemoveAllStatChangeWithIDRPC(SylvMoveRules.PassiveAttack);
        pokemon.RemoveAllStatChangeWithIDRPC(SylvMoveRules.PassiveDefense);
        if (stacks == 0 || Time.time >= expires) return;
        pokemon.AddStatChange(new StatChange((short)(stacks * 5), Stat.SpAttack, expires - Time.time,
            true, true, true, SylvMoveRules.PassiveAttack));
        if (pokemon.CurrentLevel >= 3)
            pokemon.AddStatChange(new StatChange((short)(stacks * 5), Stat.SpDefense, expires - Time.time,
                true, true, true, SylvMoveRules.PassiveDefense));
    }
    private void OnDeath(DamageInfo damage) { stacks = 0; Apply(); }
    public override void Update()
    {
        if (stacks > 0 && Time.time >= expires) { stacks = 0; Apply(); }
    }
    public override void Dispose()
    {
        if (playerManager == null) return;
        playerManager.Pokemon.OnDamageTaken -= OnDamage;
        playerManager.Pokemon.OnDamageDealt -= OnDealt;
        playerManager.Pokemon.OnDeath -= OnDeath;
        playerManager.Pokemon.OnEvolution -= Apply;
    }
}

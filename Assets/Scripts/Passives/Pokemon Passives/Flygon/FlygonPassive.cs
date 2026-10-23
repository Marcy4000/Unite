using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class FlygonPassive : PassiveBase
{
    private float damageReadyAt;
    private Coroutine extraDamage;
    private bool Evolved => playerManager != null && playerManager.Pokemon.CurrentLevel >= 3;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageReadyAt = Time.time + 3;
        playerManager.Pokemon.OnDamageDealt += OnDamageDealt;
        playerManager.Pokemon.OnStatusChange += OnStatusChange;
        playerManager.Pokemon.OnDeath += OnDeath;
    }
    private void OnDamageDealt(ulong targetId, DamageInfo damage)
    {
        if (Evolved || Time.time < damageReadyAt || playerManager.Pokemon.CurrentHp <= 0
            || (damage.proprieties & DamageProprieties.Unused_2) != 0
            || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var body)
            || !body.TryGetComponent(out Pokemon target) || target.CurrentHp <= 0
            || Vector3.Distance(playerManager.transform.position, target.transform.position) > 4.5f) return;
        damageReadyAt = Time.time + 3;
        int amount = Mathf.FloorToInt(target.CalculateDamage(damage, playerManager.Pokemon) * 0.05f);
        extraDamage = playerManager.StartCoroutine(ExtraDamage(target, amount));
    }
    private IEnumerator ExtraDamage(Pokemon target, int amount)
    {
        yield return new WaitForSeconds(0.5f);
        extraDamage = null;
        if (Evolved || target == null || !target.IsSpawned || target.CurrentHp <= 0 || playerManager.Pokemon.CurrentHp <= 0) yield break;
        target.TakeDamageRPC(new DamageInfo(playerManager.NetworkObjectId, 0, 0, (short)Mathf.Clamp(amount, 0, short.MaxValue), DamageType.True, DamageProprieties.Unused_2));
    }
    private void OnStatusChange(StatusEffect effect, bool added)
    {
        if (!Evolved || !added || !effect.IsTimed) return;
        if (effect.Type == StatusType.Immobilized || effect.Type == StatusType.Asleep || effect.Type == StatusType.Incapacitated
            || effect.Type == StatusType.VisionObscuring || effect.Type == StatusType.Frozen)
            playerManager.Pokemon.UpdateStatusEffectTimeRPC(effect, effect.Duration * 0.7f);
    }
    private void OnDeath(DamageInfo damage)
    {
        if (extraDamage != null) playerManager.StopCoroutine(extraDamage);
        extraDamage = null;
    }
    public override void Update() { }
    public override void Dispose()
    {
        if (playerManager == null) return;
        OnDeath(default);
        playerManager.Pokemon.OnDamageDealt -= OnDamageDealt;
        playerManager.Pokemon.OnStatusChange -= OnStatusChange;
        playerManager.Pokemon.OnDeath -= OnDeath;
    }
}

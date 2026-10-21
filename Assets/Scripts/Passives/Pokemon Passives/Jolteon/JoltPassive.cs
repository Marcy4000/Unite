using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class JoltPassive : PassiveBase
{
    private readonly JoltChargeState charge = new JoltChargeState();
    private readonly Dictionary<ulong, float> nextDamageCharge = new Dictionary<ulong, float>();
    private bool isEvolved;
    private int readyEffect, overchargeEffect;
    private float nextReadyVisual;
    private float chargeBudget, budgetUntil;
    public bool IsPassiveReady => charge.Ready;
    public float PassiveCharge => charge.Amount;
    private readonly StatChange eeveeSpeed = new StatChange(10, Stat.Speed, 0, false, true, true, 9);

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        playerManager.Pokemon.OnEvolution += OnEvolution;
        playerManager.Pokemon.OnDamageDealt += OnDamageDealt;
        playerManager.Pokemon.OnOtherPokemonKilled += OnKO;
        playerManager.Pokemon.OnDeath += OnDeath;
        isEvolved = playerManager.Pokemon.CurrentLevel >= 3;
        if (!isEvolved) playerManager.Pokemon.AddStatChange(eeveeSpeed);
        else OnEvolution();
    }

    private void OnEvolution()
    {
        if (playerManager.Pokemon.CurrentEvolution == null || playerManager.Pokemon.CurrentEvolution.level < 3) return;
        isEvolved = true;
        playerManager.Pokemon.RemoveStatChangeWithIDRPC(eeveeSpeed.ID);
        playerManager.HPBar.ShowGenericGuage(true);
    }

    private bool TryTarget(ulong id, out Pokemon target)
    {
        target = null;
        return NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out var obj)
            && obj.TryGetComponent(out target);
    }

    private void OnDamageDealt(ulong id, DamageInfo damage)
    {
        if (playerManager.PlayerState == PlayerState.Dead || !TryTarget(id, out var target)
            || damage.proprieties.HasFlag(DamageProprieties.IsHeal)) return;
        float now = Time.time;
        // Multi-collider hits and periodic/secondary damage cannot flood the meter.
        // This also keeps one source's DoT from repeatedly rewarding every tick.
        if (nextDamageCharge.TryGetValue(id, out float next) && now < next) return;
        nextDamageCharge[id] = now + (damage.proprieties.HasFlag(JoltMoveRules.PeriodicDamage) ? 1f : 0.25f);
        if (now >= budgetUntil) { chargeBudget = 0; budgetUntil = now + 1f; }
        float amount = Mathf.Min(target.Type == PokemonType.Player ? 8f : 4f, 24f - chargeBudget);
        chargeBudget += amount;
        charge.Add(amount);
    }

    private void OnKO(ulong id)
    {
        if (playerManager.PlayerState != PlayerState.Dead && TryTarget(id, out var target) && target.Type == PokemonType.Player) charge.Add(20f);
    }

    private void OnDeath(DamageInfo damage)
    {
        charge.Clear();
        nextDamageCharge.Clear();
        chargeBudget = 0;
        budgetUntil = 0;
        JoltMoveRuntime.EndEffect(playerManager, ref readyEffect);
        JoltMoveRuntime.EndEffect(playerManager, ref overchargeEffect);
        playerManager.HPBar.UpdateGenericGuageValue(0, 100);
    }

    public override void Update()
    {
        if (!isEvolved || playerManager.PlayerState == PlayerState.Dead) return;
        bool moving = playerManager.PlayerMovement.IsMoving
            || (playerManager.MovesController.GetMove(MoveType.MoveA) as JoltDashMove)?.IsExecuting == true
            || (playerManager.MovesController.GetMove(MoveType.UniteMove) as JolteonUnite)?.IsDashing == true;
        charge.Add((moving ? 6f : 2f) * Time.deltaTime);
        var animator = playerManager.AnimationManager.Animator;
        if (animator != null && Mathf.Abs(charge.Amount / 100f - animator.GetFloat("PassiveAmount")) > 0.03f)
            playerManager.AnimationManager.SetFloat("PassiveAmount", charge.Amount / 100f);
        playerManager.HPBar.UpdateGenericGuageValue(charge.Amount, 100f);
        if (charge.Ready && (readyEffect == 0 || Time.time >= nextReadyVisual))
        {
            JoltMoveRuntime.EndEffect(playerManager, ref readyEffect);
            readyEffect = JoltMoveRuntime.Effect(playerManager, JoltEffect.Ready);
            nextReadyVisual = Time.time + 60f;
        }
        else if (!charge.Ready) JoltMoveRuntime.EndEffect(playerManager, ref readyEffect);
    }

    public bool TryConsumeCharge()
    {
        bool empowered = charge.Consume(Time.time);
        if (!charge.Ready) JoltMoveRuntime.EndEffect(playerManager, ref readyEffect);
        return empowered;
    }

    public void ResetPassiveCharge() { charge.Reduce(100f, Time.time); }
    public void ReducePassiveCharge(float amount) { charge.Reduce(amount, Time.time); }

    public void UniteFillCharge(float duration)
    {
        charge.Overcharge(Time.time, duration);
        JoltMoveRuntime.EndEffect(playerManager, ref overchargeEffect);
        overchargeEffect = JoltMoveRuntime.Effect(playerManager, JoltEffect.Overcharge);
    }
}

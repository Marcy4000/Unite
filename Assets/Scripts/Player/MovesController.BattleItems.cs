using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private bool battleItemPending;
    private double battleItemReadyAt;
    private readonly NetworkVariable<double> smokeExpiresAt = new NetworkVariable<double>();
    private readonly NetworkVariable<Vector3> smokeCenter = new NetworkVariable<Vector3>();
    private GameObject battleSmokeView;
    private Team smokeTeam;
    private readonly HashSet<Pokemon> smokeTargets = new HashSet<Pokemon>();
    private Pokemon tailTarget;
    private double tailExpiresAt;
    private double tailNextProc;
    private bool tailSubscribed;
    private bool battleStasisServerLocked;
    private int battleItemGeneration;

    private BattleItemAsset GetSelectedBattleItem()
    {
        var lobbyPlayer = playerManager != null && LobbyController.Instance != null ? playerManager.LobbyPlayer : null;
        if (CharactersList.Instance == null || lobbyPlayer?.Data == null
            || !lobbyPlayer.Data.TryGetValue("BattleItem", out var selected)
            || !int.TryParse(selected.Value, out int index)) return null;
        return CharactersList.Instance.GetBattleItemByID(index);
    }

    public void SubmitBattleItem(AvailableBattleItems kind, Vector3 direction, ulong targetId = ulong.MaxValue)
    {
        if (!IsOwner || !IsSpawned || battleItemPending) return;
        battleItemPending = true;
        UseBattleItemRPC(kind, direction, targetId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void UseBattleItemRPC(AvailableBattleItems kind, Vector3 direction, ulong targetId)
    {
        var selected = GetSelectedBattleItem();
        double now = NetworkManager.ServerTime.Time;
        bool stunned = pokemon != null && pokemon.HasAnyStatusEffect(new[] { StatusType.Incapacitated, StatusType.Frozen, StatusType.Asleep, StatusType.Bound });
        if (selected == null || selected.battleItemType != kind || BattleItemRules.Cooldown(kind) <= 0
            || pokemon == null || pokemon.CurrentHp <= 0 || !BattleItemRules.Finite(direction)
            || now < battleItemReadyAt || pokemon.HasStatusEffect(StatusType.BattleItemStasis)
            || (kind != AvailableBattleItems.FullHeal && stunned)
            || (GameManager.Instance != null && GameManager.Instance.GameState != GameState.Playing))
        {
            BattleItemResultRPC(false, 0);
            return;
        }
        if (!ApplyBattleItem(kind, direction, targetId, now))
        {
            BattleItemResultRPC(false, 0);
            return;
        }
        float cooldown = BattleItemRules.Cooldown(kind);
        battleItemReadyAt = now + cooldown;
        BattleItemResultRPC(true, cooldown);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void BattleItemResultRPC(bool accepted, float cooldown)
    {
        battleItemPending = false;
        if (!accepted || battleItem == null) return;
        battleItem.wasUseSuccessful = true;
        battleItem.Cooldown = cooldown;
        OnBattleItemOver();
    }

    private bool ApplyBattleItem(AvailableBattleItems kind, Vector3 direction, ulong targetId, double now)
    {
        switch (kind)
        {
            case AvailableBattleItems.Potion:
                pokemon.HealDamageRPC(BattleItemRules.PotionHealing(pokemon.GetMaxHp()));
                break;
            case AvailableBattleItems.FullHeal:
                pokemon.CleanseBattleItemConditions();
                pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, 1.5f, true, BattleItemRules.FullHealId));
                break;
            case AvailableBattleItems.XAttack:
                BuffBattleItem(Stat.Attack, 20, 6.5f, BattleItemRules.AttackId);
                BuffBattleItem(Stat.SpAttack, 20, 6.5f, BattleItemRules.AttackId);
                BuffBattleItem(Stat.AtkSpeed, 25, 6.5f, BattleItemRules.AttackId);
                break;
            case AvailableBattleItems.XSpeed:
                BuffBattleItem(Stat.Speed, 45, 6, BattleItemRules.XSpeedId);
                pokemon.AddStatusEffect(new StatusEffect(StatusType.SpeedReductionImmunity, 6, true, BattleItemRules.XSpeedId));
                break;
            case AvailableBattleItems.GoalGetter:
                playerManager.AddScoreBoostRPC(new ScoreBoost(0, ScoreSpeedFactor.GoalGetter, 10, true));
                break;
            case AvailableBattleItems.Eject:
                direction.y = 0;
                if (direction.sqrMagnitude < 0.0001f || pokemon.HasStatusEffect(StatusType.Immobilized)) return false;
                float distance = PlayerMovement.GetMarshadowDashDistance(direction, BattleItemRules.EjectRange);
                if (distance <= 0.01f) return false;
                Vector3 destination = transform.position + direction.normalized * distance;
                playerManager.UpdatePosAndRotRPC(destination, Quaternion.LookRotation(direction));
                break;
            case AvailableBattleItems.SlowSmoke:
                ClearBattleSmoke();
                smokeCenter.Value = transform.position;
                smokeTeam = playerManager.CurrentTeam.Team;
                smokeExpiresAt.Value = now + 3.5;
                break;
            case AvailableBattleItems.ShedinjaDoll:
                pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, 2, true, BattleItemRules.DollId));
                pokemon.AddStatusEffect(new StatusEffect(StatusType.Untargetable, 2, true, BattleItemRules.DollId));
                pokemon.AddStatusEffect(new StatusEffect(StatusType.BattleItemStasis, 2, true, BattleItemRules.DollId));
                SynchronizeBattleStasis();
                break;
            case AvailableBattleItems.FluffyTail:
                Pokemon target = FindTailTarget(targetId);
                if (target == null) return false;
                ClearBattleTail();
                tailTarget = target;
                tailExpiresAt = now + 3;
                tailNextProc = now;
                target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 3, true, BattleItemRules.TailId).WithSource(NetworkObjectId));
                pokemon.OnHealthDamageDealt += ProcBattleTail;
                tailSubscribed = true;
                break;
            case AvailableBattleItems.GoalHacker:
                GoalZone goal = FindBattleGoal(direction);
                if (goal == null) return false;
                goal.ApplyGoalHacker(playerManager.CurrentTeam.Team);
                break;
            case AvailableBattleItems.Brick:
                SpawnBattleBrick(direction);
                break;
            default: return false;
        }
        PresentBattleItem(kind);
        return true;
    }

    private void BuffBattleItem(Stat stat, short amount, float duration, ushort id)
    {
        pokemon.AddStatChange(new StatChange(amount, stat, duration, true, true, stat != Stat.AtkSpeed, id).WithSource(NetworkObjectId));
    }

    private void SpawnBattleBrick(Vector3 offset)
    {
        int generation = battleItemGeneration;
        offset.y = 0;
        Vector3 origin = transform.position;
        Vector3 destination = origin + Vector3.ClampMagnitude(offset, 5);
        Team team = playerManager.CurrentTeam.Team;
        Addressables.LoadAssetAsync<GameObject>("Assets/Prefabs/Objects/Objects/Brick.prefab").Completed += handle =>
        {
            if (this == null || !IsSpawned || generation != battleItemGeneration || handle.Status != AsyncOperationStatus.Succeeded)
            {
                Addressables.Release(handle);
                return;
            }
            var instance = Instantiate(handle.Result, origin, Quaternion.identity);
            if (!instance.TryGetComponent(out BrickObject brick) || !instance.TryGetComponent(out NetworkObject network))
            {
                Destroy(instance);
                Addressables.Release(handle);
                return;
            }
            brick.HoldAsset(handle);
            network.Spawn();
            brick.InitializeRPC(origin, destination, team, new DamageInfo(NetworkObjectId, 0, 0, 400, DamageType.Physical, DamageProprieties.CanCrit));
        };
    }

    private Pokemon FindTailTarget(ulong targetId)
    {
        Pokemon nearest = null;
        float distance = BattleItemRules.TargetRange * BattleItemRules.TargetRange;
        foreach (var obj in NetworkManager.SpawnManager.SpawnedObjects.Values)
        {
            if (obj.NetworkObjectId != targetId) continue;
            if (!obj.TryGetComponent(out Pokemon target) || target.CurrentHp <= 0 || target.Type == PokemonType.Player
                || target.TeamMember.IsOnSameTeam(playerManager.CurrentTeam) || target.HasStatusEffect(StatusType.Invincible)
                || target.HasStatusEffect(StatusType.Untargetable) || target.HasStatusEffect(StatusType.Invisible)) continue;
            float candidate = FlatBattleDistance(target.transform.position, transform.position);
            if (candidate <= distance) { nearest = target; distance = candidate; }
        }
        return nearest;
    }

    private GoalZone FindBattleGoal(Vector3 direction)
    {
        GoalZone nearest = null;
        float distance = float.PositiveInfinity;
        Vector3 point = transform.position + Vector3.ClampMagnitude(direction, BattleItemRules.TargetRange);
        foreach (var obj in NetworkManager.SpawnManager.SpawnedObjects.Values)
        {
            if (!obj.TryGetComponent(out GoalZone goal) || goal.GoalStatus != GoalStatus.Active || goal.Team == Team.Neutral) continue;
            if (FlatBattleDistance(goal.transform.position, transform.position) > BattleItemRules.TargetRange * BattleItemRules.TargetRange) continue;
            float candidate = FlatBattleDistance(goal.transform.position, point);
            if (candidate <= distance) { nearest = goal; distance = candidate; }
        }
        return nearest;
    }

    private static float FlatBattleDistance(Vector3 first, Vector3 second)
    {
        first.y = second.y;
        return (first - second).sqrMagnitude;
    }

    private void ProcBattleTail(Pokemon target, DamageInfo damage, int healthDamage)
    {
        double now = NetworkManager.ServerTime.Time;
        if (!IsServer || target != tailTarget || target == null || !target.IsSpawned || target.CurrentHp <= 0
            || now >= tailExpiresAt || now < tailNextProc || healthDamage <= 0 || damage.source != DamageSource.Direct) return;
        tailNextProc = now + 0.5;
        int amount = BattleItemRules.TailDamage(pokemon.GetAttack(), pokemon.GetSpAttack(), pokemon.CurrentLevel);
        var extra = new DamageInfo(NetworkObjectId, 0, 0, (short)Mathf.Clamp(amount, 0, short.MaxValue), DamageType.Physical);
        extra.source = DamageSource.Item;
        extra.suppressOnHitEffects = true;
        target.TakeDamageRPC(extra);
    }

    private void UpdateBattleItemRuntime()
    {
        UpdateBattleSmokeView();
        if (battleItem != null && battleItem.IsActive && (pokemon == null || pokemon.CurrentHp <= 0
            || !battleItemStatus.HasStatus(ActionStatusType.None))) battleItem.Cancel();
        if (!IsServer || !IsSpawned) return;
        SynchronizeBattleStasis();
        double now = NetworkManager.ServerTime.Time;
        if (tailSubscribed && (tailTarget == null || !tailTarget.IsSpawned || tailTarget.CurrentHp <= 0 || now >= tailExpiresAt)) ClearBattleTail();
        if (smokeExpiresAt.Value <= now) { ClearBattleSmoke(); return; }
        var occupants = new HashSet<Pokemon>();
        foreach (var obj in NetworkManager.SpawnManager.SpawnedObjects.Values)
        {
            if (!obj.TryGetComponent(out Pokemon target) || target.CurrentHp <= 0
                || target.TeamMember.IsOnSameTeam(smokeTeam) || target.HasStatusEffect(StatusType.Invincible)
                || FlatBattleDistance(target.transform.position, smokeCenter.Value) > BattleItemRules.SmokeRadius * BattleItemRules.SmokeRadius) continue;
            occupants.Add(target);
            smokeTargets.Add(target);
            if (!target.HasBattleItemModifier(BattleItemRules.SmokeId, NetworkObjectId, Stat.Speed))
                target.AddStatChange(new StatChange(80, Stat.Speed, 0, false, false, true, BattleItemRules.SmokeId).WithSource(NetworkObjectId));
            if (!target.HasBattleItemModifier(BattleItemRules.SmokeId, NetworkObjectId, Stat.AtkSpeed))
                target.AddStatChange(new StatChange(50, Stat.AtkSpeed, 0, false, false, false, BattleItemRules.SmokeId).WithSource(NetworkObjectId));
        }
        foreach (var target in new List<Pokemon>(smokeTargets))
            if (!occupants.Contains(target))
            {
                if (target != null && target.IsSpawned) target.RemoveBattleItemModifiers(BattleItemRules.SmokeId, NetworkObjectId);
                smokeTargets.Remove(target);
            }
    }

    private void ClearBattleSmoke()
    {
        foreach (var target in smokeTargets)
            if (IsServer && target != null && target.IsSpawned) target.RemoveBattleItemModifiers(BattleItemRules.SmokeId, NetworkObjectId);
        smokeTargets.Clear();
        if (IsServer && IsSpawned) smokeExpiresAt.Value = 0;
    }

    private void UpdateBattleSmokeView()
    {
        if (!IsClient || !IsSpawned) return;
        float remaining = (float)(smokeExpiresAt.Value - NetworkManager.ServerTime.Time);
        if (remaining <= 0)
        {
            if (battleSmokeView != null) Destroy(battleSmokeView);
            battleSmokeView = null;
            return;
        }
        if (battleSmokeView == null)
            battleSmokeView = TimedCombatVisual.Spawn("Effects/Zoroark/IllusionSmoke", smokeCenter.Value, Vector3.forward,
                new Vector3(BattleItemRules.SmokeRadius, 1, BattleItemRules.SmokeRadius), remaining);
    }

    private void ClearBattleTail()
    {
        if (tailSubscribed && pokemon != null) pokemon.OnHealthDamageDealt -= ProcBattleTail;
        tailSubscribed = false;
        tailTarget = null;
    }

    private void DisposeBattleItemRuntime()
    {
        battleItemGeneration++;
        battleItem?.Cancel();
        battleItemPending = false;
        ClearBattleTail();
        ClearBattleSmoke();
        if (battleSmokeView != null) Destroy(battleSmokeView);
        battleSmokeView = null;
        if (battleStasisServerLocked) ServerActionLocks = Math.Max(0, ServerActionLocks - 1);
        battleStasisServerLocked = false;
    }

    private void SynchronizeBattleStasis()
    {
        bool locked = pokemon != null && pokemon.CurrentHp > 0 && pokemon.HasStatusEffect(StatusType.BattleItemStasis);
        if (locked == battleStasisServerLocked) return;
        ServerActionLocks = Math.Max(0, ServerActionLocks + (locked ? 1 : -1));
        battleStasisServerLocked = locked;
    }

    private void PresentBattleItem(AvailableBattleItems kind)
    {
        if (SpatialAudioManager.Instance == null || playerManager == null) return;
        DefaultAudioSounds sound;
        switch (kind)
        {
            case AvailableBattleItems.Eject: sound = DefaultAudioSounds.Play_Props_EjectButton_ShanXian; break;
            case AvailableBattleItems.Potion: sound = DefaultAudioSounds.Play_Props_Potion_ZhiLiao; break;
            case AvailableBattleItems.XSpeed: sound = DefaultAudioSounds.Play_Props_XSpeed_JiPao; break;
            case AvailableBattleItems.XAttack: sound = DefaultAudioSounds.Play_Props_XAttack_KuangBao; break;
            case AvailableBattleItems.FullHeal: sound = DefaultAudioSounds.Play_Props_FullHeal_JingHua; break;
            case AvailableBattleItems.GoalGetter: sound = DefaultAudioSounds.Play_Props_JiaSuZhuangZhi; break;
            default: return;
        }
        SpatialAudioManager.Instance.PlaySpatialSound(sound, playerManager.Vision, AudioAudibility.Everyone, playerManager);
    }
}

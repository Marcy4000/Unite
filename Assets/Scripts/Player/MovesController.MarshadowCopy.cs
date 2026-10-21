using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private MoveAsset marshadowCopyUI;
    public void SpendMarshadowUnite()
    {
        DiscardMarshadowUnite();
        onMovePerformed?.Invoke(uniteMove);
    }
    public void DiscardMarshadowUnite() { uniteMoveCharge = 0; uniteMoveStatus.AddStatus(ActionStatusType.Cooldown); }
    public void InstallMarshadowCopy(MoveAsset source)
    {
        if (!IsOwner || !IsLivingMarshadow || source == null || !MarshadowCopyRules.Supports(source.move)) return;
        uniteMove = new MarshadowCopiedUnite(source) { playerManager = playerManager };
        // Keep Mirror's energy denominator. A borrowed move is a single earned
        // use, not a way to change the next Mirror Shadow's charge requirement.
        uniteMoveCharge = uniteMoveMaxCharge; uniteMoveStatus.RemoveStatus(ActionStatusType.Cooldown);
        if (marshadowCopyUI != null) Destroy(marshadowCopyUI);
        marshadowCopyUI = Instantiate(source);
        marshadowCopyUI.moveName = "Shadow Copy: " + source.moveName;
        marshadowCopyUI.description = MarshadowCopyRules.Description(source.move);
        BattleUIManager.instance.InitializeMoveUI(marshadowCopyUI, uniteMove);
        BattleUIManager.instance.ShowUniteMoveSecondaryCooldown(15f);
        OnMoveLearnedRPC(source.move);
    }
    public void RestoreMarshadowUnite(MarshadowCopiedUnite copy)
    {
        if (!IsOwner || uniteMove != copy) return;
        LearnMove(pokemon.BaseStats.GetUniteMove());
        if (marshadowCopyUI != null) Destroy(marshadowCopyUI);
        marshadowCopyUI = null;
        BattleUIManager.instance.ShowUniteMoveSecondaryCooldown(0);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CastMarshadowCopyRPC(AvailableMoves source, int id, Vector3 origin, Vector3 direction, ulong targetId)
    {
        if (!IsLivingMarshadow || !MarshadowCopyRules.Supports(source) || (origin - transform.position).sqrMagnitude > 100f) return;
        var cast = ServerCast(id);
        if (cast.CopyStarted || cast.Cancelled) return;
        cast.CopyStarted = true;
        if (source == AvailableMoves.DuraludonUnite) { StartCoroutine(SpawnMarshadowRuin(id, origin, direction)); return; }
        if (source == AvailableMoves.EmboarUnite)
        {
            ApplyMarshadowCopyBuff(Stat.Attack, 20, true, 10f, 62100);
            ApplyMarshadowCopyBuff(Stat.AtkSpeed, 30, false, 10f, 62101);
            ApplyMarshadowCopyBuff(Stat.Cdr, 30, false, 10f, 62102);
            PresentMarshadowEffectRPC(MarshadowEffect.MirrorShadow, -MarshadowMoveRules.NextCast(), origin, direction, 1f, 0, 3);
            return;
        }
        if (source == AvailableMoves.BlazingBycicleKick)
        {
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj) && obj.TryGetComponent(out Pokemon target)
                && MarshadowMoveRuntime.CanHit(playerManager, target) && (target.transform.position - origin).sqrMagnitude <= 81f)
                LaunchHomingProjectileRpc(targetId, new DamageInfo(NetworkObjectId, 2.47f, 14, 670, DamageType.Physical, DamageProprieties.IsUniteMove));
            return;
        }
        float radius = source == AvailableMoves.FlareonUnite ? 2f : MarshadowCopyRules.Radius(source);
        foreach (Pokemon target in MarshadowMoveRuntime.Sweep(playerManager, origin, origin, radius))
        {
            DamageInfo damage = source == AvailableMoves.FlareonUnite
                ? new DamageInfo(NetworkObjectId, 2.5f, 7, (short)(510 + Mathf.Min(1500, target.GetMissingHp() * 0.05f)), DamageType.Physical, DamageProprieties.IsUniteMove)
                : source == AvailableMoves.VaporeonUnite ? new DamageInfo(NetworkObjectId, 0.92f, 9, 640, DamageType.Physical, DamageProprieties.IsUniteMove)
                : source == AvailableMoves.GlaceonUnite ? new DamageInfo(NetworkObjectId, 1f, 7, 320, DamageType.Physical, DamageProprieties.IsUniteMove)
                : new DamageInfo(NetworkObjectId, 1.3f, 13, 750, DamageType.Physical, DamageProprieties.IsUniteMove);
            target.TakeDamageRPC(damage);
            if (source == AvailableMoves.FlareonUnite) target.AddStatusEffect(new StatusEffect(StatusType.Burned, 3f, true, 62103));
            if (source == AvailableMoves.VaporeonUnite)
            {
                target.AddStatChange(new StatChange(35, Stat.Defense, 5f, true, false, true, 62104));
                target.AddStatChange(new StatChange(35, Stat.SpDefense, 5f, true, false, true, 62105));
            }
        }
        PresentMarshadowEffectRPC(MarshadowEffect.EchoBurst, -MarshadowMoveRules.NextCast(), origin, direction, 0.5f, 0, (int)radius);
        if (source == AvailableMoves.SylveonUnite)
        {
            ApplyMarshadowCopyBuff(Stat.LifeSteal, 50, false, 6f, 62106);
            ApplyMarshadowCopyBuff(Stat.SpellVamp, 50, false, 6f, 62107);
        }
        if (source == AvailableMoves.FlareonUnite) pokemon.AddShieldRPC(new ShieldInfo(Mathf.RoundToInt(pokemon.GetMaxHp() * 0.12f), 62108, 0, 3f, true));
        if (source == AvailableMoves.VaporeonUnite)
        {
            var allies = new System.Collections.Generic.HashSet<Pokemon>();
            foreach (Collider collider in Physics.OverlapSphere(origin + Vector3.up * 0.7f, radius))
                if (collider.TryGetComponent(out Pokemon ally) && ally.CurrentHp > 0 && ally.TeamMember.IsOnSameTeam(playerManager.CurrentTeam) && allies.Add(ally))
                    ally.HealDamageRPC(Mathf.RoundToInt(pokemon.GetAttack() * 2.6f + 5 * pokemon.CurrentLevel + 840));
        }
        if (source == AvailableMoves.GlaceonUnite) StartCoroutine(MarshadowIceField(cast, origin, direction));
    }
    private void ApplyMarshadowCopyBuff(Stat stat, short amount, bool percentage, float duration, ushort id)
    {
        pokemon.RemoveAllStatChangeWithIDRPC(id);
        pokemon.AddStatChange(new StatChange(amount, stat, duration, true, true, percentage, id));
    }
    private IEnumerator MarshadowIceField(MarshadowServerCast cast, Vector3 origin, Vector3 direction)
    {
        bool cancelled = false;
        Action<DamageInfo> death = _ => cancelled = true;
        pokemon.OnDeath += death;
        int visual = -MarshadowMoveRules.NextCast();
        PresentMarshadowEffectRPC(MarshadowEffect.EchoField, visual, origin, direction, 5f, 0, 0);
        try
        {
            for (int i = 0; i < 10 && !cancelled && !cast.Cancelled && IsSpawned && pokemon.CurrentHp > 0; i++)
            {
                foreach (Pokemon target in MarshadowMoveRuntime.Sweep(playerManager, origin, origin, 5f)) SlowMarshadowTarget(target, 30, 0.65f);
                yield return new WaitForSeconds(0.5f);
            }
        }
        finally { if (pokemon != null) pokemon.OnDeath -= death; if (IsSpawned) StopMarshadowEffectRPC(visual); }
    }
    private IEnumerator SpawnMarshadowRuin(int id, Vector3 origin, Vector3 direction)
    {
        var request = ServerCast(id);
        bool cancelled = false;
        Action<DamageInfo> death = _ => cancelled = true;
        pokemon.OnDeath += death;
        var handle = Addressables.LoadAssetAsync<GameObject>("Assets/Prefabs/Objects/Moves/Duraludon/DuraludonUniteObject.prefab");
        yield return handle;
        pokemon.OnDeath -= death;
        if (cancelled || request.Cancelled || !IsSpawned || pokemon.CurrentHp <= 0 || handle.Status != AsyncOperationStatus.Succeeded)
        {
            Addressables.Release(handle);
            if (IsSpawned) MarshadowCopyReadyRPC(id, false);
            yield break;
        }
        GameObject go = Instantiate(handle.Result, origin, Quaternion.identity);
        var ruin = go.GetComponent<DuraludonUniteObject>();
        ruin.HoldAsset(handle); request.Instance = go.GetComponent<NetworkObject>(); request.Instance.Spawn(true);
        ruin.InitializeRPC(NetworkObjectId, origin, direction, 0, false, origin);
        MarshadowCopyReadyRPC(id, true);
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void MarshadowCopyReadyRPC(int id, bool success)
    {
        if (uniteMove is MarshadowCopiedUnite copy) copy.CopyReady(id, success);
    }
}

using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private GlaceonRuntime glaceonRuntime;
    private int glaceRequestSerial;
    public int NextGlaceRequest() => ++glaceRequestSerial;
    private readonly NetworkVariable<int> glaceCrystals = new NetworkVariable<int>();
    private readonly NetworkVariable<double> glaceCrystalExpiry = new NetworkVariable<double>();
    private readonly NetworkVariable<int> glaceCharge = new NetworkVariable<int>();
    private readonly NetworkVariable<int> glaceMode = new NetworkVariable<int>();
    private readonly NetworkVariable<double> glaceSpearNextTick = new NetworkVariable<double>();
    private readonly NetworkVariable<int> glaceUniteEnergy = new NetworkVariable<int>();
    private double displayedGlaceSpearTick;
    public bool GlaceSpearActive => glaceSpearNextTick.Value > 0;
    public float GlaceSpearTickRemaining => NetworkManager == null ? 0 : Mathf.Max(0, (float)(glaceSpearNextTick.Value - NetworkManager.ServerTime.Time));
    public void SyncGlaceSpearTick(double nextTick)
    {
        if (IsServer && IsSpawned) glaceSpearNextTick.Value = nextTick;
    }
    private void SyncGlaceUniteEnergy()
    {
        SyncTrevenantEnergy();
        SyncLucarioEnergy();
        if (IsServer && IsSpawned && IsGlaceon) glaceUniteEnergy.Value = Mathf.FloorToInt(legacyItemEnergy);
    }
    private void UpdateGlaceonPresentation()
    {
        if (!IsOwner) return;
        if (uniteMoveMaxCharge > 0)
        {
            uniteMoveCharge = glaceUniteEnergy.Value;
            if (uniteMoveCharge >= uniteMoveMaxCharge)
            {
                if (uniteMoveStatus.HasStatus(ActionStatusType.Cooldown)) uniteMoveStatus.RemoveStatus(ActionStatusType.Cooldown);
            }
            else if (!uniteMoveStatus.HasStatus(ActionStatusType.Cooldown)) uniteMoveStatus.AddStatus(ActionStatusType.Cooldown);
        }
        if (!playerManager.IsLocalHuman || BattleUIManager.instance == null || displayedGlaceSpearTick == glaceSpearNextTick.Value) return;
        displayedGlaceSpearTick = glaceSpearNextTick.Value;
        BattleUIManager.instance.ShowMoveSecondaryCooldown(0, GlaceSpearTickRemaining);
    }
    private IciclesHolder glaceHolder;
    private bool glaceHolderLoading;
    private int glaceHolderGeneration;
    private double glaceHolderRetry;
    private readonly double[] glaceReservedCooldown = new double[3];
    private bool IsGlaceon => pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.GlaceonPassive;
    public int GlaceCrystalCount => glaceCrystals.Value;
    public int GlaceAttackCharge => glaceCharge.Value;
    public int GlaceAttackMode => glaceMode.Value;
    public float GlaceCrystalRemaining => NetworkManager == null ? 0 : Mathf.Max(0, (float)(glaceCrystalExpiry.Value - NetworkManager.ServerTime.Time));
    public GlaceonRuntime GlaceCombat => IsServer && IsGlaceon ? glaceonRuntime ?? (glaceonRuntime = new GlaceonRuntime(playerManager)) : null;
    public bool HasGlaceMove(AvailableMoves move) => prefabLearnedMoves[0] == move || prefabLearnedMoves[1] == move || prefabLearnedMoves[2] == move;
    public void SyncGlaceState(int crystals, double expiry, int charge, int mode)
    {
        if (!IsServer || !IsSpawned) return;
        glaceCrystals.Value = crystals; glaceCrystalExpiry.Value = expiry; glaceCharge.Value = charge; glaceMode.Value = mode;
    }
    private void UpdateGlaceonRuntime()
    {
        if (!IsSpawned) return;
        if (!IsGlaceon) { if (glaceonRuntime != null) DisposeGlaceonRuntime(); return; }
        UpdateGlaceonPresentation();
        if (!IsServer) return;
        GlaceCombat.Tick();
        if (pokemon.CurrentLevel < 3 || glaceHolder != null || glaceHolderLoading || NetworkManager.ServerTime.Time < glaceHolderRetry) return;
        glaceHolderRetry = NetworkManager.ServerTime.Time + 1;
        glaceHolderLoading = true;
        int generation = glaceHolderGeneration;
        Addressables.LoadAssetAsync<GameObject>("Assets/Prefabs/Objects/Passives/Glaceon/IciclesHolder.prefab").Completed += asset =>
        {
            if (this == null || !IsSpawned || !IsGlaceon || generation != glaceHolderGeneration || asset.Status != AsyncOperationStatus.Succeeded)
            { Addressables.Release(asset); if (this != null && generation == glaceHolderGeneration) glaceHolderLoading = false; return; }
            glaceHolderLoading = false;
            var root = Instantiate(asset.Result, transform.position, Quaternion.identity);
            glaceHolder = root.GetComponent<IciclesHolder>();
            if (glaceHolder == null) { Destroy(root); Addressables.Release(asset); return; }
            glaceHolder.HoldAsset(asset);
            root.GetComponent<NetworkObject>().Spawn(true);
            glaceHolder.Bind(NetworkObjectId);
        };
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void GlaceSpearInputRPC(Vector3 direction, bool released, int request = 0)
    {
        if (!IsGlaceon || !CombatTargeting.Finite(direction) || !HasGlaceMove(AvailableMoves.GlaceIcycleSpear)
            || request != prefabOwnerRequests[0]) return;
        GlaceCombat.SetSpearInput(direction, released);
    }
    public void LaunchGlaceProjectile(Pokemon target, GlaceonShot shot, bool upgraded = false, GlaceonWindVolley volley = null)
    {
        if (!IsServer || !GlaceonMoveRules.CanHit(playerManager, target)) return;
        var runtime = GlaceCombat;
        if (runtime == null) return;
        int generation = runtime.Generation;
        Addressables.LoadAssetAsync<GameObject>(GlaceonMoveRules.Folder + "GlaceProjectileEffect.prefab").Completed += asset =>
        {
            if (this == null || !IsSpawned || !IsGlaceon || glaceonRuntime != runtime || runtime.Generation != generation
                || !GlaceonMoveRules.Living(pokemon) || !GlaceonMoveRules.CanHit(playerManager, target) || asset.Status != AsyncOperationStatus.Succeeded)
            { Addressables.Release(asset); return; }
            var root = Instantiate(asset.Result, transform.position + Vector3.up * 0.8f, Quaternion.identity);
            var effect = root.GetComponent<GlaceProjectileEffect>();
            if (effect == null) { Destroy(root); Addressables.Release(asset); return; }
            effect.Shot = shot; effect.Volley = volley; effect.Generation = generation;
            effect.HoldAsset(asset);
            root.GetComponent<NetworkObject>().Spawn(true);
            effect.Initialize(playerManager, transform.forward, target, upgraded);
        };
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void GlaceCastAcceptedRPC(AvailableMoves move, float cooldown, int request)
    {
        var definition = MoveDatabase.GetMove(move) as GlaceonMove;
        if (definition == null) return;
        if (uniteMove is GlaceonMove unite && unite.MoveId == move && unite.MatchesExecutionRequest(request)) unite.AcceptExecution();
        for (int slot = 0; slot < moves.Length; slot++)
        {
            if (!(moves[slot] is GlaceonMove runtime) || runtime.MoveId != move || !runtime.MatchesExecutionRequest(request)) continue;
            runtime.AcceptExecution();
            if (cooldown > 0)
            {
                moveStatuses[slot].Cooldown = cooldown;
                moveStatuses[slot].AddStatus(ActionStatusType.Cooldown); UpdateMoveUI(slot);
            }
        }
    }
    private void DisposeGlaceonRuntime()
    {
        glaceHolderGeneration++; glaceHolderLoading = false;
        if (IsOwner)
        {
            foreach (var move in moves) if (move is GlaceonMove) move.ResetMove();
            if (uniteMove is GlaceonMove) uniteMove.ResetMove();
        }
        glaceonRuntime?.Dispose(); glaceonRuntime = null;
        if (IsServer && glaceHolder != null && glaceHolder.IsSpawned) glaceHolder.NetworkObject.Despawn(true);
        glaceHolder = null;
    }
}

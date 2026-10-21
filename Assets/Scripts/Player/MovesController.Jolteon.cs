using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private readonly Dictionary<int, JoltEffectRequest> jolteonEffects = new Dictionary<int, JoltEffectRequest>();

    // Isolated from onObjectSpawned: Charge/Web may overlap without consuming
    // Swift's legacy callback, or initializing another move's object by mistake.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void SpawnJolteonEffectRPC(JoltEffect effect, int cast, Vector3 origin, Vector3 direction, bool empowered, bool upgraded, bool second)
    {
        if (pokemon == null || pokemon.CurrentHp <= 0 || pokemon.BaseStats.Passive != AvailablePassives.JolteonPassive
            || !Enum.IsDefined(typeof(JoltEffect), effect) || effect == JoltEffect.WebMark || jolteonEffects.ContainsKey(cast)) return;
        var request = new JoltEffectRequest(playerManager, () => jolteonEffects.Remove(cast));
        jolteonEffects.Add(cast, request);
        request.Load(effect, origin, direction, empowered, upgraded, second);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void EndJolteonEffectRPC(int cast)
    {
        if (jolteonEffects.TryGetValue(cast, out var request)) request.Cancel();
    }
}

// Server-side lifetime of one asynchronous cast, including the pending load.
// Death invalidates it even if the actor respawns before Addressables completes.
public sealed class JoltEffectRequest
{
    private readonly PlayerManager actor;
    private Action completed;
    private bool cancelled;
    private JoltEffectObject instance;
    private Pokemon markedTarget;

    public JoltEffectRequest(PlayerManager actor, Action completed)
    {
        this.actor = actor;
        this.completed = completed;
        actor.Pokemon.OnDeath += OnDeath;
    }

    public void Load(JoltEffect effect, Vector3 origin, Vector3 direction, bool empowered, bool upgraded, bool second, ulong target = 0)
    {
        if (effect == JoltEffect.WebMark)
        {
            if (!actor.NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(target, out var obj)
                || !obj.TryGetComponent(out markedTarget) || markedTarget.CurrentHp <= 0)
            {
                Complete();
                return;
            }
            markedTarget.OnDeath += OnDeath;
        }
        Addressables.LoadAssetAsync<GameObject>(JoltMoveRules.EffectPath(effect)).Completed += handle =>
        {
            if (handle.Status != AsyncOperationStatus.Succeeded || cancelled || actor == null || !actor.IsSpawned || actor.Pokemon.CurrentHp <= 0)
            {
                if (handle.Status != AsyncOperationStatus.Succeeded) Debug.LogError($"Failed to load Jolteon {effect}: {handle.OperationException}");
                Addressables.Release(handle);
                Complete();
                return;
            }
            GameObject go = UnityEngine.Object.Instantiate(handle.Result, origin, Quaternion.identity);
            instance = go.GetComponent<JoltEffectObject>();
            if (instance == null)
            {
                UnityEngine.Object.Destroy(go);
                Addressables.Release(handle);
                Complete();
                return;
            }
            instance.HoldAsset(handle, Complete);
            go.GetComponent<NetworkObject>().Spawn(true);
            instance.InitializeRPC(actor.NetworkObjectId, origin, direction, empowered, upgraded, second, target);
        };
    }

    private void OnDeath(DamageInfo damage) { Cancel(); }
    public void Cancel()
    {
        cancelled = true;
        if (instance != null && instance.IsSpawned) instance.NetworkObject.Despawn(true);
        Complete();
    }
    private void Complete()
    {
        if (actor != null) actor.Pokemon.OnDeath -= OnDeath;
        if (markedTarget != null) markedTarget.OnDeath -= OnDeath;
        Action callback = completed;
        completed = null;
        callback?.Invoke();
    }
}

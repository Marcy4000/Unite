using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Server-side lifetime of one asynchronous cast, including the pending load.
// Death invalidates it even if the actor respawns before Addressables completes.
public sealed class JoltEffectRequest
{
    private readonly PlayerManager actor;
    private Action completed;
    private bool cancelled;
    private JoltEffectObject instance;
    private Pokemon markedTarget;
    private readonly JolteonCharacterRuntime runtime;

    public JoltEffectRequest(PlayerManager actor, Action completed)
    {
        this.actor = actor;
        this.completed = completed;
        actor.Pokemon.OnDeath += OnDeath;
        runtime = actor.MovesController.Combat?.Character as JolteonCharacterRuntime;
        runtime?.TrackEffectRequest(this);
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
            Spawn(handle, origin, direction, empowered, upgraded, second, target);
        };
    }

    public bool LoadPrepared(JoltEffect effect, Vector3 origin, Vector3 direction, bool empowered, bool upgraded, bool second)
    {
        var handle = Addressables.LoadAssetAsync<GameObject>(JoltMoveRules.EffectPath(effect));
        if (!handle.IsDone || handle.Status != AsyncOperationStatus.Succeeded || cancelled)
        {
            Addressables.Release(handle);
            Complete();
            return false;
        }
        return Spawn(handle, origin, direction, empowered, upgraded, second, 0);
    }

    private bool Spawn(AsyncOperationHandle<GameObject> handle, Vector3 origin, Vector3 direction, bool empowered, bool upgraded, bool second, ulong target)
    {
        if (cancelled || actor == null || !actor.IsSpawned || actor.Pokemon.CurrentHp <= 0)
        {
            Addressables.Release(handle);
            Complete();
            return false;
        }
        GameObject go = UnityEngine.Object.Instantiate(handle.Result, origin, Quaternion.identity);
        instance = go.GetComponent<JoltEffectObject>();
        NetworkObject network = go.GetComponent<NetworkObject>();
        if (instance == null || network == null)
        {
            UnityEngine.Object.Destroy(go);
            Addressables.Release(handle);
            Complete();
            return false;
        }
        instance.HoldAsset(handle, Complete);
        network.Spawn(true);
        instance.InitializeServer(actor.NetworkObjectId, origin, direction, empowered, upgraded, second, target);
        return instance != null && instance.IsSpawned;
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
        runtime?.ForgetEffectRequest(this);
        Action callback = completed;
        completed = null;
        callback?.Invoke();
    }
}

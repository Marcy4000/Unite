using System;
using System.Collections;
using Unite.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class CombatEffectSpawner
{
    private sealed class AssetLease : IDisposable
    {
        private AsyncOperationHandle<GameObject> handle;
        private bool disposed;
        public AssetLease(AsyncOperationHandle<GameObject> handle) { this.handle = handle; }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (handle.IsValid()) Addressables.Release(handle);
        }
    }

    public IEnumerator Load(string path, CombatScope scope, Action<GameObject> loaded)
    {
        if (string.IsNullOrEmpty(path) || scope.IsDisposed) yield break;
        AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(path);
        scope.Own(new AssetLease(handle));
        while (!scope.IsDisposed && handle.IsValid() && !handle.IsDone) yield return null;
        if (scope.IsDisposed || !handle.IsValid()) yield break;
        if (handle.Status == AsyncOperationStatus.Succeeded) loaded(handle.Result);
        else Debug.LogError($"Combat asset preparation failed: {path}: {handle.OperationException}");
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, CombatScope scope, bool despawnWithScope = true)
    {
        if (prefab == null || scope.IsDisposed || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return null;
        GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
        if (!instance.TryGetComponent(out NetworkObject networkObject))
        {
            UnityEngine.Object.Destroy(instance);
            throw new InvalidOperationException($"Combat prefab {prefab.name} has no NetworkObject.");
        }
        networkObject.Spawn(true);
        if (!networkObject.IsSpawned)
        {
            UnityEngine.Object.Destroy(instance);
            return null;
        }
        if (despawnWithScope) scope.Own(new CombatCallbackLease(() =>
        {
            if (networkObject != null && networkObject.IsSpawned) networkObject.Despawn(true);
            else if (instance != null) UnityEngine.Object.Destroy(instance);
        }));
        return instance;
    }
}

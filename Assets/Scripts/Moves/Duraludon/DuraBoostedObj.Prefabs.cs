using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public enum DuraMoveEffect { LaserFocus, DragonPulse, DragonTail, StealthRock, RevolvingRuin }

public static class DuraMovePrefabPaths
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/Duraludon/";
    public static string Get(DuraMoveEffect effect)
    {
        switch (effect)
        {
            case DuraMoveEffect.LaserFocus: return Folder + "DuraLaserFocusObject.prefab";
            case DuraMoveEffect.DragonPulse: return Folder + "DuraDragonPulseObject.prefab";
            case DuraMoveEffect.DragonTail: return Folder + "DuraDragonTailObject.prefab";
            case DuraMoveEffect.StealthRock: return Folder + "DuraStealthRockObject.prefab";
            case DuraMoveEffect.RevolvingRuin: return Folder + "DuraludonUniteObject.prefab";
            default: throw new System.ArgumentOutOfRangeException(nameof(effect));
        }
    }
}

public partial class DuraBoostedObj
{
    private int prefabGeneration;

    // Same Addressables -> NetworkObject -> InitializeRPC flow as MovesController.
    // Initialize directly on the server so simultaneous casts cannot consume
    // one another's shared onObjectSpawned callbacks.
    private void SpawnMovePrefab(DuraMoveEffect effect, Vector3 direction, float power = 0, bool upgraded = false, Vector3 destination = default)
    {
        if (!IsServer || !initialized || duraludon.Pokemon.CurrentHp <= 0) return;
        int generation = prefabGeneration;
        Vector3 origin = duraludon.transform.position;
        string path = DuraMovePrefabPaths.Get(effect);
        Addressables.LoadAssetAsync<GameObject>(path).Completed += handle =>
        {
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"Failed to load Duraludon move prefab {path}: {handle.OperationException}");
                Addressables.Release(handle);
                return;
            }
            if (this == null || !IsSpawned || generation != prefabGeneration || duraludon == null || duraludon.Pokemon.CurrentHp <= 0)
            {
                Addressables.Release(handle);
                return;
            }
            GameObject instance = Instantiate(handle.Result, origin, Quaternion.identity);
            var move = instance.GetComponent<DuraMoveObject>();
            if (move == null)
            {
                Debug.LogError($"Duraludon move prefab {path} has no DuraMoveObject component.");
                Destroy(instance);
                Addressables.Release(handle);
                return;
            }
            move.HoldAsset(handle);
            instance.GetComponent<NetworkObject>().Spawn(true);
            move.InitializeRPC(duraludon.NetworkObjectId, origin, direction, power, upgraded, destination);
        };
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void LaserFocusRPC()
    {
        if (!initialized) return;
        SpawnMovePrefab(DuraMoveEffect.LaserFocus, duraludon.transform.forward);
    }
}

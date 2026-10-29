using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void SpawnPsyduckRaceItemRPC(AvailableMoves move)
    {
        if (playerManager == null || !playerManager.IsSpawned || RaceManager.Instance == null
            || !RaceManager.Instance.PlayerLapCounters.TryGetValue(playerManager.NetworkObjectId, out var racer)
            || racer == null || racer.RaceFinished) return;

        string path;
        switch (move)
        {
            case AvailableMoves.PsyduckRaceBarrier: path = "PsyduckBarrier"; break;
            case AvailableMoves.PsyduckRaceBubble: path = "PsyduckRaceBubble"; break;
            case AvailableMoves.PsyduckRaceShield: path = "PsyduckRaceShield"; break;
            case AvailableMoves.PsyduckRaceConfuseRay: path = "PsyduckRaceConfuseRay"; break;
            case AvailableMoves.PsyduckRaceFreezeRay: path = "PsyduckRaceFreezeRay"; break;
            default: return;
        }

        ulong target = 0;
        if (move == AvailableMoves.PsyduckRaceConfuseRay || move == AvailableMoves.PsyduckRaceFreezeRay)
        {
            var next = RaceManager.Instance.GetNextPlayer(playerManager.NetworkObjectId);
            if (next == null) return;
            target = next.AssignedPlayerID;
        }

        Vector3 origin = playerManager.transform.position;
        Quaternion rotation = playerManager.transform.rotation;
        int count = move == AvailableMoves.PsyduckRaceBubble ? 3 : 1;
        for (int index = 0; index < count; index++)
        {
            Vector3 offset = index == 0 ? Vector3.forward * 6.5f
                : Vector3.forward * 5.5f + Vector3.right * (index == 1 ? 1 : -1) * Mathf.Tan(50 * Mathf.Deg2Rad) * 5.5f;
            Vector3 endpoint = origin + rotation * offset;
            LoadPsyduckRaceItem("Assets/Prefabs/Objects/Moves/Psyduck/" + path + ".prefab", origin, rotation, endpoint, target);
        }
    }

    private void LoadPsyduckRaceItem(string path, Vector3 origin, Quaternion rotation, Vector3 endpoint, ulong target)
    {
        RaceManager race = RaceManager.Instance;
        Addressables.LoadAssetAsync<GameObject>(path).Completed += handle =>
        {
            if (handle.Status != AsyncOperationStatus.Succeeded || this == null || !IsSpawned
                || playerManager == null || !playerManager.IsSpawned || race == null || RaceManager.Instance != race
                || !RaceManager.Instance.PlayerLapCounters.TryGetValue(playerManager.NetworkObjectId, out var racer)
                || racer == null || racer.RaceFinished)
            {
                if (handle.Status != AsyncOperationStatus.Succeeded) Debug.LogError($"Failed to load racing item: {handle.OperationException}");
                Addressables.Release(handle);
                return;
            }

            GameObject instance = Instantiate(handle.Result, origin, rotation);
            instance.AddComponent<PsyduckRaceEffectLifetime>().Hold(handle);
            instance.GetComponent<NetworkObject>().Spawn(true);
            if (instance.TryGetComponent(out PsyduckRaceBarrierWall wall))
                wall.InitializeRPC(origin, rotation.eulerAngles, playerManager.NetworkObjectId);
            else if (instance.TryGetComponent(out PsyduckRaceBubbleObject bubble))
                bubble.InitializeRPC(playerManager.NetworkObjectId, origin, endpoint);
            else if (instance.TryGetComponent(out PsyduckRacingShieldObject shield))
                shield.Initialize(playerManager.NetworkObjectId);
            else if (instance.TryGetComponent(out PsyduckRaceHoamingProjectile projectile))
                projectile.SetTargetRPC(target, playerManager.NetworkObjectId, origin);
        };
    }
}

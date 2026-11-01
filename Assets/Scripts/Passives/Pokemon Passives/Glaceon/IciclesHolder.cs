using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class IciclesHolder : NetworkBehaviour
{
    [SerializeField] private GameObject iciclePrefab;
    [SerializeField] private float radius = 5f;
    [SerializeField] private float yOffset = 0.75f;
    private readonly NetworkVariable<byte> count = new NetworkVariable<byte>();
    private readonly NetworkVariable<ulong> actorId = new NetworkVariable<ulong>(ulong.MaxValue);
    private readonly List<GameObject> spawnedIcicles = new List<GameObject>();
    public byte IciclesCount => count.Value;

    public override void OnNetworkSpawn()
    {
        count.OnValueChanged += CountChanged;
        Reconcile(count.Value);
    }
    public void Follow(ulong id) { if (IsServer) actorId.Value = id; }
    public void SetCount(byte value) { if (IsServer) count.Value = GlaceonCharacterRuntime.ClampCrystals(value); }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    public void AddIcicleRPC() => SetCount((byte)Mathf.Min(count.Value + 1, 8));
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    public void RemoveIcicleRPC() => SetCount((byte)Mathf.Max(count.Value - 1, 0));
    private void CountChanged(byte previous, byte current) => Reconcile(current);
    private void LateUpdate()
    {
        if (IsSpawned && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(actorId.Value, out var actor))
            transform.position = actor.transform.position;
    }
    private void Reconcile(byte value)
    {
        int desired = Mathf.Clamp(value, 0, 8);
        if (iciclePrefab == null) desired = 0;
        while (spawnedIcicles.Count > desired)
        {
            int index = spawnedIcicles.Count - 1;
            if (spawnedIcicles[index] != null) Destroy(spawnedIcicles[index]);
            spawnedIcicles.RemoveAt(index);
        }
        while (spawnedIcicles.Count < desired) spawnedIcicles.Add(Instantiate(iciclePrefab, transform));
        for (int index = 0; index < desired; index++)
        {
            float radians = index * 2 * Mathf.PI / desired;
            spawnedIcicles[index].transform.localPosition = new Vector3(Mathf.Cos(radians) * radius, yOffset, Mathf.Sin(radians) * radius);
        }
    }
    public override void OnNetworkDespawn()
    {
        count.OnValueChanged -= CountChanged;
        Reconcile(0);
        base.OnNetworkDespawn();
    }
}

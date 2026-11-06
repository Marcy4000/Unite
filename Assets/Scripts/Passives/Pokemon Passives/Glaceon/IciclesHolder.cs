using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Snapshot presentation reads the actor's replicated resources, including on late join.
public class IciclesHolder : NetworkBehaviour
{
    [SerializeField] private GameObject iciclePrefab;
    [SerializeField] private float radius = 5;
    [SerializeField] private float yOffset = 0.75f;
    private readonly NetworkVariable<ulong> actorId = new NetworkVariable<ulong>(ulong.MaxValue);
    private readonly List<GameObject> spawnedIcicles = new List<GameObject>();
    private AsyncOperationHandle<GameObject> asset;
    private bool holdsAsset;
    public void HoldAsset(AsyncOperationHandle<GameObject> handle) { asset = handle; holdsAsset = true; }
    public void Bind(ulong actor) { if (IsServer) actorId.Value = actor; }
    private void Update()
    {
        if (!IsSpawned || !IsClient || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(actorId.Value, out var root)
            || !root.TryGetComponent(out PlayerManager actor)) return;
        transform.position = actor.transform.position;
        int desired = actor.Pokemon.CurrentHp > 0 ? actor.MovesController.GlaceCrystalCount : 0;
        while (spawnedIcicles.Count > desired) { int last = spawnedIcicles.Count - 1; Destroy(spawnedIcicles[last]); spawnedIcicles.RemoveAt(last); }
        while (spawnedIcicles.Count < desired && iciclePrefab != null) spawnedIcicles.Add(Instantiate(iciclePrefab, transform));
        bool visible = actor.Vision == null || actor.Vision.IsRendered;
        for (int i = 0; i < spawnedIcicles.Count; i++)
        {
            spawnedIcicles[i].SetActive(visible);
            float angle = i * Mathf.PI * 2 / spawnedIcicles.Count;
            spawnedIcicles[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, yOffset, Mathf.Sin(angle) * radius);
        }
    }
    public override void OnNetworkDespawn()
    {
        foreach (var icicle in spawnedIcicles) if (icicle != null) Destroy(icicle);
        spawnedIcicles.Clear(); base.OnNetworkDespawn();
    }
    public override void OnDestroy() { if (holdsAsset && asset.IsValid()) Addressables.Release(asset); holdsAsset = false; base.OnDestroy(); }
}

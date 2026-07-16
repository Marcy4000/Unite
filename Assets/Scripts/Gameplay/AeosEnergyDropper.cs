using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public struct AeosEnergyDropSettings
{
    public string ResourcePath;
    public float ScatterRadius;
    public int BigEnergyValue;

    public static AeosEnergyDropSettings Default => new AeosEnergyDropSettings
    {
        ResourcePath = "Assets/Prefabs/Objects/Objects/AeosEnergy.prefab",
        ScatterRadius = 1f,
        BigEnergyValue = 5
    };
}

public static class AeosEnergyDropper
{
    public static void SpawnDrops(Vector3 origin, int amount, AeosEnergyDropSettings settings)
    {
        if (amount <= 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.ResourcePath))
        {
            settings.ResourcePath = AeosEnergyDropSettings.Default.ResourcePath;
        }

        if (settings.BigEnergyValue <= 0)
        {
            settings.BigEnergyValue = AeosEnergyDropSettings.Default.BigEnergyValue;
        }

        if (settings.ScatterRadius < 0f)
        {
            settings.ScatterRadius = 0f;
        }

        int bigDrops = amount / settings.BigEnergyValue;
        int smallDrops = amount % settings.BigEnergyValue;

        AsyncOperationHandle<GameObject> loadHandle = Addressables.LoadAssetAsync<GameObject>(settings.ResourcePath);
        loadHandle.Completed += handle =>
        {
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"Failed to load addressable: {handle.OperationException}");
                return;
            }

            GameObject prefab = handle.Result;

            for (int i = 0; i < bigDrops; i++)
            {
                SpawnSingleDrop(prefab, origin, true, settings.ScatterRadius);
            }

            for (int i = 0; i < smallDrops; i++)
            {
                SpawnSingleDrop(prefab, origin, false, settings.ScatterRadius);
            }

            Addressables.Release(handle);
        };
    }

    private static void SpawnSingleDrop(GameObject prefab, Vector3 origin, bool isBig, float scatterRadius)
    {
        Vector3 offset = new Vector3(Random.Range(-scatterRadius, scatterRadius), 0f, Random.Range(-scatterRadius, scatterRadius));
        GameObject spawnedObject = Object.Instantiate(prefab, origin + offset, Quaternion.identity);

        AeosEnergy aeosEnergy = spawnedObject.GetComponent<AeosEnergy>();
        if (aeosEnergy != null)
        {
            aeosEnergy.LocalBigEnergy = isBig;
        }

        NetworkObject networkObject = spawnedObject.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn(true);
        }
    }
}
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class IciclesHolder : NetworkBehaviour
{
    [SerializeField] private GameObject iciclePrefab;
    [SerializeField] private float radius = 5f;
    [SerializeField] private float yOffset = 0.75f;

    private byte iciclesCount;
    private List<GameObject> spawnedIcicles = new List<GameObject>();

    public byte IciclesCount => iciclesCount;

    [Rpc(SendTo.ClientsAndHost)]
    public void AddIcicleRPC()
    {
        iciclesCount++;
        GameObject newIcicle = Instantiate(iciclePrefab, transform);
        spawnedIcicles.Add(newIcicle);
        UpdateIciclePositions();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void RemoveIcicleRPC()
    {
        if (iciclesCount == 0)
        {
            return;
        }

        iciclesCount--;
        GameObject icicleToRemove = spawnedIcicles[iciclesCount];
        spawnedIcicles.RemoveAt(iciclesCount);
        Destroy(icicleToRemove);
        UpdateIciclePositions();
    }

    private void UpdateIciclePositions()
    {
        float angleStep = 360f / iciclesCount;

        for (int i = 0; i < iciclesCount; i++)
        {
            float angle = i * angleStep;
            float radians = angle * Mathf.Deg2Rad;

            Vector3 position = new Vector3(
                Mathf.Cos(radians) * radius,
                yOffset,
                Mathf.Sin(radians) * radius
            );

            spawnedIcicles[i].transform.localPosition = position;
        }
    }
}

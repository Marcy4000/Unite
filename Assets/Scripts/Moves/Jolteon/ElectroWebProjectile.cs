using Unity.Netcode;

public class ElectroWebProjectile : NetworkBehaviour
{
    private void Update()
    {
        if (IsServer && IsSpawned) NetworkObject.Despawn(true);
    }
}

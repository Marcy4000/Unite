using Unity.Netcode;

public class ElectroWebMark : NetworkBehaviour
{
    private void Update()
    {
        if (IsServer && IsSpawned) NetworkObject.Despawn(true);
    }
}

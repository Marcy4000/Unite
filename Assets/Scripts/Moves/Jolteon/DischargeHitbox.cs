using Unity.Netcode;

public class DischargeHitbox : NetworkBehaviour
{
    private void Update()
    {
        if (IsServer && IsSpawned) NetworkObject.Despawn(true);
    }
}

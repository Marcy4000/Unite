using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class PsyduckRaceEffectLifetime : MonoBehaviour
{
    private AsyncOperationHandle<GameObject> asset;

    public void Hold(AsyncOperationHandle<GameObject> handle) { asset = handle; }

    private void OnDestroy()
    {
        if (asset.IsValid()) Addressables.Release(asset);
    }
}

public static class PsyduckRaceHit
{
    public static bool Apply(PlayerManager player, StatusEffect effect)
    {
        if (player == null || player.Pokemon == null || !player.IsServer || !player.IsSpawned) return false;
        if (player.Pokemon.HasStatusEffect(StatusType.Invincible) || player.Pokemon.HasStatusEffect(StatusType.Unstoppable)) return false;
        if (!PsyduckRacingShieldObject.TryBlock(player)) player.Pokemon.AddStatusEffect(effect);
        return true;
    }

    public static void PrepareTrigger(GameObject instance)
    {
        var body = instance.GetComponent<Rigidbody>();
        if (body == null) body = instance.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }
}

using System.Collections;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerMovement
{
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void DisplaceRPC(Vector3 direction, float distance, float duration)
    {
        if ((!IsOwner && !IsServer) || !CombatTargeting.Finite(direction) || !float.IsFinite(distance) || !float.IsFinite(duration)
            || distance <= 0 || duration <= 0 || pokemon.CurrentHp <= 0 || !CanBeKnockedBack
            || pokemon.HasStatusEffect(StatusType.Unstoppable) || pokemon.HasStatusEffect(StatusType.Invincible)
            || pokemon.HasStatusEffect(StatusType.HindranceResistance)) return;
        direction.y = 0;
        if (direction.sqrMagnitude > 0.001f) StartCoroutine(Displace(direction.normalized, distance, duration));
    }

    private IEnumerator Displace(Vector3 direction, float distance, float duration)
    {
        var actor = GetComponent<PlayerManager>();
        bool cancelled = false;
        System.Action<DamageInfo> onDeath = _ => cancelled = true;
        pokemon.OnDeath += onDeath;
        try
        {
            using (var actionLock = new CombatActionLock(actor, true))
            {
                float remaining = distance, speed = distance / duration;
                while (!cancelled && remaining > 0.001f && pokemon.CurrentHp > 0 && !pokemon.HasStatusEffect(StatusType.Unstoppable) && !pokemon.HasStatusEffect(StatusType.Invincible))
                {
                    float step = Mathf.Min(remaining, speed * Time.deltaTime);
                    float moved = Vector3.Dot(MoveWithWalls(direction * step), direction);
                    if (moved < step * 0.2f) yield break;
                    remaining -= step; yield return null;
                }
            }
        }
        finally { if (pokemon != null) pokemon.OnDeath -= onDeath; }
    }
}

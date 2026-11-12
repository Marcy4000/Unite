using UnityEngine;

public class LucarioMarkerVisual : MonoBehaviour
{
    [SerializeField] private GameObject ready, cooling;
    private PlayerManager actor;
    private Pokemon target;
    private double readyAt;
    private float refreshed, range;
    public void Bind(PlayerManager owner, Pokemon enemy, double at, float radius)
    { actor = owner; target = enemy; readyAt = at; range = radius; refreshed = Time.time; }
    private void LateUpdate()
    {
        if (Time.time - refreshed > 0.6f || actor == null || !LucarioMoveRules.Living(actor.Pokemon) || !LucarioMoveRules.Living(target)) { Destroy(gameObject); return; }
        bool visible = LucarioMoveRules.Flat(actor.transform.position - target.transform.position).sqrMagnitude <= range * range
            && CombatTargeting.CanPokemonBeTargeted(target.gameObject, AimTarget.NonAlly, actor.CurrentTeam, false);
        bool available = actor.NetworkManager.ServerTime.Time >= readyAt;
        if (ready != null) ready.SetActive(visible && available);
        if (cooling != null) cooling.SetActive(visible && !available);
        transform.SetPositionAndRotation(target.transform.position + Vector3.up * 0.12f, Quaternion.Euler(0, Time.time * 40, 0));
    }
}

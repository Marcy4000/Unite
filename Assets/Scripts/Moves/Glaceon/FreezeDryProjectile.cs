using System;
using Unity.Netcode;
using UnityEngine;

public class FreezeDryProjectile : NetworkBehaviour
{
    public float speed = 30f;
    private float maxDistance, distanceTraveled;
    private DamageInfo damageInfo;
    private Vector3 direction;
    private Team orangeTeam;
    private bool canMove, gaveDamage;
    private PlayerManager attacker;
    public event Action<int> OnMoveHit;

    public void SetDirection(Vector3 startingPosition, Vector2 value, DamageInfo info, float distance)
    {
        if (!IsServer || canMove || gaveDamage || !CombatTargeting.Finite(startingPosition)
            || !float.IsFinite(value.x) || !float.IsFinite(value.y) || !float.IsFinite(distance) || distance <= 0
            || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(info.attackerId, out var source)
            || !source.TryGetComponent(out attacker)) return;
        direction = new Vector3(value.x, 0, value.y).normalized;
        if (direction.sqrMagnitude <= 0.001f) return;
        transform.position = startingPosition;
        damageInfo = info;
        maxDistance = distance;
        orangeTeam = attacker.CurrentTeam.Team;
        canMove = true;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || !canMove || gaveDamage) return;
        float step = Mathf.Min(Mathf.Max(0, speed) * Time.deltaTime, maxDistance - distanceTraveled);
        Vector3 start = transform.position;
        Vector3 end = start + direction * step;
        var sphere = GetComponent<SphereCollider>();
        float radius = sphere != null ? sphere.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z)) : 0.6f;
        radius = Mathf.Max(0.01f, radius);
        int steps = Mathf.Clamp(Mathf.CeilToInt(step / (radius * 0.5f)), 1, 128);
        for (int index = 0; index <= steps; index++)
        {
            Vector3 position = Vector3.Lerp(start, end, index / (float)steps);
            foreach (var target in CombatTargeting.SphereTargets(position, radius))
            {
                if (!GlaceonCharacterRuntime.CanHit(attacker, target)) continue;
                transform.position = position;
                GiveDamage();
                return;
            }
        }
        transform.position = end;
        distanceTraveled += step;
        if (distanceTraveled >= maxDistance) GiveDamage();
    }

    private void GiveDamage()
    {
        if (!IsServer || !IsSpawned || !canMove || gaveDamage) return;
        gaveDamage = true;
        canMove = false;
        int count = 0;
        foreach (var pokemon in CombatTargeting.SphereTargets(transform.position, 1))
        {
            if (!GlaceonCharacterRuntime.CanHit(attacker, pokemon)) continue;
            pokemon.ResolveDamage(damageInfo);
            count++;
        }
        OnMoveHit?.Invoke(count);
        if (IsSpawned) NetworkObject.Despawn(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !canMove || gaveDamage) return;
        if (CombatTargeting.TryGetPokemon(other, out var pokemon) && GlaceonCharacterRuntime.CanHit(attacker, pokemon)) GiveDamage();
    }
    public override void OnNetworkDespawn()
    {
        canMove = false;
        OnMoveHit = null;
        base.OnNetworkDespawn();
    }
}

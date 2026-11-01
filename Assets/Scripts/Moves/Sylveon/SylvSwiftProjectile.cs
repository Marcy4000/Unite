using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class SylvSwiftProjectile : NetworkBehaviour
{
    public float speed = 30f;
    private float maxDistance;
    private DamageInfo damageInfo;
    private Vector3 direction;
    private float distanceTraveled;
    private Team orangeTeam;
    private bool canMove;
    private bool hit;
    private bool serverDriven;
    private AsyncOperationHandle<GameObject> asset;

    public bool HasValidAssetHandle => asset.IsValid();

    public void HoldAsset(AsyncOperationHandle<GameObject> handle) => asset = handle;

    public void InitializeServer(PlayerManager actor, Vector3 startingPosition, Vector3 travelDirection, DamageInfo info, float distance)
    {
        serverDriven = true;
        hit = false;
        distanceTraveled = 0f;
        transform.position = startingPosition;
        Vector3 flat = new Vector3(travelDirection.x, 0f, travelDirection.z);
        direction = flat.sqrMagnitude > 0.001f ? flat.normalized : (actor != null ? actor.transform.forward : Vector3.forward);
        damageInfo = info;
        maxDistance = distance;
        orangeTeam = actor != null ? actor.CurrentTeam.Team : Team.Neutral;
        canMove = true;
    }

    public void SetDirection(Vector3 startingPosition, Vector2 travelDirection, DamageInfo info, float distance)
    {
        serverDriven = false;
        hit = false;
        distanceTraveled = 0f;
        transform.position = startingPosition;
        Vector3 flat = new Vector3(travelDirection.x, 0f, travelDirection.y);
        direction = flat.sqrMagnitude > 0.001f ? flat.normalized : Vector3.forward;
        damageInfo = info;
        maxDistance = distance;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null
            && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(info.attackerId, out NetworkObject attackerObj)
            && attackerObj != null && attackerObj.TryGetComponent(out PlayerManager pm))
        {
            orangeTeam = pm.CurrentTeam.Team;
        }
        canMove = true;
    }

    private void Update()
    {
        if (!canMove || hit) return;
        if (serverDriven ? !IsServer : !IsOwner) return;

        float step = speed * Time.deltaTime;
        Vector3 nextPosition = transform.position + direction * step;
        if (TrySweepHit(transform.position, nextPosition))
            return;

        transform.position = nextPosition;
        distanceTraveled += step;

        if (distanceTraveled >= maxDistance)
        {
            if (TryOverlapHit(transform.position))
                return;
            DespawnSelf();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!canMove || hit || other == null) return;
        if (serverDriven ? !IsServer : !IsOwner) return;
        TryHitCollider(other);
    }

    private bool TrySweepHit(Vector3 from, Vector3 to)
    {
        float radius = GetWorldRadius();
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist > 0.0001f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(from, radius, delta / dist, dist);
            for (int index = 0; index < hits.Length; index++)
            {
                if (TryHitCollider(hits[index].collider))
                    return true;
            }
        }
        return TryOverlapHit(to);
    }

    private bool TryOverlapHit(Vector3 center)
    {
        Collider[] colliders = Physics.OverlapSphere(center, GetWorldRadius());
        for (int index = 0; index < colliders.Length; index++)
        {
            if (TryHitCollider(colliders[index]))
                return true;
        }
        return false;
    }

    private float GetWorldRadius()
    {
        SphereCollider col = GetComponent<SphereCollider>();
        float baseRadius = col != null ? col.radius : 1.25f;
        float scale = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
        return baseRadius * scale;
    }

    private bool TryHitCollider(Collider other)
    {
        if (other == null || hit) return false;
        Pokemon pokemon = other.GetComponent<Pokemon>() ?? other.GetComponentInParent<Pokemon>();
        if (!CanHit(pokemon)) return false;

        hit = true;
        canMove = false;
        pokemon.TakeDamageRPC(damageInfo);
        DespawnSelf();
        return true;
    }

    private bool CanHit(Pokemon pokemon)
    {
        if (pokemon == null || !pokemon.IsSpawned || pokemon.CurrentHp <= 0 || pokemon.NetworkObjectId == damageInfo.attackerId)
            return false;
        if (pokemon.TeamMember.Team == orangeTeam)
            return false;
        if (pokemon.HasStatusEffect(StatusType.Invincible) || pokemon.HasStatusEffect(StatusType.Untargetable))
            return false;
        if (!serverDriven && Aim.Instance != null && !Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, orangeTeam))
            return false;
        return true;
    }

    public void DespawnSelf()
    {
        canMove = false;
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            if (IsServer) NetworkObject.Despawn(true);
            else DespawnObjectRPC();
        }
        else if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    [Rpc(SendTo.Server)]
    private void DespawnObjectRPC()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    public override void OnDestroy()
    {
        if (asset.IsValid())
        {
            Addressables.Release(asset);
            asset = default;
        }
        base.OnDestroy();
    }
}

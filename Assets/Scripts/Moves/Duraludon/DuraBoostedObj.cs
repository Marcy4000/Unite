using Unity.Netcode;
using UnityEngine;

public partial class DuraBoostedObj : NetworkBehaviour
{
    [SerializeField] private GameObject beamObject, beamObjectCannon;
    [SerializeField] private BoxCollider beamCollider, beamColliderCannon;

    private bool initialized = false;
    private bool isCasting = false;

    private PlayerManager duraludon;
    private DamageInfo damageInfo;

    public bool IsInitialized => initialized;

    [Rpc(SendTo.Everyone)]
    public void InitializeRPC(DamageInfo dmgInfo, ulong playerID)
    {
        damageInfo = dmgInfo;
        duraludon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[playerID].gameObject.GetComponent<PlayerManager>();
        beamObject.SetActive(false);
        beamObjectCannon.SetActive(false);
        beamCollider.enabled = false;
        beamColliderCannon.enabled = false;

        initialized = true;
        InitializeCombat();
    }

    void Update()
    {
        if (!initialized || !IsOwner || isCasting)
        {
            return;
        }

        transform.position = duraludon.transform.position;
        transform.rotation = duraludon.transform.rotation;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CastRPC(bool cannonMode)
    {
        if (!initialized)
        {
            return;
        }

        StartCoroutine(CastRoutine(cannonMode));
    }

    private System.Collections.IEnumerator CastRoutine(bool cannonMode)
    {
        isCasting = true;
        Vector3 origin = duraludon.transform.position;
        Quaternion rotation = duraludon.transform.rotation;
        ShowBoostedBeamRPC(cannonMode, origin, rotation);
        BoxCollider hitbox = cannonMode ? beamColliderCannon : beamCollider;
        int ticks = cannonMode ? 3 : 1;
        for (int i = 0; i < ticks; i++)
        {
            var targets = new System.Collections.Generic.HashSet<Pokemon>();
            Vector3 center = origin + rotation * hitbox.center;
            foreach (var hit in Physics.OverlapBox(center, hitbox.size * 0.5f, rotation))
            {
                if (hit.TryGetComponent(out Pokemon pokemon) && CanHit(pokemon) && targets.Add(pokemon))
                {
                    DamageInfo hitDamage = cannonMode
                        ? new DamageInfo(duraludon.NetworkObjectId, 0.58f, 2, 66, DamageType.Physical, damageInfo.proprieties)
                        : damageInfo;
                    if (i > 0) hitDamage.proprieties = DamageProprieties.CanCrit | SecondaryDamage;
                    pokemon.TakeDamageRPC(hitDamage);
                    if (cannonMode) RefreshSlow(pokemon, 60, 1f, 8847);
                    int extra = Mathf.RoundToInt(pokemon.GetMaxHp() * (cannonMode ? 0.005f : 0.015f));
                    if (pokemon.Type == PokemonType.Wild || pokemon.Type == PokemonType.Objective) extra = Mathf.Min(extra, 500);
                    pokemon.TakeDamageRPC(new DamageInfo(duraludon.NetworkObjectId, 0f, 0, (short)Mathf.Min(extra, short.MaxValue),
                        DamageType.True, DamageProprieties.CanCrit | SecondaryDamage));
                }
            }
            if (cannonMode) yield return new WaitForSeconds(0.08f);
        }
        isCasting = false;
    }

    [Rpc(SendTo.Everyone)]
    private void ShowBoostedBeamRPC(bool cannonMode, Vector3 origin, Quaternion rotation)
    {
        isCasting = true;
        transform.SetPositionAndRotation(origin, rotation);
        GameObject beam = cannonMode ? beamObjectCannon : beamObject;
        beam.SetActive(true);
        StartCoroutine(HideBoostedBeam(beam));
    }

    private System.Collections.IEnumerator HideBoostedBeam(GameObject beam)
    {
        yield return new WaitForSeconds(0.3f);
        beam.SetActive(false);
        isCasting = false;
    }
}

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class DuraBoostedObj
{
    [SerializeField] private DuraDragonPulseMarkView pulseMarkPrefab;
    private readonly Dictionary<ulong, DuraDragonPulseMarkView> markViews = new Dictionary<ulong, DuraDragonPulseMarkView>();

    [Rpc(SendTo.Owner)]
    private void ShowPulseMarksRPC(ulong targetId, int count)
    {
        markViews.TryGetValue(targetId, out var view);
        if (count == 0)
        {
            if (view != null) Destroy(view.gameObject);
            markViews.Remove(targetId);
            return;
        }
        if (!TryPokemon(targetId, out Pokemon target)) return;
        if (view == null)
        {
            view = Instantiate(pulseMarkPrefab, target.transform.position, Quaternion.identity);
            markViews[targetId] = view;
        }
        view.Bind(target, PulseMarkId, count);
    }

    [Rpc(SendTo.Everyone)]
    private void ClearCombatVisualsRPC()
    {
        DestroyCombatVisuals();
        isCasting = false;
        beamObject.SetActive(false);
        beamObjectCannon.SetActive(false);
    }

    private void DestroyCombatVisuals()
    {
        foreach (var view in markViews.Values) if (view != null) Destroy(view.gameObject);
        markViews.Clear();
    }
}

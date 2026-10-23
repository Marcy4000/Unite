using UnityEngine;

public partial class Pokemon
{
    private static GameObject burnStatusPrefab;
    private GameObject burnStatusView;
    private Vision periodicStatusVision;
    private bool periodicVisionBound;

    private void UpdatePeriodicPresentation()
    {
        if (!IsClient || !IsSpawned) return;
        bool burning = CurrentHp > 0 && HasStatusEffect(StatusType.Burned);
        if (!burning)
        {
            if (burnStatusView != null) Destroy(burnStatusView);
            burnStatusView = null; return;
        }
        if (!periodicVisionBound) { periodicStatusVision = GetComponent<Vision>(); periodicVisionBound = true; }
        bool visible = periodicStatusVision == null || periodicStatusVision.IsRendered;
        if (burnStatusView == null && visible)
        {
            if (burnStatusPrefab == null) burnStatusPrefab = Resources.Load<GameObject>("Combat/BurnStatus");
            if (burnStatusPrefab != null) burnStatusView = Instantiate(burnStatusPrefab, transform);
        }
        if (burnStatusView != null)
        {
            burnStatusView.SetActive(visible);
            burnStatusView.transform.localPosition = Vector3.up * 0.4f;
            burnStatusView.transform.localRotation = Quaternion.Euler(0, Time.time * 65, 0);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (burnStatusView != null) Destroy(burnStatusView);
        burnStatusView = null; periodicStatuses.Clear();
        base.OnNetworkDespawn();
    }
}

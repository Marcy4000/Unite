using UnityEngine;

// Local owner-only decoration. All geometry is authored in its prefab.
public class DuraDragonPulseMarkView : MonoBehaviour
{
    [SerializeField] private GameObject[] orbs;
    [SerializeField] private GameObject finalRing;
    [SerializeField] private Vector3 offset = new Vector3(0, 2, 0);
    private Pokemon target;
    private ushort statusId;
    private float started, expires;

    public void Bind(Pokemon pokemon, ushort markStatusId, int count)
    {
        target = pokemon;
        statusId = markStatusId;
        started = Time.time;
        expires = Time.time + DuraMoveRules.MarkDuration;
        for (int i = 0; i < orbs.Length; i++) orbs[i].SetActive(i < count);
        finalRing.SetActive(count >= DuraMoveRules.MaxMarks);
        transform.position = target.transform.position + offset;
    }

    private void LateUpdate()
    {
        if (target == null || target.CurrentHp <= 0 || Time.time >= expires
            || (Time.time - started > 0.2f && !target.HasStatusEffect(statusId)))
        {
            Destroy(gameObject);
            return;
        }
        transform.position = target.transform.position + offset;
    }
}

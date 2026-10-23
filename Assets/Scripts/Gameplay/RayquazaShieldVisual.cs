using UnityEngine;

// Local presentation of replicated shield state, including late-joining clients.
// The authored Resources prefab needs no extra network object or addressable entry.
[DisallowMultipleComponent]
public class RayquazaShieldVisual : MonoBehaviour
{
    public const string ResourcePath = "Effects/RayquazaShield";
    private Pokemon pokemon;
    private Vision vision;
    private GameObject effect;
    private static GameObject prefab;
    private bool attemptedLoad;

    private void Awake()
    {
        pokemon = GetComponent<Pokemon>();
        vision = GetComponent<Vision>();
    }

    private void LateUpdate()
    {
        if (pokemon == null || !pokemon.IsSpawned || !pokemon.IsClient || !pokemon.HasRayquazaShield)
        {
            ClearEffect();
            return;
        }

        if (effect == null)
        {
            if (prefab == null && !attemptedLoad)
            {
                attemptedLoad = true;
                prefab = Resources.Load<GameObject>(ResourcePath);
                if (prefab == null) Debug.LogError("Rayquaza shield visual prefab is missing.", this);
            }
            if (prefab == null) return;
            effect = Instantiate(prefab, transform);
        }

        effect.SetActive(vision == null || vision.IsRendered);
        effect.transform.localRotation = Quaternion.Euler(0, Time.time * 35f, 0);
        effect.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 4f) * 0.035f);
    }

    private void ClearEffect()
    {
        if (effect == null) return;
        effect.SetActive(false);
        Destroy(effect);
        effect = null;
    }

    private void OnDisable() => ClearEffect();
}

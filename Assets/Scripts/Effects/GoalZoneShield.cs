using System.Collections;
using UnityEngine;

public class GoalZoneShield : MonoBehaviour
{
    [SerializeField] private MeshRenderer shieldRenderer;
    [SerializeField] private float shieldSpeed = 0.5f;

    [SerializeField] [ColorUsage(true, true)] private Color shieldColor;

    private bool isShieldActive = false;
    private Material material;
    private Material ShieldMaterial => material != null ? material : material = shieldRenderer.material;

    private void Awake()
    {
        isShieldActive = false;
        HideShield();
    }

    public void SetShieldColor(Color color)
    {
        shieldColor = color;
    }

    public void ShowShield()
    {
        if (shieldRenderer == null || !isActiveAndEnabled) return;
        if (isShieldActive) return;
        initialized = true;
        isShieldActive = true;
        ShieldMaterial.SetFloat("_Smoothness", 0.5f);

        Color shieldColor = this.shieldColor;
        StopAllCoroutines();
        StartCoroutine(TransitionColor(shieldColor, 0.3f));
    }

    public void HideShield()
    {
        if (shieldRenderer == null) return;
        if (initialized && !isShieldActive) return;
        initialized = true;
        isShieldActive = false;
        ShieldMaterial.SetFloat("_Smoothness", 0);

        Color shieldColor = this.shieldColor;
        StopAllCoroutines();
        if (isActiveAndEnabled) StartCoroutine(TransitionColor(Color.clear, 0.3f));
        else ShieldMaterial.SetColor("_EmissionColor", Color.clear);
    }

    private bool initialized;

    private IEnumerator TransitionColor(Color targetColor, float duration)
    {
        Color startColor = ShieldMaterial.GetColor("_EmissionColor");
        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            ShieldMaterial.SetColor("_EmissionColor", Color.Lerp(startColor, targetColor, elapsedTime / duration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        ShieldMaterial.SetColor("_EmissionColor", targetColor);
    }

    private void Update()
    {
        ShieldMaterial.SetTextureOffset("_BaseMap", new Vector2(Time.time * shieldSpeed % 1, 0));

        if (isShieldActive)
        {
            ShieldMaterial.SetColor("_EmissionColor", shieldColor * (1 - Mathf.Sin(Time.time * 5) * 0.1f));
        }
    }

    private void OnDestroy() { if (material != null) Destroy(material); }
}

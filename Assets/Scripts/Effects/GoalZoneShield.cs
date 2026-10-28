using UnityEngine;

public class GoalZoneShield : MonoBehaviour
{
    [SerializeField] private MeshRenderer shieldRenderer;
    [SerializeField] private float shieldSpeed = 0.5f;

    [SerializeField] [ColorUsage(true, true)] private Color shieldColor;

    private const float FadeDuration = 0.3f;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
    private static readonly int ShieldFadeId = Shader.PropertyToID("_ShieldFade");
    private bool isShieldActive;
    private Material material;
    private Vector4 shieldFade;

    private bool EnsureMaterial()
    {
        if (shieldRenderer == null) return false;
        if (material != null) return true;
        material = shieldRenderer.material;
        material.SetColor(EmissionColorId, shieldColor);
        material.SetFloat(ScrollSpeedId, shieldSpeed);
        material.SetVector(ShieldFadeId, shieldFade);
        return true;
    }

    private void Awake()
    {
        if (EnsureMaterial()) SetVisibility(false, true);
    }

    public void SetShieldColor(Color color)
    {
        shieldColor = color;
        if (material != null) material.SetColor(EmissionColorId, color);
    }

    public void ShowShield()
    {
        if (!isActiveAndEnabled || isShieldActive || !EnsureMaterial()) return;
        SetVisibility(true, false);
    }

    public void HideShield()
    {
        if (!EnsureMaterial()) return;
        if (!isActiveAndEnabled) SetVisibility(false, true);
        else if (isShieldActive) SetVisibility(false, false);
    }

    private void SetVisibility(bool visible, bool immediate)
    {
        float target = visible ? 1f : 0f;
        float current = Mathf.Lerp(shieldFade.x, shieldFade.y,
            Mathf.Clamp01((Time.time - shieldFade.z) * shieldFade.w));
        shieldFade = new Vector4(immediate ? target : current, target, Time.time, 1f / FadeDuration);
        isShieldActive = visible;
        material.SetVector(ShieldFadeId, shieldFade);
    }

    private void OnDisable()
    {
        if (material != null) SetVisibility(false, true);
    }

    private void OnDestroy() { if (material != null) Destroy(material); }
}

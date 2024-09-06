using System.Collections;
using UnityEngine;

public class GoalZoneShield : MonoBehaviour
{
    [SerializeField] private MeshRenderer shieldRenderer;
    [SerializeField] private float shieldSpeed = 0.5f;

    [SerializeField] [ColorUsage(true, true)] private Color shieldColor;

    private bool isShieldActive = false;

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
        isShieldActive = true;
        shieldRenderer.material.SetFloat("_Smoothness", 0.5f);

        Color shieldColor = this.shieldColor;
        StopAllCoroutines();
        StartCoroutine(TransitionColor(shieldColor, 0.3f));
    }

    public void HideShield()
    {
        isShieldActive = false;
        shieldRenderer.material.SetFloat("_Smoothness", 0);

        Color shieldColor = this.shieldColor;
        StopAllCoroutines();
        StartCoroutine(TransitionColor(Color.clear, 0.3f));
    }

    private IEnumerator TransitionColor(Color targetColor, float duration)
    {
        Color startColor = shieldRenderer.material.GetColor("_EmissionColor");
        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            shieldRenderer.material.SetColor("_EmissionColor", Color.Lerp(startColor, targetColor, elapsedTime / duration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        shieldRenderer.material.SetColor("_EmissionColor", targetColor);
    }

    private void Update()
    {
        shieldRenderer.material.SetTextureOffset("_BaseMap", new Vector2(Time.time * shieldSpeed % 1, 0));

        if (isShieldActive)
        {
            shieldRenderer.material.SetColor("_EmissionColor", shieldColor * (1 - Mathf.Sin(Time.time * 5) * 0.1f));
        }
    }
}

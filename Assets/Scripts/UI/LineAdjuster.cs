using UnityEngine;

public class LineAdjuster : MonoBehaviour
{
    private RectTransform rectTransform;

    private Vector2 initialPosition = new Vector2(-8, 9);
    private float initialWidth = 2250;
    private float initialHeight = 150;
    private float initialRotation = -24.9f;

    private const float referenceAspectRatio = 16f / 9f;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        AdjustLine();
    }

    void Update()
    {
        AdjustLine();
    }

    void AdjustLine()
    {
        if (rectTransform == null) return;

        float currentAspectRatio = (float)Screen.width / Screen.height;

        float scaleFactor = currentAspectRatio / referenceAspectRatio;

        Vector2 adjustedPosition = new Vector2(initialPosition.x * scaleFactor, initialPosition.y);

        float adjustedWidth = initialWidth * scaleFactor;

        float adjustedRotation = initialRotation * (referenceAspectRatio / currentAspectRatio);

        rectTransform.anchoredPosition = adjustedPosition;
        rectTransform.sizeDelta = new Vector2(adjustedWidth, initialHeight);
        rectTransform.rotation = Quaternion.Euler(0, 0, adjustedRotation);
    }
}
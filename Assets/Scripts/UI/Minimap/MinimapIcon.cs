using UnityEngine;

public class MinimapIcon : MonoBehaviour
{
    public Transform target;
    public RectTransform minimapRect;
    public float worldSizeX = 100f;
    public float worldSizeZ = 100f;

    private RectTransform iconRect;

    void Awake()
    {
        iconRect = GetComponent<RectTransform>();
    }

    public void Initialize(RectTransform minimapRect, float worldSizeX, float worldSizeZ)
    {
        this.minimapRect = minimapRect;
        this.worldSizeX = worldSizeX;
        this.worldSizeZ = worldSizeZ;
    }

    public void SetTarget(Transform target)
    {
        this.target = target;
    }

    void FixedUpdate()
    {
        UpdateIconPosition();
    }

    void UpdateIconPosition()
    {
        if (target == null) return;

        UpdateIconPosition(target.position);
    }

    public void UpdateIconPosition(Vector3 position)
    {
        Vector3 relativePosition = position;

        float normalizedX = relativePosition.x / worldSizeX;
        float normalizedZ = relativePosition.z / worldSizeZ;

        float minimapPosX = normalizedX * minimapRect.rect.width;
        float minimapPosZ = normalizedZ * minimapRect.rect.height;

        iconRect.anchoredPosition = new Vector2(minimapPosX, minimapPosZ);
    }
}

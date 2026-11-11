using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Projects animated geometry onto one ground plane without shadow maps or CPU mesh baking.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class FakeMeshShadow : MonoBehaviour
{
    [SerializeField] private Transform sourceRoot;
    [SerializeField] private Material shadowMaterial;
    [SerializeField] private Color shadowColor = new Color(0, 0, 0, 0.3f);
    [Tooltip("World X/Z displacement per unit of height. Zero puts the light directly overhead.")]
    [SerializeField] private Vector2 lightBend;
    [SerializeField, Min(0)] private float surfaceOffset = 0.015f;
    [Tooltip("Include only walkable ground layers for best performance and accuracy.")]
    // Exclude this project's actor and UI layers by default.
    [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers
        & ~((1 << 6) | (1 << 7) | (1 << 5) | (1 << 9));
    [SerializeField, Min(0.01f)] private float probeHeight = 2f;
    [SerializeField, Min(0.01f)] private float probeDistance = 12f;
    [SerializeField, Range(0, 80)] private float maximumSlope = 45f;

    private sealed class ShadowPart
    {
        public Renderer source;
        public Renderer shadow;
        public Mesh mesh;
    }

    private readonly List<ShadowPart> parts = new List<ShadowPart>();
    private readonly RaycastHit[] groundHits = new RaycastHit[32];
    private MaterialPropertyBlock properties;
    private static readonly int ColorId = Shader.PropertyToID("_ShadowColor");
    private static readonly int PlaneId = Shader.PropertyToID("_GroundPlane");
    private static readonly int DirectionId = Shader.PropertyToID("_ProjectionDirection");
    private static readonly int OffsetId = Shader.PropertyToID("_SurfaceOffset");

    public Color ShadowColor { get => shadowColor; set => shadowColor = value; }
    public Vector2 LightBend { get => lightBend; set => lightBend = value; }

    private void Start()
    {
        if (parts.Count == 0) SetSource(sourceRoot != null ? sourceRoot : transform);
    }

    public void SetSource(Transform model, bool forceRebuild = false)
    {
        if (!forceRebuild && sourceRoot == model && parts.Count > 0) return;
        ClearParts();
        sourceRoot = model;
        if (model == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        if (shadowMaterial == null) shadowMaterial = Resources.Load<Material>("Rendering/FakeMeshShadow");
        if (shadowMaterial == null)
        {
            Debug.LogError("Fake mesh shadow material is missing.", this);
            return;
        }
        properties = new MaterialPropertyBlock();
        foreach (Renderer source in model.GetComponentsInChildren<Renderer>(true))
        {
            // Never copy particles, trails, UI, or our own pending-destruction renderers.
            if (source.gameObject.name == "Fake Mesh Shadow") continue;
            Mesh mesh = null;
            var skin = source as SkinnedMeshRenderer;
            if (skin != null) mesh = skin.sharedMesh;
            else if (source is MeshRenderer && source.TryGetComponent<MeshFilter>(out var filter))
                mesh = filter.sharedMesh;
            if (mesh == null) continue;

            var obj = new GameObject("Fake Mesh Shadow") { hideFlags = HideFlags.DontSave, layer = source.gameObject.layer };
            obj.transform.SetParent(source.transform, false);
            Renderer shadow;
            if (skin != null)
            {
                var copy = obj.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = mesh;
                copy.bones = skin.bones;
                copy.rootBone = skin.rootBone;
                copy.quality = skin.quality;
                copy.localBounds = skin.localBounds;
                copy.updateWhenOffscreen = true;
                shadow = copy;
            }
            else
            {
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                shadow = obj.AddComponent<MeshRenderer>();
            }
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = shadowMaterial;
            shadow.sharedMaterials = materials;
            shadow.shadowCastingMode = ShadowCastingMode.Off;
            shadow.receiveShadows = false;
            shadow.lightProbeUsage = LightProbeUsage.Off;
            shadow.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shadow.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            shadow.enabled = false;
            parts.Add(new ShadowPart { source = source, shadow = shadow, mesh = mesh });
        }
    }

    private void LateUpdate()
    {
        if (parts.Count == 0) return;
        bool grounded = TryGetGround(out Vector3 point, out Vector3 normal);
        Vector3 direction = new Vector3(lightBend.x, -1, lightBend.y);
        // Keep the ray pointed into the receiving plane, even with a very large bend.
        float dot = Vector3.Dot(normal, direction);
        if (dot > -0.05f) direction -= normal * (dot + 0.05f);
        properties.SetColor(ColorId, shadowColor);
        properties.SetVector(PlaneId, new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point)));
        properties.SetVector(DirectionId, direction);
        properties.SetFloat(OffsetId, surfaceOffset);
        foreach (var part in parts)
        {
            if (part.shadow == null || part.source == null) continue;
            part.shadow.enabled = grounded && shadowColor.a > 0 && part.source.enabled
                && !part.source.forceRenderingOff && part.source.gameObject.activeInHierarchy;
            if (!part.shadow.enabled) continue;
            part.shadow.gameObject.layer = part.source.gameObject.layer;
            if (part.source is SkinnedMeshRenderer skin && part.shadow is SkinnedMeshRenderer copy)
                for (int i = 0; i < part.mesh.blendShapeCount; i++)
                    copy.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
            part.shadow.SetPropertyBlock(properties);
            // Vertex projection happens after culling. Supply the projected world bounds.
            Bounds original = part.source.bounds;
            Bounds projected = new Bounds(Project(original.center, point, normal, direction), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = original.center + Vector3.Scale(original.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                projected.Encapsulate(Project(corner, point, normal, direction));
            }
            projected.Expand(0.1f);
            part.shadow.bounds = projected;
        }
    }

    private Vector3 Project(Vector3 vertex, Vector3 point, Vector3 normal, Vector3 direction)
        => vertex - direction * (Vector3.Dot(normal, vertex - point) / Vector3.Dot(normal, direction)) + normal * surfaceOffset;

    private bool TryGetGround(out Vector3 point, out Vector3 normal)
    {
        point = transform.position;
        normal = Vector3.up;
        int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * probeHeight,
            Vector3.down, groundHits, probeHeight + probeDistance, groundLayers, QueryTriggerInteraction.Ignore);
        float closest = float.PositiveInfinity;
        float minimumY = Mathf.Cos(maximumSlope * Mathf.Deg2Rad);
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.transform.IsChildOf(transform) || hit.normal.y < minimumY || hit.distance >= closest) continue;
            closest = hit.distance;
            point = hit.point;
            normal = hit.normal;
        }
        return closest < float.PositiveInfinity;
    }

    private void OnDisable()
    {
        foreach (var part in parts)
            if (part.shadow != null) part.shadow.enabled = false;
    }

    private void OnDestroy() => ClearParts();

    private void ClearParts()
    {
        foreach (var part in parts)
        {
            if (part.shadow == null) continue;
            part.shadow.gameObject.SetActive(false);
            Destroy(part.shadow.gameObject);
        }
        parts.Clear();
    }
}

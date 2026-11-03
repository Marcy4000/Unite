using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

[RequireComponent(typeof(LightProbeGroup))]
[AddComponentMenu("Light Probe Helper/Light Probe Generator")]
public class LightProbeGenerator : MonoBehaviour
{
    [System.Serializable]
    public class LightProbeArea
    {
        public Bounds ProbeVolume = new Bounds(Vector3.zero, new Vector3(10f, 10f, 10f));
        public Vector3 Subdivisions = new Vector3(5, 5, 5);
        public Quaternion Rotation = Quaternion.identity;
        [Min(0)] public int RandomCount = 0;
    }

    public enum LightProbePlacementType
    {
        Grid,
        Random
    }

#if UNITY_EDITOR
    public LightProbeArea LightProbeVolumes = new LightProbeArea();
    public LightProbePlacementType PlacementAlgorithm;
    [Tooltip("Reject probes inside enabled rendered meshes, even without colliders. Closed meshes with outward-facing normals give the most reliable results.")]
    public bool AvoidInsideMeshes = true;
    [Min(0f), Tooltip("Minimum distance in world units between a probe and a mesh surface.")]
    public float SurfaceClearance = 0.1f;
    public LayerMask ObstacleLayers = ~0;

    private void Reset()
    {
        LightProbeVolumes = new LightProbeArea();
        LightProbeVolumes.ProbeVolume.center = transform.position;
    }

    private void OnValidate()
    {
        if (LightProbeVolumes == null)
            LightProbeVolumes = new LightProbeArea();
        Vector3 subdivisions = LightProbeVolumes.Subdivisions;
        LightProbeVolumes.Subdivisions = new Vector3(
            Mathf.Max(1, Mathf.RoundToInt(subdivisions.x)),
            Mathf.Max(1, Mathf.RoundToInt(subdivisions.y)),
            Mathf.Max(1, Mathf.RoundToInt(subdivisions.z)));
        LightProbeVolumes.RandomCount = Mathf.Max(0, LightProbeVolumes.RandomCount);
        Vector3 size = LightProbeVolumes.ProbeVolume.size;
        LightProbeVolumes.ProbeVolume.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        SurfaceClearance = Mathf.Max(0f, SurfaceClearance);
    }

    public void ClearProbes()
    {
        if (!TryGetComponent<LightProbeGroup>(out var probeGroup))
            return;
        Undo.RecordObject(probeGroup, "Clear Light Probes");
        probeGroup.probePositions = new Vector3[0];
        EditorUtility.SetDirty(probeGroup);
        PrefabUtility.RecordPrefabInstancePropertyModifications(probeGroup);
    }

    public void GenProbes()
    {
        OnValidate();
        if (!TryGetComponent<LightProbeGroup>(out var probeGroup))
            probeGroup = Undo.AddComponent<LightProbeGroup>(gameObject);

        var positions = new List<Vector3>();
        Matrix4x4 volumeMatrix = Matrix4x4.TRS(LightProbeVolumes.ProbeVolume.center, LightProbeVolumes.Rotation, Vector3.one);
        bool previousBackfaces = Physics.queriesHitBackfaces;
        MeshObstacles obstacles = null;
        try
        {
            if (AvoidInsideMeshes)
            {
                Physics.queriesHitBackfaces = true;
                obstacles = new MeshObstacles();
                Bounds worldBounds = new Bounds(volumeMatrix.MultiplyPoint3x4(Vector3.zero), Vector3.zero);
                Vector3 halfSize = LightProbeVolumes.ProbeVolume.extents;
                for (int horizontal = -1; horizontal <= 1; horizontal += 2)
                for (int vertical = -1; vertical <= 1; vertical += 2)
                for (int depth = -1; depth <= 1; depth += 2)
                    worldBounds.Encapsulate(volumeMatrix.MultiplyPoint3x4(Vector3.Scale(halfSize,
                        new Vector3(horizontal, vertical, depth))));
                worldBounds.Expand(SurfaceClearance * 2f);
                obstacles.Build(ObstacleLayers, worldBounds);
                Physics.SyncTransforms();
            }

            Vector3 extents = LightProbeVolumes.ProbeVolume.extents;
            if (PlacementAlgorithm == LightProbePlacementType.Grid)
            {
                Vector3 subdivisions = LightProbeVolumes.Subdivisions;
                for (int horizontal = 0; horizontal <= subdivisions.x; horizontal++)
                for (int vertical = 0; vertical <= subdivisions.y; vertical++)
                for (int depth = 0; depth <= subdivisions.z; depth++)
                {
                    Vector3 offset = new Vector3(
                        Mathf.Lerp(-extents.x, extents.x, horizontal / subdivisions.x),
                        Mathf.Lerp(-extents.y, extents.y, vertical / subdivisions.y),
                        Mathf.Lerp(-extents.z, extents.z, depth / subdivisions.z));
                    AddProbe(volumeMatrix.MultiplyPoint3x4(offset), obstacles, positions);
                }
            }
            else
            {
                for (int index = 0; index < LightProbeVolumes.RandomCount; index++)
                {
                    Vector3 offset = new Vector3(Random.Range(-extents.x, extents.x),
                        Random.Range(-extents.y, extents.y), Random.Range(-extents.z, extents.z));
                    AddProbe(volumeMatrix.MultiplyPoint3x4(offset), obstacles, positions);
                }
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = previousBackfaces;
            obstacles?.Dispose();
        }

        Undo.RecordObject(probeGroup, "Generate Light Probes");
        probeGroup.probePositions = positions.ToArray();
        EditorUtility.SetDirty(probeGroup);
        PrefabUtility.RecordPrefabInstancePropertyModifications(probeGroup);
        Debug.Log($"Generated {positions.Count} light probes. Mesh filtering can reduce the requested count.", this);
    }

    private void AddProbe(Vector3 worldPosition, MeshObstacles obstacles, List<Vector3> positions)
    {
        if (obstacles == null || !obstacles.Blocks(worldPosition, SurfaceClearance))
            positions.Add(transform.InverseTransformPoint(worldPosition));
    }

    private sealed class MeshObstacles : System.IDisposable
    {
        private static readonly Vector3[] Directions =
        {
            new Vector3(1f, 0.371f, 0.529f).normalized,
            new Vector3(-0.417f, 1f, 0.233f).normalized,
            new Vector3(0.319f, -0.613f, 1f).normalized
        };
        private readonly Scene scene;
        private readonly PhysicsScene physicsScene;
        private readonly List<MeshCollider> colliders = new List<MeshCollider>();
        private readonly List<Mesh> bakedMeshes = new List<Mesh>();
        private readonly Dictionary<Transform, Transform> transforms = new Dictionary<Transform, Transform>();
        private readonly Collider[] overlaps = new Collider[1];

        public MeshObstacles()
        {
            scene = EditorSceneManager.NewPreviewScene();
            physicsScene = scene.GetPhysicsScene();
        }

        public void Build(LayerMask layers, Bounds volumeBounds)
        {
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.scene.IsValid()
                    || EditorSceneManager.IsPreviewScene(renderer.gameObject.scene)
                    || !volumeBounds.Intersects(renderer.bounds)
                    || (layers.value & (1 << renderer.gameObject.layer)) == 0)
                    continue;

                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skinnedRenderer && skinnedRenderer.sharedMesh != null)
                {
                    mesh = new Mesh();
                    bakedMeshes.Add(mesh);
                    skinnedRenderer.BakeMesh(mesh);
                }
                else if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter))
                {
                    mesh = filter.sharedMesh;
                }
                if (mesh == null || mesh.vertexCount == 0)
                    continue;
                var collider = CopyTransform(renderer.transform).gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                colliders.Add(collider);
            }
        }

        private Transform CopyTransform(Transform source)
        {
            if (transforms.TryGetValue(source, out var copy))
                return copy;
            Transform parent = source.parent != null ? CopyTransform(source.parent) : null;
            var owner = new GameObject("Probe Obstacle") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(owner, scene);
            copy = owner.transform;
            copy.SetParent(parent, false);
            copy.localPosition = source.localPosition;
            copy.localRotation = source.localRotation;
            copy.localScale = source.localScale;
            transforms.Add(source, copy);
            return copy;
        }

        public bool Blocks(Vector3 point, float clearance)
        {
            if (physicsScene.OverlapSphere(point, Mathf.Max(clearance, 0.0001f), overlaps,
                Physics.AllLayers, QueryTriggerInteraction.Ignore) > 0)
                return true;
            foreach (var collider in colliders)
            {
                Bounds bounds = collider.bounds;
                if (!bounds.Contains(point))
                    continue;
                int exits = 0;
                for (int index = 0; index < Directions.Length; index++)
                {
                    Vector3 direction = Directions[index];
                    if (collider.Raycast(new Ray(point, direction), out var hit, bounds.size.magnitude + 0.01f)
                        && Vector3.Dot(hit.normal, direction) > 0f)
                        exits++;
                    if (exits >= 2)
                        return true;
                    if (exits + Directions.Length - index - 1 < 2)
                        break;
                }
            }
            return false;
        }

        public void Dispose()
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var mesh in bakedMeshes)
                Object.DestroyImmediate(mesh);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (LightProbeVolumes == null)
            return;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(LightProbeVolumes.ProbeVolume.center, LightProbeVolumes.Rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, LightProbeVolumes.ProbeVolume.size);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
#endif
}

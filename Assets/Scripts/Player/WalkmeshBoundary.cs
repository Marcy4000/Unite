using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

// Build once per authored walkmesh, shared by every avatar. Weld imported split
// vertices so triangle seams are not mistaken for walls. Weak keys do not retain
// meshes/scenes between matches.
public static class WalkmeshBoundary
{
    private struct Edge { public Vector3 A, B; public int Count; }
    private sealed class Geometry
    {
        public Mesh Mesh;
        public Matrix4x4 Matrix;
        public readonly List<Edge> Edges = new();
    }
    private static readonly ConditionalWeakTable<Collider, Geometry> cache = new();
    public static void Prepare(Collider collider) { if (collider != null) Get(collider); }

    private static Geometry Get(Collider collider)
    {
        if (collider is not MeshCollider meshCollider || meshCollider.sharedMesh == null || !meshCollider.sharedMesh.isReadable) return null;
        var geometry = cache.GetValue(collider, _ => new Geometry());
        var mesh = meshCollider.sharedMesh;
        var matrix = collider.transform.localToWorldMatrix;
        if (geometry.Mesh == mesh && geometry.Matrix == matrix) return geometry;
        geometry.Mesh = mesh;
        geometry.Matrix = matrix;
        geometry.Edges.Clear();
        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        var weld = new Dictionary<Vector3Int, int>();
        var indices = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            var key = Vector3Int.RoundToInt(vertices[i] * 10000);
            if (!weld.TryGetValue(key, out int index)) { index = i; weld.Add(key, index); }
            indices[i] = index;
        }
        var edges = new Dictionary<(int, int), Edge>();
        for (int i = 0; i + 2 < triangles.Length; i += 3)
            for (int j = 0; j < 3; j++)
            {
                int a = indices[triangles[i + j]], b = indices[triangles[i + (j + 1) % 3]];
                if (a == b) continue;
                var key = a < b ? (a, b) : (b, a);
                edges.TryGetValue(key, out var edge);
                edge.A = vertices[a]; edge.B = vertices[b]; edge.Count++;
                edges[key] = edge;
            }
        foreach (var edge in edges.Values)
            if (edge.Count == 1 && Flat(edge.B - edge.A).sqrMagnitude > 0.000001f) geometry.Edges.Add(edge);
        return geometry;
    }

    private static Vector3 Flat(Vector3 value) { value.y = 0; return value; }

    public static bool Contains(Collider collider, Vector3 point)
    {
        if (collider == null) return true;
        return collider.Raycast(new Ray(new Vector3(point.x, collider.bounds.max.y + 1, point.z), Vector3.down), out _,
            Mathf.Max(2, collider.bounds.size.y + 2));
    }

    private static Vector3 Closest(Edge edge, Vector3 point)
    {
        Vector3 direction = Flat(edge.B - edge.A);
        float t = Mathf.Clamp01(Vector3.Dot(Flat(point - edge.A), direction) / direction.sqrMagnitude);
        var result = edge.A + (edge.B - edge.A) * t;
        result.y = point.y;
        return result;
    }

    private static Vector3 Advance(Collider collider, Vector3 from, Vector3 delta)
    {
        if (Contains(collider, from + delta)) return from + delta;
        float min = 0, max = 1;
        for (int i = 0; i < 10; i++)
        {
            float t = (min + max) * 0.5f;
            if (Contains(collider, from + delta * t)) min = t; else max = t;
        }
        return from + delta * min;
    }

    public static bool TrySlide(Collider collider, Vector3 from, Vector3 intended, out Vector3 result)
    {
        result = from;
        var geometry = Get(collider);
        if (geometry == null || geometry.Edges.Count == 0) return false;
        Vector3 remaining = Flat(intended - from);
        Vector3 position = from;
        for (int iteration = 0; iteration < 3 && remaining.sqrMagnitude > 0.00000001f; iteration++)
        {
            Vector3 contact = Advance(collider, position, remaining);
            remaining -= Flat(contact - position);
            position = contact;
            if (remaining.sqrMagnitude < 0.00000001f) break;
            float nearest = float.PositiveInfinity;
            foreach (var edge in geometry.Edges)
                nearest = Mathf.Min(nearest, Flat(Closest(edge, position) - position).sqrMagnitude);
            Vector3 best = Vector3.zero;
            float bestProgress = 0;
            foreach (var edge in geometry.Edges)
            {
                if (Flat(Closest(edge, position) - position).sqrMagnitude > nearest + 0.0001f) continue;
                Vector3 tangent = Flat(edge.B - edge.A).normalized;
                Vector3 projected = tangent * Vector3.Dot(remaining, tangent);
                Vector3 step = Flat(Advance(collider, position, projected) - position);
                float progress = Vector3.Dot(step, remaining);
                if (progress > bestProgress) { bestProgress = progress; best = projected; }
            }
            if (bestProgress < 0.00000001f) break;
            remaining = best;
        }
        position.y = intended.y;
        result = position;
        return true;
    }

    public static bool TryRecover(Collider collider, Vector3 point, out Vector3 result)
    {
        result = point;
        var geometry = Get(collider);
        if (geometry == null) return false;
        float best = float.PositiveInfinity;
        foreach (var edge in geometry.Edges)
        {
            Vector3 closest = Closest(edge, point);
            float distance = Flat(closest - point).sqrMagnitude;
            if (distance >= best) continue;
            var tangent = Flat(edge.B - edge.A).normalized;
            var normal = new Vector3(-tangent.z, 0, tangent.x) * 0.01f;
            if (Contains(collider, closest + normal)) { result = closest + normal; best = distance; }
            else if (Contains(collider, closest - normal)) { result = closest - normal; best = distance; }
        }
        return !float.IsPositiveInfinity(best);
    }
}

using UnityEngine;
using UnityEngine.AI;

public static class BotPositioning
{
    public static Vector3 OffsetFor(int identity)
    {
        var random = new System.Random(identity);
        float angle = (float)random.NextDouble() * Mathf.PI * 2;
        float radius = Mathf.Lerp(.65f, 1.15f, (float)random.NextDouble());
        return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
    }

    public static Vector3 Around(Vector3 anchor, Vector3 offset, float radius = 1.2f)
    {
        if (radius <= 0 || !NavMesh.SamplePosition(anchor, out var center, .5f, NavMesh.AllAreas)) return anchor;
        offset.y = 0;
        offset = Vector3.ClampMagnitude(offset, radius);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Vector3 desired = anchor + offset;
            if (NavMesh.SamplePosition(desired, out var hit, .25f, NavMesh.AllAreas))
            {
                Vector3 delta = hit.position - anchor;
                delta.y = 0;
                if (delta.sqrMagnitude <= radius * radius && Mathf.Abs(hit.position.y - center.position.y) < .5f &&
                    !NavMesh.Raycast(center.position, hit.position, out _, NavMesh.AllAreas)) return hit.position;
            }
            offset *= .5f;
        }
        return anchor;
    }
}

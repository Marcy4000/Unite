using UnityEngine;

public partial class PlayerMovement
{
    // Use the grounded walkmesh even while flying. The walkmeshes are validation
    // surfaces below the map, not the ground the model should land on.
    public bool TryCresseliaLanding(Vector3 requested, out Vector3 landing)
    {
        landing = requested;
        if (!CresseliaMoveRules.Finite(requested)) return false;
        bool Valid(Vector3 point) => walkableWithWalls == null || (Physics.Raycast(new Vector3(point.x, -29, point.z),
            Vector3.down, out RaycastHit hit, 2) && hit.collider == walkableWithWalls);
        bool found = Valid(requested);
        for (int ring = 1; !found && ring <= 12; ring++)
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12;
                Vector3 candidate = requested + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (ring * 0.25f);
                if (!Valid(candidate)) continue;
                landing = candidate; found = true; break;
            }
        if (!found && Valid(lastValidPosition)) { landing = lastValidPosition; found = true; }
        if (!found) return false;
        // Ignore characters, triggers and the subterranean validation meshes.
        int mask = snapToGroundLayerMask & ~LayerMask.GetMask("Players", "WildPokemons", "Objects");
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(landing.x, requested.y + 5, landing.z), Vector3.down, 30, mask, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
            if (hit.collider != walkableWithWalls && hit.collider != walkableNoWalls && hit.point.y > best)
                best = hit.point.y;
        if (float.IsNegativeInfinity(best)) return false;
        landing.y = best + 0.01f;
        return true;
    }
}

using System.Collections;

public class GlaceProjectileEffect : PrefabMoveEffect
{
    public GlaceonShot Shot { get; set; }
    public GlaceonWindVolley Volley { get; set; }
    public int Generation { get; set; }
    protected override IEnumerator Execute()
    {
        var combat = Actor.MovesController.GlaceCombat;
        yield return TravelTo(Target, 20, () => CanHit(Target) && combat != null && combat.Generation == Generation);
        if (Arrived && CanHit(Target) && combat.Generation == Generation) combat.Hit(Target, Shot, Upgraded, Volley);
    }
}

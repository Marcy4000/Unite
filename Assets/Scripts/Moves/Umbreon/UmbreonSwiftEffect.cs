using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class UmbreonSwiftEffect : UmbreonMoveEffect
{
    [SerializeField] private Transform[] stars;
    private int flying;
    protected override IEnumerator Execute()
    {
        LockActions(); FaceRPC(Direction); PlayAnimation(UmbreonMoveRules.Animation(Actor, "spell1"), 0.4f);
        for (int i = 0; i < 4; i++)
        {
            if (CastInterrupted || !CanHit(Target)) break;
            flying++; LaunchRPC(i);
            yield return CastPause(UmbreonMoveRules.SwiftInterval);
        }
        UnlockActions();
        float until = Time.time + 3;
        while (flying > 0 && Time.time < until) yield return null;
    }
    protected override void BeginPresentation() { foreach (var star in stars) if (star != null) star.gameObject.SetActive(false); }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void LaunchRPC(int index) { StartCoroutine(Fly(index)); }
    private IEnumerator Fly(int index)
    {
        Transform star = stars != null && index < stars.Length ? stars[index] : null;
        Vector3 point = Actor.transform.position + Vector3.up * 0.8f;
        if (star != null) { star.gameObject.SetActive(true); star.position = point; }
        float until = Time.time + 3; bool arrived = false;
        while (CanHit(Target) && Time.time < until)
        {
            Vector3 goal = Target.transform.position + Vector3.up * 0.8f;
            point = Vector3.MoveTowards(point, goal, UmbreonMoveRules.SwiftSpeed * Time.deltaTime);
            if (star != null) { star.position = point; star.Rotate(0, 0, 400 * Time.deltaTime); }
            if ((point - goal).sqrMagnitude < 0.04f) { arrived = true; break; }
            yield return null;
        }
        if (IsServer)
        {
            if (arrived && CanHit(Target))
            {
                Target.ResolveDamage(UmbreonMoveRules.Swift(Actor.NetworkObjectId, index == 0));
                if (index == 3 && CanHit(Target)) Combat?.Mark(Target);
            }
            flying--;
        }
        if (star != null) star.gameObject.SetActive(false);
    }
}

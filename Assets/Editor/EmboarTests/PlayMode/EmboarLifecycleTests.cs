using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Real-frame cleanup checks, deliberately isolated from lobby/network/match singletons.
public class EmboarLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    [UnityTearDown]
    public IEnumerator TearDown() { if (root != null) UnityEngine.Object.Destroy(root); yield return null; }

    [UnityTest]
    public IEnumerator ResetReleasesOnlyItsOwnMovementAndActionLeases()
    {
        root = new GameObject("Emboar isolated cleanup");
        root.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        ((Behaviour)root.AddComponent(GameType("Pokemon"))).enabled = false;
        var movement = root.AddComponent(GameType("PlayerMovement")); ((Behaviour)movement).enabled = false;
        ((Behaviour)root.AddComponent(GameType("AnimationManager"))).enabled = false;
        var manager = root.AddComponent(GameType("PlayerManager")); ((Behaviour)manager).enabled = false;
        object move = Activator.CreateInstance(GameType("EmboarHeatCrash"));
        move.GetType().GetField("playerManager").SetValue(move, manager);
        Type moveBase = GameType("EmboarMoveBase");
        object status = Activator.CreateInstance(GameType("BattleActionStatus"), new object[] { 0f });
        object busy = Enum.Parse(GameType("ActionStatusType"), "Busy"), cooldown = Enum.Parse(GameType("ActionStatusType"), "Cooldown");
        Call(status, "AddStatus", cooldown);
        var externalLease = (IDisposable)Call(status, "AcquireStatus", busy);
        var ownLease = (IDisposable)Call(status, "AcquireStatus", busy);
        ((IList)moveBase.GetField("leases", Private).GetValue(move)).Add(ownLease);
        // One root belongs to an external stun, one to this move.
        Call(movement, "AddMovementRestriction"); Call(movement, "AddMovementRestriction");
        moveBase.GetField("rooted", Private).SetValue(move, true);
        Call(move, "ResetMove"); yield return null;
        Assert.That(movement.GetType().GetProperty("CanMove").GetValue(movement), Is.False);
        Assert.That(Call(status, "HasStatus", busy), Is.True); Assert.That(Call(status, "HasStatus", cooldown), Is.True);
        Call(move, "ResetMove"); // Idempotent: must not consume the other movement restriction.
        Assert.That(movement.GetType().GetProperty("CanMove").GetValue(movement), Is.False);
        Call(movement, "RemoveMovementRestriction"); externalLease.Dispose();
        Assert.That(movement.GetType().GetProperty("CanMove").GetValue(movement), Is.True);
        Assert.That(Call(status, "HasStatus", busy), Is.False); Assert.That(Call(status, "HasStatus", cooldown), Is.True);
    }
    [UnityTest]
    public IEnumerator DestroyedLandingEffectRestoresItsVisualLift()
    {
#if UNITY_EDITOR
        root = new GameObject("Emboar lift cleanup");
        var model = new GameObject("Model"); model.transform.SetParent(root.transform);
        Vector3 original = new Vector3(0, 0.1f, 0); model.transform.localPosition = original + Vector3.up;
        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Emboar/EmboarHeatCrashObject.prefab"), root.transform);
        var effect = instance.GetComponent(GameType("EmboarMoveObject"));
        Set(effect, "liftedModel", model.transform); Set(effect, "modelOrigin", original);
        UnityEngine.Object.Destroy(instance); yield return null;
        Assert.That(Vector3.Distance(model.transform.localPosition, original), Is.LessThan(0.0001f));
#else
        Assert.Ignore("Requires Editor-authored prefabs."); yield break;
#endif
    }
    [UnityTest]
    public IEnumerator RepeatedDespawnCleanupRestoresTheModelWithoutATerminalPhase()
    {
#if UNITY_EDITOR
        root = new GameObject("Emboar despawn cleanup");
        var model = new GameObject("Model"); model.transform.SetParent(root.transform); model.transform.localPosition = Vector3.up;
        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Emboar/EmboarHeatCrashObject.prefab"), root.transform);
        var effect = instance.GetComponent(GameType("EmboarMoveObject"));
        Set(effect, "liftedModel", model.transform); Set(effect, "modelOrigin", Vector3.zero);
        Call(effect, "OnNetworkDespawn"); Call(effect, "OnNetworkDespawn"); yield return null;
        Assert.That(model.transform.localPosition, Is.EqualTo(Vector3.zero));
#else
        Assert.Ignore("Requires Editor-authored prefabs."); yield break;
#endif
    }
}

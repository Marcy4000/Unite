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

public class FlareonLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    [UnityTearDown] public IEnumerator TearDown() { if (root != null) UnityEngine.Object.Destroy(root); yield return null; }

    [UnityTest]
    public IEnumerator RepeatedResetReleasesOnlyTheCastsOwnLocks()
    {
        root = new GameObject("Flareon isolated cleanup"); root.AddComponent(Type("Unity.Netcode.NetworkObject"));
        ((Behaviour)root.AddComponent(Type("Pokemon"))).enabled = false;
        var movement = root.AddComponent(Type("PlayerMovement")); ((Behaviour)movement).enabled = false;
        ((Behaviour)root.AddComponent(Type("AnimationManager"))).enabled = false;
        var manager = root.AddComponent(Type("PlayerManager")); ((Behaviour)manager).enabled = false;
        object move = Activator.CreateInstance(Type("FlareonTakedown")); move.GetType().GetField("playerManager").SetValue(move, manager);
        object status = Activator.CreateInstance(Type("BattleActionStatus"), 0f);
        object busy = Enum.Parse(Type("ActionStatusType"), "Busy"), cooldown = Enum.Parse(Type("ActionStatusType"), "Cooldown");
        Call(status, "AddStatus", cooldown);
        var external = (IDisposable)Call(status, "AcquireStatus", busy);
        ((IList)Type("FlareonMoveBase").GetField("leases", Private).GetValue(move)).Add(Call(status, "AcquireStatus", busy));
        Call(movement, "AddMovementRestriction"); Call(movement, "AddMovementRestriction");
        Type("FlareonMoveBase").GetField("rooted", Private).SetValue(move, true);
        Call(move, "ResetMove"); yield return null; Call(move, "ResetMove");
        Assert.That(movement.GetType().GetProperty("CanMove").GetValue(movement), Is.False);
        Assert.That(Call(status, "HasStatus", busy), Is.True); Assert.That(Call(status, "HasStatus", cooldown), Is.True);
        Call(movement, "RemoveMovementRestriction"); external.Dispose();
        Assert.That(movement.GetType().GetProperty("CanMove").GetValue(movement), Is.True);
        Assert.That(Call(status, "HasStatus", busy), Is.False);
    }
    [UnityTest]
    public IEnumerator DestroyDuringUniteRestoresTheVisualModelPosition()
    {
#if UNITY_EDITOR
        root = new GameObject("Flareon visual lift cleanup"); var model = new GameObject("Model"); model.transform.SetParent(root.transform);
        Vector3 original = Vector3.up * 0.1f; model.transform.localPosition = original + Vector3.up;
        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Flareon/FlareonUniteObject.prefab"), root.transform);
        var effect = instance.GetComponent(Type("FlareonMoveObject"));
        effect.GetType().GetField("movedModel", Private).SetValue(effect, model.transform); effect.GetType().GetField("modelOrigin", Private).SetValue(effect, original);
        UnityEngine.Object.Destroy(instance); yield return null;
        Assert.That(model.transform.localPosition, Is.EqualTo(original));
#else
        Assert.Ignore("Requires authored Editor assets."); yield break;
#endif
    }
    [UnityTest]
    public IEnumerator AttachedAurasStayCenteredAcrossMovementAndIndependentNetworkRootUpdates()
    {
#if UNITY_EDITOR
        root = new GameObject("Flareon aura follow regression");
        root.AddComponent(Type("Unity.Netcode.NetworkObject"));
        ((Behaviour)root.AddComponent(Type("Pokemon"))).enabled = false;
        var manager = root.AddComponent(Type("PlayerManager")); ((Behaviour)manager).enabled = false;
        foreach (string kind in new[] { "Swift", "HeatWave" })
        {
            var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Flareon/Flareon" + kind + "Object.prefab"), root.transform);
            var effect = instance.GetComponent(Type("FlareonMoveObject"));
            effect.GetType().GetField("actor", Private).SetValue(effect, manager);
            var graphics = (GameObject)effect.GetType().GetField("graphics", Private).GetValue(effect);
            var area = (SphereCollider)effect.GetType().GetField("area", Private).GetValue(effect);
            effect.GetType().GetMethod("PositionGraphics", Private).Invoke(effect, new[] { (object)true, Enum.Parse(Type("FlareonCast"), kind) });
            Assert.That(graphics.transform.parent, Is.SameAs(root.transform), kind);
            Assert.That(area.transform.IsChildOf(instance.transform), Is.True, "The server hitbox must remain on the network effect.");
            for (int frame = 0; frame < 3; frame++)
            {
                // Move after presentation, then simulate the effect root receiving a pulse/network correction.
                root.transform.SetPositionAndRotation(new Vector3(frame * 2, 0, -frame), Quaternion.Euler(0, frame * 90, 0));
                instance.transform.position = root.transform.position + new Vector3(0.6f, 0, 0.8f);
                Assert.That(Vector3.Distance(graphics.transform.position, root.transform.position), Is.LessThan(0.0001f), kind);
                Assert.That(Quaternion.Angle(graphics.transform.rotation, root.transform.rotation), Is.LessThan(0.001f), kind);
                yield return null;
                Assert.That(Vector3.Distance(graphics.transform.position, root.transform.position), Is.LessThan(0.0001f), kind);
            }
            Call(effect, "OnNetworkDespawn"); Call(effect, "OnNetworkDespawn");
            Assert.That(graphics.activeSelf, Is.False, kind);
            Assert.That(graphics.transform.parent, Is.SameAs(instance.transform), kind);
            UnityEngine.Object.Destroy(instance); yield return null;
            Assert.That(graphics == null, Is.True, "Despawned aura graphics must not remain on the actor.");
        }
#else
        Assert.Ignore("Requires authored Editor assets."); yield break;
#endif
    }
    [UnityTest]
    public IEnumerator DestroyingAnAttachedAuraDoesNotOrphanItsGraphics()
    {
#if UNITY_EDITOR
        root = new GameObject("Flareon aura destruction regression");
        root.AddComponent(Type("Unity.Netcode.NetworkObject"));
        ((Behaviour)root.AddComponent(Type("Pokemon"))).enabled = false;
        var manager = root.AddComponent(Type("PlayerManager")); ((Behaviour)manager).enabled = false;
        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Flareon/FlareonHeatWaveObject.prefab"), root.transform);
        var effect = instance.GetComponent(Type("FlareonMoveObject"));
        effect.GetType().GetField("actor", Private).SetValue(effect, manager);
        var graphics = (GameObject)effect.GetType().GetField("graphics", Private).GetValue(effect);
        effect.GetType().GetMethod("PositionGraphics", Private).Invoke(effect, new[] { (object)true, Enum.Parse(Type("FlareonCast"), "HeatWave") });
        UnityEngine.Object.Destroy(instance); yield return null; yield return null;
        Assert.That(graphics == null, Is.True);
#else
        Assert.Ignore("Requires authored Editor assets."); yield break;
#endif
    }
}

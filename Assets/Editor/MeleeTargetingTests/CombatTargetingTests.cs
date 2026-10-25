using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class CombatTargetingTests
{
    private GameObject root;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Rule(string name, params object[] args) => GameType("CombatTargeting").GetMethod(name).Invoke(null, args);
    [TearDown] public void TearDown() { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
    private Collider Body(string kind)
    {
        root = new GameObject("Current pose targeting test");
        var collider = (Collider)root.AddComponent(GameType("UnityEngine." + kind));
        if (collider is SphereCollider sphere) { sphere.center = Vector3.up; sphere.radius = 0.5f; }
        if (collider is CapsuleCollider capsule) { capsule.center = Vector3.up; capsule.height = 2; capsule.radius = 0.5f; }
        if (collider is BoxCollider box) { box.center = Vector3.up; box.size = new Vector3(1, 2, 1); }
        if (collider is CharacterController controller) { controller.center = Vector3.up; controller.height = 2; controller.radius = 0.5f; }
        return collider;
    }
    [TestCase("SphereCollider")]
    [TestCase("CapsuleCollider")]
    [TestCase("BoxCollider")]
    [TestCase("CharacterController")]
    public void CurrentGeometryRejectsAnOldPhysicsPoseAfterATeleport(string kind)
    {
        Collider body = Body(kind); Physics.SyncTransforms();
        Assert.That(Rule("OverlapsSphereNow", body, Vector3.up, 1f), Is.True);
        root.transform.position = Vector3.forward * 80;
        Assert.That(Rule("OverlapsSphereNow", body, Vector3.up, 1f), Is.False);
        Assert.That(Rule("InCurrentBoxBounds", body, Vector3.up, Vector3.one, Quaternion.identity), Is.False);
    }
    [TestCase("SphereCollider")]
    [TestCase("CapsuleCollider")]
    [TestCase("BoxCollider")]
    [TestCase("CharacterController")]
    public void CurrentGeometryAcceptsMovedRotatedAndScaledBodies(string kind)
    {
        Collider body = Body(kind);
        root.transform.SetPositionAndRotation(new Vector3(10, 0, 10), Quaternion.Euler(0, 45, 0)); root.transform.localScale = new Vector3(2, 1, 3);
        Assert.That(Rule("OverlapsSphereNow", body, new Vector3(10, 1, 10), 0.1f), Is.True);
        Assert.That(Rule("InCurrentBoxBounds", body, new Vector3(10, 1, 10), Vector3.one, Quaternion.Euler(0, 30, 0)), Is.True);
    }
    [Test]
    public void CapsuleExtentsAreNotReducedToItsRootOrCenterPoint()
    {
        var body = (CapsuleCollider)Body("CapsuleCollider"); body.direction = 2; body.center = Vector3.zero; body.height = 6;
        Assert.That(Rule("OverlapsSphereNow", body, Vector3.forward * 2.8f, 0.1f), Is.True);
        Assert.That(Rule("OverlapsSphereNow", body, Vector3.forward * 4, 0.1f), Is.False);
    }
    [Test]
    public void SphereBoxValidationUsesTheCurrentRotatedBoxNotAnExpandedRootRadius()
    {
        var body = (BoxCollider)Body("BoxCollider"); body.center = Vector3.zero; body.size = new Vector3(1, 1, 6);
        root.transform.rotation = Quaternion.Euler(0, 90, 0);
        Assert.That(Rule("OverlapsSphereNow", body, Vector3.right * 2.9f, 0.1f), Is.True);
        Assert.That(Rule("OverlapsSphereNow", body, Vector3.forward * 2.9f, 0.1f), Is.False);
    }
    [Test]
    public void DisabledAndInactiveHurtboxesCannotBeSelected()
    {
        Collider body = Body("SphereCollider"); body.enabled = false;
        Assert.That(Rule("OverlapsSphereNow", body, Vector3.up, 1f), Is.False);
        body.enabled = true; root.SetActive(false);
        Assert.That(Rule("OverlapsSphereNow", body, Vector3.up, 1f), Is.False);
    }
    [Test]
    public void InvalidRangesAndCoordinatesCannotPassValidation()
    {
        Collider body = Body("SphereCollider");
        Assert.That(Rule("OverlapsSphereNow", body, new Vector3(float.NaN, 0, 0), 1f), Is.False);
        Assert.That(Rule("OverlapsSphereNow", body, Vector3.zero, -1f), Is.False);
        Assert.That(Rule("WithinMeleeRange", Vector3.zero, Vector3.zero, float.NaN), Is.False);
        Assert.That(Rule("WithinMeleeRange", Vector3.zero, new Vector3(0, -50, -50), 3.5f), Is.False);
    }
    [TestCase("Jolteon/JoltBasicAtk.cs")]
    [TestCase("Flygon/FlygonBasicAtk.cs")]
    [TestCase("Marshadow/MarshadowBasicAtk.cs")]
    [TestCase("Serperior/SerperiorBasicAtk.cs")]
    [TestCase("WildPokemons/WildPokemonBasicAtk.cs")]
    public void LegacyMeleeAttacksAlsoCheckEveryVictimsRootRange(string path)
        => Assert.That(File.ReadAllText("Assets/Scripts/Moves/" + path), Does.Contain("CombatTargeting.WithinMeleeRange"));
    [Test]
    public void FlygonQueriesUseSharedVisionAndCurrentPoseFiltering()
    {
        Assert.That(File.ReadAllText("Assets/Scripts/Moves/Flygon/FlygonMoveObject.cs"), Does.Contain("CombatTargeting.SphereTargets"));
        Assert.That(File.ReadAllText("Assets/Scripts/Moves/Flygon/SupersonicHitbox.cs"), Does.Contain("CombatTargeting.BoxTargets"));
    }
}

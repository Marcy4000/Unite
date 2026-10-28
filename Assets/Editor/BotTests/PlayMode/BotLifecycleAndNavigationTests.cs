using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class TeleportTriggerContacts : MonoBehaviour
{
    public int Enters { get; private set; }
    public int Exits { get; private set; }

    private void OnTriggerEnter(Collider other) => Enters++;
    private void OnTriggerExit(Collider other) => Exits++;
}

public class BotLifecycleAndNavigationTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private GameObject root;
    private NavMeshData navigation;
    private NavMeshDataInstance navigationInstance;

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (navigationInstance.valid) navigationInstance.Remove();
        if (navigation != null) Object.Destroy(navigation);
        if (root != null) Object.Destroy(root);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RespawnTeleportCancelsKnockupAndCannotBeOverwrittenByOldDashCleanup()
    {
        root = new GameObject("Respawn movement regression");
        var controller = root.AddComponent<CharacterController>();
        var movement = root.AddComponent(GameType("PlayerMovement"));
        ((Behaviour)movement).enabled = false;
        Set(movement, "characterController", controller);
        Call(movement, "Knockup", 3f, 0.2f);
        // The old respawn path only worked at exactly y=-50. Perturb the
        // parking position to reproduce the reported alive-but-off-map case.
        root.transform.position = new Vector3(0, -49.4f, -50);
        Vector3 spawn = new Vector3(5, 0, 5);
        Call(movement, "Teleport", spawn, Quaternion.identity, true);
        yield return new WaitForSeconds(0.3f);
        Assert.That(Vector3.Distance(root.transform.position, spawn), Is.LessThan(0.001f));
        Assert.That(movement.GetType().GetProperty("IsKnockedUp").GetValue(movement), Is.False);
        Assert.That(controller.enabled, Is.True);

        movement.GetType().GetMethod("StartDash", new[] { typeof(Vector3), typeof(float), typeof(float) })
            .Invoke(movement, new object[] { Vector3.forward, 4f, 0.1f });
        Vector3 nextSpawn = new Vector3(-5, 0, -5);
        Call(movement, "Teleport", nextSpawn, Quaternion.identity, true);
        yield return new WaitForSeconds(0.2f);
        Assert.That(Vector3.Distance(root.transform.position, nextSpawn), Is.LessThan(0.001f));
        Assert.That(movement.GetType().GetProperty("IsDashing").GetValue(movement), Is.False);
        Assert.That(movement.GetType().GetField("lastValidPosition", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(movement), Is.EqualTo(nextSpawn));
    }

    [UnityTest]
    public IEnumerator TeleportLeavesTriggersWithoutLosingExitOrReenteringAnUnchangedOverlap()
        => VerifyTeleportTriggerLifecycle(false);

    [UnityTest]
    public IEnumerator TeleportLeavesConvexMeshHitboxesWithoutLosingExit()
        => VerifyTeleportTriggerLifecycle(true);

    private IEnumerator VerifyTeleportTriggerLifecycle(bool useMesh)
    {
        root = new GameObject("Teleport trigger regression");
        root.transform.position = new Vector3(10000, 0, 10000);
        var zone = new GameObject("Persistent area");
        zone.transform.SetParent(root.transform, false);
        Collider trigger;
        if (useMesh)
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            source.transform.SetParent(root.transform, false);
            source.SetActive(false);
            var mesh = zone.AddComponent<MeshCollider>();
            mesh.sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            mesh.convex = true;
            zone.transform.localScale = Vector3.one * 4;
            trigger = mesh;
        }
        else
        {
            var box = zone.AddComponent<BoxCollider>();
            box.size = Vector3.one * 4;
            trigger = box;
        }
        trigger.isTrigger = true;
        var body = zone.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        var contacts = zone.AddComponent<TeleportTriggerContacts>();
        var actor = new GameObject("Teleporting player");
        actor.transform.SetParent(root.transform, false);
        var controller = actor.AddComponent<CharacterController>();
        var movement = actor.AddComponent(GameType("PlayerMovement"));
        ((Behaviour)movement).enabled = false;
        Set(movement, "characterController", controller);
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(contacts.Enters, Is.EqualTo(1));

        Call(movement, "Teleport", root.transform.position + Vector3.right * 0.5f, Quaternion.identity, true);
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(contacts.Enters, Is.EqualTo(1), "Teleporting within an area must not apply its effect twice.");
        Assert.That(contacts.Exits, Is.Zero);

        Call(movement, "Teleport", root.transform.position + Vector3.right * 5, Quaternion.identity, true);
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(contacts.Exits, Is.EqualTo(1), "Teleporting out must release persistent zone and damage effects.");
        Assert.That(controller.enabled, Is.True);

        Call(movement, "Teleport", root.transform.position, Quaternion.identity, true);
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(contacts.Enters, Is.EqualTo(2), "Returning to the area must apply its effect again.");
    }

    [UnityTest]
    public IEnumerator TeleportDoesNotEnableADisabledController()
    {
        root = new GameObject("Disabled controller teleport");
        var controller = root.AddComponent<CharacterController>();
        controller.enabled = false;
        var movement = root.AddComponent(GameType("PlayerMovement"));
        ((Behaviour)movement).enabled = false;
        Set(movement, "characterController", controller);
        var destination = new Vector3(10000, 0, 10000);
        Call(movement, "Teleport", destination, Quaternion.Euler(0, 90, 0), true);
        Assert.That(root.transform.position, Is.EqualTo(destination));
        Assert.That(controller.enabled, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DynamicWallCarvesADetourAndOpeningRestoresTheDirectPath()
    {
        Vector3 origin = new Vector3(10000, 0, 10000);
        var sources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(30, 0.2f, 30), transform = Matrix4x4.Translate(Vector3.down * 0.1f) }
        };
        navigation = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
            new Bounds(Vector3.zero, new Vector3(40, 10, 40)), Vector3.zero, Quaternion.identity);
        Assert.That(navigation, Is.Not.Null);
        navigationInstance = NavMesh.AddNavMeshData(navigation, origin, Quaternion.identity);
        root = new GameObject("Dynamic navigation wall regression");
        root.transform.position = origin;
        var collider = root.AddComponent<BoxCollider>();
        collider.center = Vector3.up;
        collider.size = new Vector3(1, 3, 6);
        var renderer = root.AddComponent<MeshRenderer>();
        var obstacle = root.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.up;
        obstacle.size = new Vector3(2, 4, 7);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        obstacle.carvingTimeToStationary = 0;
        var barrier = root.AddComponent(GameType("DynamicMapBarrier"));
        ((Behaviour)barrier).enabled = false;
        Set(barrier, "blockingColliders", new Collider[] { collider });
        Set(barrier, "wallRenderers", new Renderer[] { renderer });
        Set(barrier, "navigationObstacle", obstacle);
        Call(barrier, "SetBlocked", true);
        for (int i = 0; i < 10; i++) yield return null;
        Assert.That(NavMesh.SamplePosition(origin + Vector3.left * 8, out var start, 2, NavMesh.AllAreas), Is.True);
        Assert.That(NavMesh.SamplePosition(origin + Vector3.right * 8, out var end, 2, NavMesh.AllAreas), Is.True);
        var path = new NavMeshPath();
        Assert.That(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path), Is.True);
        Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
        Assert.That(path.corners.Length, Is.GreaterThan(2), "Bots need an actual detour, not just a physical collider.");

        Call(barrier, "SetBlocked", false);
        Assert.That(collider.enabled, Is.False);
        Assert.That(renderer.enabled, Is.False);
        Assert.That(obstacle.enabled, Is.False);
        for (int i = 0; i < 10; i++) yield return null;
        Assert.That(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path), Is.True);
        Assert.That(path.corners.Length, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator SoldierRebindsADisabledAgentToACompatibleNavMesh()
    {
        Vector3 origin = new Vector3(10000, 0, 10000);
        var sources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(30, 0.2f, 30), transform = Matrix4x4.Translate(Vector3.down * 0.1f) }
        };
        navigation = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
            new Bounds(Vector3.zero, new Vector3(40, 10, 40)), Vector3.zero, Quaternion.identity);
        navigationInstance = NavMesh.AddNavMeshData(navigation, origin, Quaternion.identity);
        root = new GameObject("Soldier navigation recovery regression");
        root.SetActive(false);
        root.transform.position = origin + Vector3.up * 3;
        root.AddComponent(GameType("Pokemon"));
        var agent = root.AddComponent<NavMeshAgent>();
        agent.enabled = false;
        var soldier = root.AddComponent(GameType("SoldierPokemon"));
        ((Behaviour)soldier).enabled = false;
        root.SetActive(true);
        Set(soldier, "agent", agent);
        Assert.That(soldier.GetType().GetMethod("EnsureNavigation", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(soldier, null), Is.True);
        Assert.That(agent.isOnNavMesh, Is.True);
        Assert.That(root.transform.position.y, Is.LessThan(1));
        yield return null;
        LogAssert.NoUnexpectedReceived();
    }
}

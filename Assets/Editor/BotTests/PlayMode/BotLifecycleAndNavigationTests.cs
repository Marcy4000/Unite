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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public class BotPositioningTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static Vector3 OffsetFor(int identity) => (Vector3)GameType("BotPositioning")
        .GetMethod("OffsetFor").Invoke(null, new object[] { identity });
    private static Vector3 Around(Vector3 anchor, Vector3 offset, float radius = 1.2f) => (Vector3)GameType("BotPositioning")
        .GetMethod("Around").Invoke(null, new object[] { anchor, offset, radius });
    private static readonly Vector3 origin = new Vector3(10000, 0, 10000);
    private NavMeshData navigation;
    private NavMeshDataInstance navigationInstance;
    private GameObject root;

    [TearDown]
    public void Cleanup()
    {
        if (navigationInstance.valid) navigationInstance.Remove();
        if (navigation != null) UnityEngine.Object.DestroyImmediate(navigation);
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
    }

    private void BuildNavigation(params NavMeshBuildSource[] sources)
    {
        navigation = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), new List<NavMeshBuildSource>(sources),
            new Bounds(Vector3.zero, new Vector3(30, 10, 30)), Vector3.zero, Quaternion.identity);
        Assert.That(navigation, Is.Not.Null);
        navigationInstance = NavMesh.AddNavMeshData(navigation, origin, Quaternion.identity);
    }

    private static NavMeshBuildSource Floor(float center, float width) => new NavMeshBuildSource
    {
        shape = NavMeshBuildSourceShape.Box,
        size = new Vector3(width, .2f, 12),
        transform = Matrix4x4.Translate(new Vector3(center, -.1f, 0))
    };

    [Test]
    public void PersonalOffsetsAreStableSmallHorizontalAndDistinct()
    {
        var offsets = new HashSet<Vector3>();
        for (int identity = 1; identity <= 10; identity++)
        {
            Vector3 offset = OffsetFor(identity);
            Assert.That(offset, Is.EqualTo(OffsetFor(identity)));
            Assert.That(offset.y, Is.Zero);
            Assert.That(offset.magnitude, Is.InRange(.649f, 1.151f));
            Assert.That(offsets.Add(offset), Is.True);
        }
    }

    [Test]
    public void SharedAnchorsProduceStableDistinctNavigableDestinations()
    {
        BuildNavigation(Floor(0, 12));
        Vector3 first = Around(origin, OffsetFor(1));
        Vector3 second = Around(origin, OffsetFor(2));
        Assert.That(Vector3.Distance(first, second), Is.GreaterThan(.1f));
        Assert.That(Around(origin, OffsetFor(1)), Is.EqualTo(first));
        Assert.That(NavMesh.SamplePosition(first, out _, .1f, NavMesh.AllAreas), Is.True);
        Assert.That(NavMesh.Raycast(first, second, out _, NavMesh.AllAreas), Is.False);
    }

    [TestCase(.15f)]
    [TestCase(.5f)]
    [TestCase(1.2f)]
    public void VariationStaysWithinTheInteractionOrAttackRadius(float radius)
    {
        BuildNavigation(Floor(0, 12));
        Vector3 result = Around(origin, Vector3.right * 5, radius);
        Vector3 delta = result - origin;
        delta.y = 0;
        Assert.That(delta.magnitude, Is.GreaterThan(0).And.LessThanOrEqualTo(radius + .001f));
    }

    [Test]
    public void OffsetsShrinkAtEdgesInsteadOfJumpingToDisconnectedNavigation()
    {
        BuildNavigation(Floor(0, 3), Floor(4, 3));
        Vector3 result = Around(origin, Vector3.right * 4, 4);
        Assert.That(result.x, Is.LessThan(origin.x + 1.5f));
        Assert.That(NavMesh.SamplePosition(result, out _, .1f, NavMesh.AllAreas), Is.True);
    }

    [Test]
    public void MissingNavigationAndZeroRadiusKeepTheOriginalAnchor()
    {
        Vector3 anchor = new Vector3(30000, 42, 30000);
        Assert.That(Around(anchor, OffsetFor(1)), Is.EqualTo(anchor));
        BuildNavigation(Floor(0, 12));
        Assert.That(Around(origin, OffsetFor(1), 0), Is.EqualTo(origin));
    }

    [Test]
    public void GoalOffsetsStayInsideSmallScoringAndHealingTriggers()
    {
        BuildNavigation(Floor(0, 12));
        root = new GameObject("Bot goal positioning fixture");
        root.SetActive(false);
        root.transform.position = origin;
        var collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(1, 2, 1);
        collider.isTrigger = true;
        var goal = root.AddComponent(GameType("GoalZone"));
        var brain = root.AddComponent(GameType("BotBrain"));
        ((Behaviour)goal).enabled = false;
        ((Behaviour)brain).enabled = false;
        root.SetActive(true);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        brain.GetType().GetField("positionOffset", flags).SetValue(brain, Vector3.right * 1.15f);
        Physics.SyncTransforms();
        Vector3 result = (Vector3)brain.GetType().GetMethod("NearGoal", flags).Invoke(brain, new object[] { goal });
        Assert.That(result.x, Is.GreaterThan(origin.x).And.LessThan(origin.x + .5f));
        Assert.That(result.z, Is.InRange(origin.z - .5f, origin.z + .5f));
    }
}

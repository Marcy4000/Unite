using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class LightProbeGeneratorTests
{
    private static readonly Vector3 Center = new Vector3(20000f, 20000f, 20000f);
    private Type generatorType;
    private Component generator;
    private object area;
    private LightProbeGroup group;
    private GameObject root;
    private UnityEngine.Random.State randomState;
    private bool backfaces;

    [SetUp]
    public void SetUp()
    {
        generatorType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("LightProbeGenerator")).First(type => type != null);
        randomState = UnityEngine.Random.state;
        backfaces = Physics.queriesHitBackfaces;
        root = new GameObject("Light Probe Tests");
        var owner = new GameObject("Generator");
        owner.transform.SetParent(root.transform);
        owner.transform.position = Center;
        generator = owner.AddComponent(generatorType);
        group = owner.GetComponent<LightProbeGroup>();
        area = generatorType.GetField("LightProbeVolumes").GetValue(generator);
        SetArea("ProbeVolume", new Bounds(Center, Vector3.one * 4f));
        SetArea("Subdivisions", Vector3.one * 2f);
        Set("AvoidInsideMeshes", false);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        UnityEngine.Random.state = randomState;
        Physics.queriesHitBackfaces = backfaces;
    }

    [Test]
    public void SerializedSettingsPersist()
    {
        var serialized = new SerializedObject(generator);
        serialized.FindProperty("LightProbeVolumes").FindPropertyRelative("RandomCount").intValue = 23;
        serialized.FindProperty("SurfaceClearance").floatValue = 0.25f;
        serialized.ApplyModifiedProperties();
        serialized.Update();
        Assert.That(area.GetType().GetField("RandomCount").GetValue(area), Is.EqualTo(23));
        Assert.That(generatorType.GetField("SurfaceClearance").GetValue(generator), Is.EqualTo(0.25f));
    }

    [Test]
    public void RotatedGridRespectsWorldVolumeWithoutMovingGenerator()
    {
        generator.transform.position = Center + new Vector3(3f, 2f, -1f);
        generator.transform.rotation = Quaternion.Euler(17f, 28f, 39f);
        generator.transform.localScale = new Vector3(2f, 3f, 4f);
        Vector3 originalPosition = generator.transform.position;
        Quaternion rotation = Quaternion.Euler(10f, 45f, 20f);
        SetArea("Rotation", rotation);
        Generate();
        Assert.That(group.probePositions.Length, Is.EqualTo(27));
        Assert.That(generator.transform.position, Is.EqualTo(originalPosition));
        Assert.That(Vector3.Distance(generator.transform.TransformPoint(group.probePositions[0]),
            Center + rotation * (Vector3.one * -2f)), Is.LessThan(0.01f));
    }

    [TestCase(0)]
    [TestCase(500)]
    public void RandomUsesExactCandidateCountAndFullVolume(int count)
    {
        Set("PlacementAlgorithm", Enum.ToObject(generatorType.GetField("PlacementAlgorithm").FieldType, 1));
        SetArea("RandomCount", count);
        UnityEngine.Random.InitState(42);
        Generate();
        Assert.That(group.probePositions.Length, Is.EqualTo(count));
        if (count > 0)
        {
            Assert.That(group.probePositions.All(point => Mathf.Abs(point.x) <= 2.01f
                && Mathf.Abs(point.y) <= 2.01f && Mathf.Abs(point.z) <= 2.01f), Is.True);
            Assert.That(group.probePositions.Any(point => point.x > 1.5f), Is.True);
            Assert.That(group.probePositions.Any(point => point.x < -1.5f), Is.True);
        }
    }

    [Test]
    public void InvalidSubdivisionsAreClampedBeforeGeneration()
    {
        SetArea("Subdivisions", new Vector3(0f, -2f, 1.4f));
        Generate();
        Assert.That(group.probePositions.Length, Is.EqualTo(8));
        Assert.That(group.probePositions.All(point => float.IsFinite(point.x)
            && float.IsFinite(point.y) && float.IsFinite(point.z)), Is.True);
    }

    [Test]
    public void MeshWithoutColliderRejectsInteriorAndCleansUp()
    {
        GameObject obstacle = CreateCube(Vector3.zero, Vector3.one * 2f);
        Set("AvoidInsideMeshes", true);
        int sceneCount = SceneManager.sceneCount;
        bool originalBackfaces = Physics.queriesHitBackfaces;
        Generate();
        Assert.That(group.probePositions.Length, Is.EqualTo(26));
        Assert.That(group.probePositions.Any(point => point.sqrMagnitude < 0.01f), Is.False);
        Assert.That(obstacle.GetComponent<Collider>(), Is.Null);
        Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
        Assert.That(Physics.queriesHitBackfaces, Is.EqualTo(originalBackfaces));
    }

    [Test]
    public void MeshFilteringPreservesScaledRotatedHierarchy()
    {
        var parent = new GameObject("Scaled mesh parent");
        parent.transform.SetParent(root.transform);
        parent.transform.position = Center;
        parent.transform.rotation = Quaternion.Euler(20f, 35f, 10f);
        parent.transform.localScale = new Vector3(-2f, 3f, 1.5f);
        GameObject obstacle = CreateCube(Vector3.zero, Vector3.one * 2f);
        obstacle.transform.SetParent(parent.transform, false);
        obstacle.transform.localPosition = Vector3.zero;
        obstacle.transform.localRotation = Quaternion.Euler(15f, 40f, 5f);
        Set("AvoidInsideMeshes", true);
        SetArea("ProbeVolume", new Bounds(Center, Vector3.one * 0.5f));
        Generate();
        Assert.That(group.probePositions, Is.Empty);
    }

    [Test]
    public void SurfaceClearanceRejectsNearbyExteriorProbes()
    {
        CreateCube(Vector3.zero, Vector3.one * 2f);
        Set("AvoidInsideMeshes", true);
        Set("SurfaceClearance", 0.2f);
        SetArea("ProbeVolume", new Bounds(Center + Vector3.right * 1.1f, Vector3.zero));
        SetArea("Subdivisions", Vector3.one);
        Generate();
        Assert.That(group.probePositions, Is.Empty);
    }

    [Test]
    public void LayerMaskAndDisabledRenderersAreRespected()
    {
        GameObject obstacle = CreateCube(Vector3.zero, Vector3.one * 2f);
        Set("AvoidInsideMeshes", true);
        Set("ObstacleLayers", (LayerMask)0);
        Generate();
        Assert.That(group.probePositions.Length, Is.EqualTo(27));
        Set("ObstacleLayers", (LayerMask)(~0));
        obstacle.GetComponent<Renderer>().enabled = false;
        Generate();
        Assert.That(group.probePositions.Length, Is.EqualTo(27));
    }

    [Test]
    public void InwardFacingRoomDoesNotRejectEmptyInterior()
    {
        GameObject obstacle = CreateCube(Vector3.zero, Vector3.one * 10f);
        Mesh mesh = Object.Instantiate(obstacle.GetComponent<MeshFilter>().sharedMesh);
        try
        {
            int[] triangles = mesh.triangles;
            Array.Reverse(triangles);
            mesh.triangles = triangles;
            obstacle.GetComponent<MeshFilter>().sharedMesh = mesh;
            Set("AvoidInsideMeshes", true);
            Generate();
            Assert.That(group.probePositions.Length, Is.EqualTo(27));
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void ClearCanBeUndone()
    {
        Generate();
        Undo.IncrementCurrentGroup();
        generatorType.GetMethod("ClearProbes").Invoke(generator, null);
        Undo.FlushUndoRecordObjects();
        Assert.That(group.probePositions, Is.Empty);
        Undo.PerformUndo();
        Assert.That(group.probePositions.Length, Is.EqualTo(27));
    }

    private GameObject CreateCube(Vector3 offset, Vector3 scale)
    {
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.transform.SetParent(root.transform);
        obstacle.transform.position = Center + offset;
        obstacle.transform.localScale = scale;
        Object.DestroyImmediate(obstacle.GetComponent<Collider>());
        return obstacle;
    }

    private void Set(string name, object value) => generatorType.GetField(name).SetValue(generator, value);
    private void SetArea(string name, object value) => area.GetType().GetField(name).SetValue(area, value);
    private void Generate() => generatorType.GetMethod("GenProbes").Invoke(generator, null);
}

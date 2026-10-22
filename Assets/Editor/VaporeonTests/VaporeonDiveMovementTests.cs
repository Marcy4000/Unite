using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VaporeonDiveMovementTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private Scene scene;
    private Mesh groundedMesh;
    private MeshCollider grounded;
    private BoxCollider noWalls, physicalWall;
    private Component movement;
    private GameObject actor;
    private CharacterController controller;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private object Call(string method, params object[] args) => movement.GetType().GetMethod(method).Invoke(movement, args);
    private void Set(string field, object value) => movement.GetType().GetField(field, Private).SetValue(movement, value);
    private bool Traversing => (bool)movement.GetType().GetProperty("IsVaporeonDiveTraversing").GetValue(movement);
    private bool Valid => (bool)Call("IsVaporeonDiveLandingValid", actor.transform.position);
    private GameObject Node(string name)
    {
        var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go;
    }
    [SetUp]
    public void SetUp()
    {
        scene = EditorSceneManager.NewPreviewScene();
        groundedMesh = new Mesh(); SetTerrain(2, 6);
        var ground = Node("GroundedWalkmesh"); ground.transform.position = Vector3.down * 30;
        grounded = ground.AddComponent<MeshCollider>(); grounded.sharedMesh = groundedMesh;
        var bounds = Node("NoWallsWalkmesh"); bounds.transform.position = new Vector3(1.5f, -40.1f, 0);
        noWalls = bounds.AddComponent<BoxCollider>(); noWalls.size = new Vector3(9, 0.2f, 6);
        var wall = Node("PhysicalWall"); wall.transform.position = new Vector3(1, 1, 0);
        physicalWall = wall.AddComponent<BoxCollider>(); physicalWall.size = new Vector3(2, 3, 6);
        actor = Node("DivingVaporeon"); actor.transform.position = Vector3.left * 1.5f;
        actor.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        controller = actor.AddComponent<CharacterController>(); controller.center = Vector3.up;
        movement = actor.AddComponent(GameType("PlayerMovement"));
        Set("characterController", controller); Set("walkableWithWalls", grounded); Set("walkableNoWalls", noWalls); Set("activeWalkmesh", grounded);
        Physics.SyncTransforms();
    }
    private void SetTerrain(float farEdge, float end)
    {
        groundedMesh.vertices = new[] { new Vector3(-3, 0, -3), new Vector3(-3, 0, 3), new Vector3(0, 0, -3), new Vector3(0, 0, 3),
            new Vector3(farEdge, 0, -3), new Vector3(farEdge, 0, 3), new Vector3(end, 0, -3), new Vector3(end, 0, 3) };
        groundedMesh.triangles = new[] { 0, 1, 2, 2, 1, 3, 4, 5, 6, 6, 5, 7 }; groundedMesh.RecalculateBounds();
        if (grounded != null) { grounded.sharedMesh = null; grounded.sharedMesh = groundedMesh; }
    }
    [TearDown]
    public void TearDown()
    {
        EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(groundedMesh);
    }
    [Test]
    public void DiveCrossesPhysicalAndWalkmeshWallsWithoutChangingRootHeight()
    {
        Call("BeginVaporeonDiveTraversal"); Assert.That(Traversing, Is.True);
        Assert.That(movement.GetType().GetField("activeWalkmesh", Private).GetValue(movement), Is.SameAs(noWalls));
        Call("MoveVaporeonDive", new Vector3(4.5f, -99, 0));
        Assert.That(actor.transform.position.x, Is.EqualTo(3).Within(0.001f)); Assert.That(actor.transform.position.y, Is.Zero);
        Call("EndVaporeonDiveTraversal", true);
        Assert.That(Valid, Is.True); Assert.That(Traversing, Is.False);
        Assert.That(movement.GetType().GetField("activeWalkmesh", Private).GetValue(movement), Is.SameAs(grounded));
    }
    [Test]
    public void ResurfacingInsideTerrainChoosesTheNearbyFarSideWithRoomForTheController()
    {
        Call("BeginVaporeonDiveTraversal"); Call("MoveVaporeonDive", Vector3.right * 3.3f);
        Assert.That(Valid, Is.False);
        Call("EndVaporeonDiveTraversal", true);
        Assert.That(actor.transform.position.x, Is.GreaterThan(2.5f)); Assert.That(Valid, Is.True);
    }
    [Test]
    public void ThickTerrainFallsBackToTheLastClearPointRatherThanLeavingVaporeonStuck()
    {
        SetTerrain(8, 12); noWalls.size = new Vector3(15, 0.2f, 6); noWalls.transform.position = new Vector3(4.5f, -40.1f, 0);
        physicalWall.size = new Vector3(8, 3, 6); physicalWall.transform.position = new Vector3(4, 1, 0); Physics.SyncTransforms();
        Call("BeginVaporeonDiveTraversal"); Call("MoveVaporeonDive", Vector3.right * 5);
        Assert.That(Valid, Is.False);
        var safe = (Vector3)movement.GetType().GetField("vaporeonDiveLastSafe", Private).GetValue(movement);
        Assert.That(safe.x, Is.GreaterThan(-1));
        Call("EndVaporeonDiveTraversal", true);
        Assert.That(actor.transform.position, Is.EqualTo(safe)); Assert.That(Valid, Is.True);
    }
    [Test]
    public void DiveCannotLeaveTheMapAndEmergesInsideItsBoundary()
    {
        Call("BeginVaporeonDiveTraversal"); Call("MoveVaporeonDive", Vector3.right * 20);
        Assert.That(actor.transform.position.x, Is.LessThanOrEqualTo(6.001f));
        Call("EndVaporeonDiveTraversal", true);
        Assert.That(Valid, Is.True); Assert.That(actor.transform.position.x, Is.LessThan(5.5f));
    }
    [Test]
    public void DiveCannotSkipAGapInTheNoWallsMeshEvenInOneLargeFrame()
    {
        var bounds = Node("SplitNoWallsWalkmesh"); bounds.transform.position = Vector3.down * 40;
        var collider = bounds.AddComponent<MeshCollider>(); collider.sharedMesh = groundedMesh;
        Set("walkableNoWalls", collider); Physics.SyncTransforms();
        Call("BeginVaporeonDiveTraversal"); Call("MoveVaporeonDive", Vector3.right * 4.5f);
        Assert.That(actor.transform.position.x, Is.LessThanOrEqualTo(0.001f));
        Call("EndVaporeonDiveTraversal", true); Assert.That(Valid, Is.True);
    }
    [Test]
    public void RepeatedBeginAndCleanupAreIdempotentAndEachDiveCanCrossBack()
    {
        Call("BeginVaporeonDiveTraversal"); Call("BeginVaporeonDiveTraversal");
        Call("MoveVaporeonDive", Vector3.right * 4.5f); Call("EndVaporeonDiveTraversal", true); Call("EndVaporeonDiveTraversal", true);
        Assert.That(actor.transform.position.x, Is.EqualTo(3).Within(0.001f));
        Call("BeginVaporeonDiveTraversal"); Call("MoveVaporeonDive", Vector3.left * 4.5f); Call("EndVaporeonDiveTraversal", true);
        Assert.That(actor.transform.position.x, Is.EqualTo(-1.5f).Within(0.001f)); Assert.That(Valid, Is.True);
        Assert.That(Call("MoveVaporeonDive", Vector3.right * 4), Is.EqualTo(Vector3.zero));
    }
    [TestCase(true, true)]
    [TestCase(false, false)]
    public void DivePreservesControllerFlightSnapAndOtherDashState(bool enabled, bool snap)
    {
        controller.enabled = enabled;
        movement.GetType().GetProperty("IsFlying").SetValue(movement, true);
        movement.GetType().GetProperty("SnapToGround").SetValue(movement, snap);
        Set("isDashing", true);
        Call("BeginVaporeonDiveTraversal"); Call("MoveVaporeonDive", Vector3.right * 4.5f); Call("EndVaporeonDiveTraversal", true);
        Assert.That(controller.enabled, Is.EqualTo(enabled));
        Assert.That(movement.GetType().GetProperty("IsFlying").GetValue(movement), Is.True);
        Assert.That(movement.GetType().GetProperty("IsDashing").GetValue(movement), Is.True);
        Assert.That(movement.GetType().GetProperty("SnapToGround").GetValue(movement), Is.EqualTo(snap));
    }
    [Test]
    public void MissingWalkmeshDataNeverEnablesUnboundedWallPhasing()
    {
        Set("walkableNoWalls", null); Call("BeginVaporeonDiveTraversal");
        Assert.That(Traversing, Is.False); Assert.That(Call("MoveVaporeonDive", Vector3.right * 100), Is.EqualTo(Vector3.zero));
        Assert.That(actor.transform.position.x, Is.EqualTo(-1.5f));
    }
}

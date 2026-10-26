using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WallCrossingTraversalTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private Scene scene;
    private Mesh groundedMesh;
    private MeshCollider grounded;
    private BoxCollider noWalls;
    private Component movement;
    private CharacterController controller;
    private GameObject actor;
    private object traversal;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private void Set(string field, object value) => movement.GetType().GetField(field, Private).SetValue(movement, value);
    private bool Valid => (bool)Call(movement, "IsWallCrossingLandingValid", actor.transform.position);
    private void Begin() => Call(traversal, "Begin", movement);
    private Vector3 Move(Vector3 step) => (Vector3)Call(traversal, "Move", step);
    private void End() => Call(traversal, "End");
    private GameObject Node(string name)
    {
        var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go;
    }

    [SetUp]
    public void SetUp()
    {
        scene = EditorSceneManager.NewPreviewScene();
        groundedMesh = new Mesh
        {
            vertices = new[] { new Vector3(-3, 0, -3), new Vector3(-3, 0, 3), new Vector3(0, 0, -3), new Vector3(0, 0, 3),
                new Vector3(2, 0, -3), new Vector3(2, 0, 3), new Vector3(6, 0, -3), new Vector3(6, 0, 3) },
            triangles = new[] { 0, 1, 2, 2, 1, 3, 4, 5, 6, 6, 5, 7 }
        };
        groundedMesh.RecalculateBounds();
        var ground = Node("GroundedWalkmesh"); ground.transform.position = Vector3.down * 30;
        grounded = ground.AddComponent<MeshCollider>(); grounded.sharedMesh = groundedMesh;
        var bounds = Node("NoWallsWalkmesh"); bounds.transform.position = new Vector3(1.5f, -40.1f, 0);
        noWalls = bounds.AddComponent<BoxCollider>(); noWalls.size = new Vector3(9, 0.2f, 6);
        var wall = Node("PhysicalWall"); wall.transform.position = new Vector3(1, 1, 0);
        wall.AddComponent<BoxCollider>().size = new Vector3(2, 3, 6);
        actor = Node("Dasher"); actor.transform.position = Vector3.left;
        actor.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        controller = actor.AddComponent<CharacterController>(); controller.center = Vector3.up;
        movement = actor.AddComponent(GameType("PlayerMovement"));
        Set("characterController", controller); Set("walkableWithWalls", grounded); Set("walkableNoWalls", noWalls); Set("activeWalkmesh", grounded);
        traversal = Activator.CreateInstance(GameType("WallCrossingTraversal"));
        Physics.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(groundedMesh);
    }

    [TestCase("EmboarSmog")]
    [TestCase("EmboarHeatCrash")]
    [TestCase("EmboarUnite")]
    [TestCase("VaporRainDance")]
    [TestCase("FlygonEarthquake")]
    [TestCase("JoltThunderFang")]
    [TestCase("MarshadowAuraSphere")]
    [TestCase("FlareonTackle")]
    [TestCase("FlareonTakedown")]
    [TestCase("FlareonUnite")]
    public void RequestedMovesOwnTraversalThatCrossesPhysicalAndWalkmeshWalls(string moveType)
    {
        object move = Activator.CreateInstance(GameType(moveType));
        traversal = null;
        for (Type type = move.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField("traversal", Private | BindingFlags.DeclaredOnly);
            if (field != null) { traversal = field.GetValue(move); break; }
        }
        Assert.That(traversal, Is.Not.Null);
        Begin();
        Move(Vector3.right * 2);
        Assert.That(actor.transform.position.x, Is.EqualTo(1).Within(0.001f), "Must pass through terrain, not slide along it.");
        Assert.That(Valid, Is.False);
        Move(Vector3.right * 2); End();
        Assert.That(actor.transform.position.x, Is.EqualTo(3).Within(0.001f));
        Assert.That(Valid, Is.True);
    }

    [TestCase("Tackle", true)]
    [TestCase("Slam", true)]
    [TestCase("Unite", true)]
    [TestCase("Takedown", false)]
    [TestCase("FlareBlitz", false)]
    public void FlareonOnlyChangesRequestedMovesAndTakedownSecondHit(string kind, bool crosses)
    {
        object move = Activator.CreateInstance(GameType("FlareonTakedown"));
        Type type = GameType("FlareonMoveBase");
        type.GetField("executingKind", Private).SetValue(move, Enum.Parse(GameType("FlareonCast"), kind));
        Assert.That(type.GetProperty("CrossesWalls", Private).GetValue(move), Is.EqualTo(crosses));
    }

    [Test]
    public void GroundTargetDestinationSearchesPastWallsWithoutExtendingRange()
    {
        var requested = new Vector3(3, 0, 0);
        Assert.That(Call(movement, "GetWallCrossingDestination", requested), Is.EqualTo(requested));
        var shortHop = (Vector3)Call(movement, "GetWallCrossingDestination", new Vector3(1, 0, 0));
        Assert.That(shortHop.x, Is.InRange(-1, -0.5f));
        Assert.That(shortHop.x, Is.LessThanOrEqualTo(1), "Do not extend a dash to reach the far side.");
    }

    [Test]
    public void RainDanceDestinationCanCrossWallsAndStillClampsToFourMeters()
    {
        var destination = (Vector3)Call(movement, "VaporeonHopDestination", Vector3.right * 100);
        Assert.That(destination, Is.EqualTo(new Vector3(3, 0, 0)));
        Assert.That(Vector3.Distance(actor.transform.position, destination), Is.EqualTo(4));
    }

    [Test]
    public void InterruptedOrShortDashReturnsToLastSafePositionWithControllerClearance()
    {
        Begin(); Move(Vector3.right * 0.4f); Vector3 safe = actor.transform.position;
        Move(Vector3.right * 2); Assert.That(Valid, Is.False);
        End();
        Assert.That(actor.transform.position, Is.EqualTo(safe)); Assert.That(Valid, Is.True);
        actor.transform.position = Vector3.right * 3; End();
        Assert.That(actor.transform.position.x, Is.EqualTo(3), "Repeated cleanup must not move a subsequent action.");
        Assert.That(Move(Vector3.right), Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void FastStepAndDestinationSearchCannotSkipHolesInMapBoundary()
    {
        var bounds = Node("SplitNoWallsWalkmesh"); bounds.transform.position = Vector3.down * 40;
        var split = bounds.AddComponent<MeshCollider>(); split.sharedMesh = groundedMesh;
        Set("walkableNoWalls", split); Physics.SyncTransforms();
        var destination = (Vector3)Call(movement, "GetWallCrossingDestination", Vector3.right * 3);
        Assert.That(destination.x, Is.LessThan(-0.5f));
        Begin(); Move(Vector3.right * 4);
        Assert.That(actor.transform.position.x, Is.LessThanOrEqualTo(0.001f));
        End(); Assert.That(Valid, Is.True);
    }

    [Test]
    public void MapEdgeClampsTravelAndFinalLandingHasRoomForTheCharacter()
    {
        Begin(); Move(Vector3.right * 20);
        Assert.That(actor.transform.position.x, Is.LessThanOrEqualTo(6.001f));
        End(); Assert.That(Valid, Is.True);
        Assert.That(actor.transform.position.x, Is.InRange(5, 5.5f));
    }

    [Test]
    public void HomingStepsCanChangeDirectionAndRecrossTheWall()
    {
        Begin(); Move(Vector3.right * 4); Move(Vector3.forward * 0.5f); Move(Vector3.left * 4); End();
        Assert.That(actor.transform.position, Is.EqualTo(new Vector3(-1, 0, 0.5f)));
        Assert.That(Valid, Is.True);
        Begin(); Move(Vector3.right * 4); End();
        Assert.That(actor.transform.position, Is.EqualTo(new Vector3(3, 0, 0.5f)));
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void TraversalPreservesControllerFlightSnapAndExistingDashState(bool enabled, bool snap)
    {
        controller.enabled = enabled;
        movement.GetType().GetProperty("IsFlying").SetValue(movement, true);
        movement.GetType().GetProperty("SnapToGround").SetValue(movement, snap);
        Set("isDashing", true);
        object mesh = movement.GetType().GetField("activeWalkmesh", Private).GetValue(movement);
        Begin(); Move(new Vector3(4, -99, 0)); End();
        Assert.That(actor.transform.position, Is.EqualTo(new Vector3(3, 0, 0)));
        Assert.That(controller.enabled, Is.EqualTo(enabled));
        Assert.That(movement.GetType().GetProperty("IsFlying").GetValue(movement), Is.True);
        Assert.That(movement.GetType().GetProperty("IsDashing").GetValue(movement), Is.True);
        Assert.That(movement.GetType().GetProperty("SnapToGround").GetValue(movement), Is.EqualTo(snap));
        Assert.That(movement.GetType().GetField("activeWalkmesh", Private).GetValue(movement), Is.SameAs(mesh));
    }

    [TestCase("walkableNoWalls")]
    [TestCase("walkableWithWalls")]
    public void MissingWalkmeshNeverAllowsUnboundedMovement(string field)
    {
        Set(field, null); Begin();
        Assert.That(Move(Vector3.right * 100), Is.EqualTo(Vector3.zero)); End();
        Assert.That(actor.transform.position, Is.EqualTo(Vector3.left));
    }

    [Test]
    public void InvalidRequestsDoNotMoveTheActor()
    {
        Begin(); Assert.That(Move(new Vector3(float.NaN, 0, 0)), Is.EqualTo(Vector3.zero));
        Assert.That(Call(movement, "GetWallCrossingDestination", new Vector3(float.PositiveInfinity, 0, 0)), Is.EqualTo(Vector3.left));
        End(); Assert.That(actor.transform.position, Is.EqualTo(Vector3.left));
    }
}

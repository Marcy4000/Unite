using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MarshadowWallDashTests
{
    private Scene scene;
    private Mesh groundedMesh;
    private Component movement;
    private GameObject actor;
    private CharacterController controller;
    private object dash;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private void SetMovementField(string name, object value) => GameType("PlayerMovement").GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, value);
    private bool ValidLanding() => (bool)Call(movement, "IsMarshadowDashLandingValid", actor.transform.position);
    private GameObject Node(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    [SetUp]
    public void SetUp()
    {
        scene = EditorSceneManager.NewPreviewScene();
        // Two grounded platforms separated by terrain at x=0..2. The no-walls
        // surface spans the gap, just like the project's paired walkmeshes.
        groundedMesh = new Mesh
        {
            vertices = new[] { new Vector3(-2, 0, -2), new Vector3(-2, 0, 2), new Vector3(0, 0, -2), new Vector3(0, 0, 2),
                new Vector3(2, 0, -2), new Vector3(2, 0, 2), new Vector3(5, 0, -2), new Vector3(5, 0, 2) },
            triangles = new[] { 0, 1, 2, 2, 1, 3, 4, 5, 6, 6, 5, 7 }
        };
        groundedMesh.RecalculateBounds();
        var ground = Node("GroundedWalkmesh"); ground.transform.position = Vector3.down * 30;
        var grounded = ground.AddComponent<MeshCollider>(); grounded.sharedMesh = groundedMesh;
        var bounds = Node("NoWallsWalkmesh"); bounds.transform.position = new Vector3(1.5f, -40.1f, 0);
        var noWalls = bounds.AddComponent<BoxCollider>(); noWalls.size = new Vector3(7, 0.2f, 4);
        var wall = Node("PhysicalWall"); wall.transform.position = new Vector3(1, 1, 0);
        wall.AddComponent<BoxCollider>().size = new Vector3(2, 3, 4);
        actor = Node("Dasher"); actor.transform.position = Vector3.left;
        actor.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        controller = actor.AddComponent<CharacterController>(); controller.center = Vector3.up;
        movement = actor.AddComponent(GameType("PlayerMovement"));
        SetMovementField("characterController", controller);
        SetMovementField("walkableWithWalls", grounded); SetMovementField("walkableNoWalls", noWalls);
        SetMovementField("activeWalkmesh", grounded);
        dash = Activator.CreateInstance(GameType("MarshadowWallDash"));
        Physics.SyncTransforms();
    }
    [TearDown]
    public void TearDown()
    {
        EditorSceneManager.ClosePreviewScene(scene);
        UnityEngine.Object.DestroyImmediate(groundedMesh);
    }

    [TestCase("MarshadowSpectralThief", 4f)]
    [TestCase("MarshadowCloseCombat", 3.5f)]
    public void BothMovesCrossPhysicalAndWalkmeshWallsWithoutChangingTheirRange(string moveType, float range)
    {
        object move = Activator.CreateInstance(GameType(moveType));
        dash = move.GetType().GetField("dash", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(move);
        Call(dash, "Begin", movement, Vector3.right, range);
        Assert.That(dash.GetType().GetProperty("Destination").GetValue(dash), Is.EqualTo(Vector3.left + Vector3.right * range));
        Call(dash, "Advance", range / 2);
        Assert.That(actor.transform.position.x, Is.InRange(0.5f, 1f), "The dash must be able to pass inside terrain.");
        Call(dash, "Advance", range / 2); Call(dash, "End");
        Assert.That(actor.transform.position.x, Is.EqualTo(range - 1f).Within(0.001f));
        Assert.That(ValidLanding(), Is.True);
        Assert.That(controller.enabled, Is.True);
    }

    [Test]
    public void DashThatCannotReachTheFarSideStopsBeforeTheWall()
    {
        Call(dash, "Begin", movement, Vector3.right, 2f);
        Call(dash, "Advance", 2f); Call(dash, "End");
        Assert.That(actor.transform.position.x, Is.InRange(-1f, 0.001f));
        Assert.That(ValidLanding(), Is.True);
    }
    [Test]
    public void DashCannotLeaveTheMap()
    {
        Call(dash, "Begin", movement, Vector3.right, 10f);
        Call(dash, "Advance", 10f); Call(dash, "End");
        Assert.That(actor.transform.position.x, Is.InRange(4.8f, 5.001f));
        Assert.That(ValidLanding(), Is.True);
    }
    [Test]
    public void DashDoesNotJumpAcrossAHoleInTheNoWallsBoundary()
    {
        var bounds = Node("SplitNoWallsWalkmesh"); bounds.transform.position = Vector3.down * 40;
        var collider = bounds.AddComponent<MeshCollider>(); collider.sharedMesh = groundedMesh;
        SetMovementField("walkableNoWalls", collider); Physics.SyncTransforms();
        Call(dash, "Begin", movement, Vector3.right, 4f);
        Call(dash, "Advance", 4f); Call(dash, "End");
        Assert.That(actor.transform.position.x, Is.InRange(-1f, 0.001f));
        Assert.That(ValidLanding(), Is.True);
    }
    [Test]
    public void InterruptedDashReturnsToItsLastSafePointAndCleanupIsIdempotent()
    {
        Call(dash, "Begin", movement, Vector3.right, 4f);
        Call(dash, "Advance", 0.5f); Vector3 safe = actor.transform.position;
        Call(dash, "Advance", 1f);
        Assert.That(ValidLanding(), Is.False);
        Call(dash, "End");
        Assert.That(actor.transform.position, Is.EqualTo(safe));
        actor.transform.position = new Vector3(3, 0, 0); Call(dash, "End");
        Assert.That(actor.transform.position.x, Is.EqualTo(3f));
    }
    [Test]
    public void EachCloseCombatRecastCanCrossTheWallInADifferentDirection()
    {
        foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.right })
        {
            Call(dash, "Begin", movement, direction, 3.5f);
            Call(dash, "Advance", 3.5f); Call(dash, "End");
            Assert.That(ValidLanding(), Is.True);
        }
        Assert.That(actor.transform.position.x, Is.EqualTo(2.5f).Within(0.001f));
    }
    [TestCase(true)]
    [TestCase(false)]
    public void DashDoesNotChangeControllerFlightOrSharedDashState(bool enabled)
    {
        controller.enabled = enabled;
        movement.GetType().GetProperty("IsFlying").SetValue(movement, true);
        Call(dash, "Begin", movement, Vector3.right, 4f);
        Call(dash, "Advance", 4f); Call(dash, "End");
        Assert.That(controller.enabled, Is.EqualTo(enabled));
        Assert.That(movement.GetType().GetProperty("IsFlying").GetValue(movement), Is.True);
        Assert.That(movement.GetType().GetProperty("IsDashing").GetValue(movement), Is.False);
    }
}

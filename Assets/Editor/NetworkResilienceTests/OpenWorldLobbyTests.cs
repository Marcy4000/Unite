using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unite.Multiplayer;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEditor;

public class OpenWorldLobbyTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [Test]
    public void SocialDiscoveryCannotMatchNormalMatchLobbies()
    {
        Assert.That(OpenWorldLobbyRules.Version, Is.Not.EqualTo(SessionCompatibility.Current));
        Assert.That(OpenWorldLobbyRules.SessionType, Is.Not.EqualTo("UniteGame"));
        Assert.That(OpenWorldLobbyRules.IsCompatible(new Dictionary<string, SessionProperty>
        {
            [SessionCompatibility.PropertyName] = new SessionProperty(SessionCompatibility.Current)
        }), Is.False);
    }

    [Test]
    public void SocialDiscoveryRequiresStyleAndVersion()
    {
        var properties = new Dictionary<string, SessionProperty>
        {
            [SessionCompatibility.PropertyName] = new SessionProperty(OpenWorldLobbyRules.Version),
            [OpenWorldLobbyRules.StyleProperty] = new SessionProperty(OpenWorldLobbyRules.Style)
        };
        Assert.That(OpenWorldLobbyRules.IsCompatible(properties), Is.True);
        properties.Remove(OpenWorldLobbyRules.StyleProperty);
        Assert.That(OpenWorldLobbyRules.IsCompatible(properties), Is.False);
        Assert.That(OpenWorldLobbyRules.IsCompatible(null), Is.False);
    }

    [Test]
    public void SocialQuickJoinFiltersRequireSpaceAndUnlockedCity()
    {
        var filters = OpenWorldLobbyRules.BrowserFilters();
        Assert.That(filters.Count, Is.EqualTo(4));
        Assert.That(filters.Any(filter => filter.Field == FilterField.AvailableSlots && filter.Value == "0" &&
            filter.Operation == FilterOperation.Greater), Is.True);
        Assert.That(filters.Any(filter => filter.Field == FilterField.IsLocked && filter.Value == "0"), Is.True);
        Assert.That(filters.Any(filter => filter.Field == FilterField.StringIndex1 && filter.Value == OpenWorldLobbyRules.Version), Is.True);
        Assert.That(filters.Any(filter => filter.Field == FilterField.StringIndex2 && filter.Value == OpenWorldLobbyRules.Style), Is.True);
    }

    [TestCase("ChineseLobby", true)]
    [TestCase("LobbyScene", true)]
    [TestCase("BootScene", false)]
    [TestCase("CharacterSelect", false)]
    [TestCase("RemoatStadium", false)]
    public void OnlyHomeScenesAreRecognizedAsHome(string name, bool expected)
    {
        Assert.That(GameType("LobbyController").GetMethod("IsHomeScene").Invoke(null, new object[] { name }), Is.EqualTo(expected));
    }

    [Test]
    public void MigrationRestoresOnlyValidStablePlayerRecords()
    {
        var root = new GameObject("City migration fixture");
        try
        {
            var type = GameType("OpenWorldLobbyController");
            var controller = root.AddComponent(type);
            var json = "{\"version\":1,\"trainers\":[{\"playerId\":\"trainer-a\",\"position\":{\"x\":2,\"y\":0.3,\"z\":5},\"yaw\":90}," +
                "{\"playerId\":\"\",\"position\":{\"x\":1,\"y\":0,\"z\":1},\"yaw\":0}," +
                "{\"playerId\":\"fallen\",\"position\":{\"x\":0,\"y\":-99,\"z\":0},\"yaw\":0}]}";
            type.GetMethod("Apply").Invoke(controller, new object[] { Encoding.UTF8.GetBytes(json) });
            var bytes = (byte[])type.GetMethod("Generate").Invoke(controller, null);
            var restored = Encoding.UTF8.GetString(bytes);
            StringAssert.Contains("trainer-a", restored);
            StringAssert.DoesNotContain("fallen", restored);
            type.GetMethod("Apply").Invoke(controller, new object[] { Encoding.UTF8.GetBytes("{\"version\":2,\"trainers\":[]}") });
            Assert.That((byte[])type.GetMethod("Generate").Invoke(controller, null), Is.EqualTo(bytes));
            type.GetMethod("Apply").Invoke(controller, new object[] { new byte[65537] });
            Assert.That((byte[])type.GetMethod("Generate").Invoke(controller, null), Is.EqualTo(bytes));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [TestCase(float.NaN, 0f, 0f, 0f, false)]
    [TestCase(0f, 0f, float.PositiveInfinity, 0f, false)]
    [TestCase(0f, 0f, 0f, float.NaN, false)]
    [TestCase(0f, -40f, 0f, 0f, false)]
    [TestCase(2f, 0.3f, 5f, 90f, true)]
    public void MigrationRejectsNonFiniteOrOutOfBoundsPoses(float x, float y, float z, float yaw, bool expected)
    {
        var type = GameType("OpenWorldLobbyController");
        var record = Activator.CreateInstance(type.GetNestedType("TrainerPosition"));
        record.GetType().GetField("playerId").SetValue(record, "trainer-a");
        record.GetType().GetField("position").SetValue(record, new Vector3(x, y, z));
        record.GetType().GetField("yaw").SetValue(record, yaw);
        Assert.That(type.GetMethod("IsValidPosition").Invoke(null, new[] { record }), Is.EqualTo(expected));
    }

    [Test]
    public void LateRosterConnectionsWaitInsteadOfBeingRejected()
    {
        WithAdmissionController((controller, approve) =>
        {
            var response = Approve(controller, approve, 4, "late-trainer");
            Assert.That(response.GetType().GetField("Pending").GetValue(response), Is.True);
            Assert.That(response.GetType().GetField("Approved").GetValue(response), Is.False);
            Assert.That(response.GetType().GetField("CreatePlayerObject").GetValue(response), Is.False);
        });
    }

    [Test]
    public void HostApprovalCannotWaitForItsOwnSessionCreation()
    {
        WithAdmissionController((controller, approve) =>
        {
            var response = Approve(controller, approve, 0, "host-trainer");
            Assert.That(response.GetType().GetField("Pending").GetValue(response), Is.False);
            Assert.That(response.GetType().GetField("Approved").GetValue(response), Is.True);
        });
    }

    [Test]
    public void DuplicateOrInvalidAdmissionCannotClaimAnotherAvatar()
    {
        WithAdmissionController((controller, approve) =>
        {
            Approve(controller, approve, 4, "trainer-a");
            foreach (var id in new[] { "trainer-a", "", new string('x', 129) })
            {
                var response = Approve(controller, approve, 5, id);
                Assert.That(response.GetType().GetField("Pending").GetValue(response), Is.False);
                Assert.That(response.GetType().GetField("Approved").GetValue(response), Is.False);
            }
        });
    }

    [Test]
    public void CancelingCityEntryStopsAdmissionEvenBeforeNetworkingStarts()
    {
        WithAdmissionController((controller, approve) =>
        {
            controller.GetType().GetMethod("CancelPendingConnection").Invoke(controller, null);
            var response = Approve(controller, approve, 0, "host-trainer");
            Assert.That(response.GetType().GetField("Pending").GetValue(response), Is.False);
            Assert.That(response.GetType().GetField("Approved").GetValue(response), Is.False);
        });
    }

    [Test]
    public void TrainerMovementUsesNormalizedCameraRelativeInput()
    {
        var method = GameType("OpenWorldTrainerAvatar").GetMethod("CalculatePlanarMovement", BindingFlags.Static | BindingFlags.NonPublic);
        var forward = (Vector3)method.Invoke(null, new object[] { Vector2.up, 90f });
        Assert.That(Vector3.Distance(forward, Vector3.right), Is.LessThan(0.0001f));
        var diagonal = (Vector3)method.Invoke(null, new object[] { new Vector2(5f, 5f), 0f });
        Assert.That(diagonal.magnitude, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void SocialAvatarAndSceneAreRegisteredWithoutChangingBootScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldTrainer.prefab");
        Assert.That(prefab, Is.Not.Null);
        var components = prefab.GetComponents<Component>().Select(component => component.GetType().Name).ToArray();
        Assert.That(components, Does.Contain("NetworkObject"));
        Assert.That(components, Does.Contain("ClientNetworkTransform"));
        Assert.That(components, Does.Contain("OpenWorldTrainerAvatar"));
        Assert.That(components, Does.Not.Contain("PlayerNetworkManager"));
        Assert.That(components, Does.Not.Contain("PlayerManager"));
        Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null);
        var list = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/DefaultNetworkPrefabs.asset"));
        var entries = list.FindProperty("List");
        Assert.That(Enumerable.Range(0, entries.arraySize).Any(index =>
            entries.GetArrayElementAtIndex(index).FindPropertyRelative("Prefab").objectReferenceValue == prefab), Is.True);
        Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo("Assets/Scenes/BootScene.unity"));
        Assert.That(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == "Assets/Scenes/ChineseLobby.unity"), Is.True);
    }

    private static void WithAdmissionController(Action<Component, MethodInfo> test)
    {
        var root = new GameObject("City admission fixture");
        try
        {
            var type = GameType("OpenWorldLobbyController");
            var controller = root.AddComponent(type);
            type.GetField("localPlayer", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, new GameSessionPlayer("host-trainer"));
            test(controller, type.GetMethod("ApproveConnection", BindingFlags.Instance | BindingFlags.NonPublic));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static object Approve(Component controller, MethodInfo method, ulong clientId, string playerId)
    {
        var request = Activator.CreateInstance(method.GetParameters()[0].ParameterType);
        request.GetType().GetField("Payload").SetValue(request, Encoding.UTF8.GetBytes(playerId));
        request.GetType().GetField("ClientNetworkId").SetValue(request, clientId);
        var response = Activator.CreateInstance(method.GetParameters()[1].ParameterType);
        method.Invoke(controller, new[] { request, response });
        return response;
    }
}

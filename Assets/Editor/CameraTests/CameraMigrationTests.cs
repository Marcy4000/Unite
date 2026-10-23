using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class CameraMigrationTests
{
    private const string PrefabPath = "Assets/Prefabs/DefaultObjects.prefab";
    private static readonly Vector3 NormalOffset = new Vector3(0f, 18.85f, -18.6f);
    private static Type ControllerType => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType("CameraController")).First(type => type != null);

    [Test]
    public void SharedRigUsesNativeCinemachine3ComponentsAndPreservesSettings()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var camera = prefab.GetComponentsInChildren<CinemachineCamera>(true).Single();
        var follow = camera.GetComponent<CinemachineFollow>();
        var brain = prefab.GetComponentsInChildren<CinemachineBrain>(true).Single();
        var controller = prefab.GetComponentInChildren(ControllerType, true);

        Assert.That(follow, Is.Not.Null);
        Assert.That(new SerializedObject(controller).FindProperty("virtualCamera").objectReferenceValue, Is.EqualTo(camera));
        Assert.That(new SerializedObject(controller).FindProperty("panSpeed").floatValue, Is.EqualTo(15f));
        Assert.That(camera.Lens.FieldOfView, Is.EqualTo(25f));
        Assert.That(camera.Priority.Value, Is.EqualTo(10));
        Assert.That(follow.FollowOffset, Is.EqualTo(NormalOffset));
        Assert.That(follow.TrackerSettings.BindingMode, Is.EqualTo(BindingMode.WorldSpace));
        Assert.That(follow.TrackerSettings.PositionDamping, Is.EqualTo(Vector3.zero));
        Assert.That(follow.TrackerSettings.RotationDamping, Is.EqualTo(Vector3.zero));
        Assert.That(Quaternion.Angle(camera.transform.localRotation, Quaternion.Euler(45f, 0f, 0f)), Is.LessThan(0.001f));
        Assert.That(camera.GetComponents<CinemachineComponentBase>().Any(component => component.Stage == CinemachineCore.Stage.Aim), Is.False);
        Assert.That(camera.transform.Find("cm"), Is.Null);
        Assert.That(brain.UpdateMethod, Is.EqualTo(CinemachineBrain.UpdateMethods.SmartUpdate));
        Assert.That(brain.BlendUpdateMethod, Is.EqualTo(CinemachineBrain.BrainUpdateMethods.LateUpdate));
        Assert.That((brain.ChannelMask & camera.OutputChannel) != 0, Is.True);
        Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).All(component => component != null), Is.True);
    }

    [Test]
    public void MobilePanAreaIsAuthoredInTheMobileHudBehindMoveButtons()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var area = prefab.transform.Find("MobileControls/MovesHolderMobile/MoveButtons/CameraPanArea");
        Assert.That(area, Is.Not.Null);
        Assert.That(area.GetSiblingIndex(), Is.Zero);
        var rect = area.GetComponent<RectTransform>();
        Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.65f, 0.48f)));
        Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.98f, 0.82f)));
        var image = area.GetComponent<Image>();
        Assert.That(image.raycastTarget, Is.True);
        Assert.That(image.color.a, Is.Zero);
        var handlerType = Type.GetType("MobileCameraPanArea, Assembly-CSharp", true);
        var handler = area.GetComponent(handlerType);
        Assert.That(handler, Is.Not.Null);
        var controller = prefab.GetComponentInChildren(ControllerType, true);
        Assert.That(new SerializedObject(handler).FindProperty("cameraController").objectReferenceValue, Is.EqualTo(controller));
        Assert.That(new SerializedObject(controller).FindProperty("touchPanSensitivity").floatValue, Is.EqualTo(30f));
    }

    [TestCase("AuromaPark", 25f)]
    [TestCase("ElectrodeVolleyball", 25f)]
    [TestCase("MerStadium", 25f)]
    [TestCase("PsyduckRacing", 30f)]
    [TestCase("RemoatStadium", 25f)]
    [TestCase("RemoatStadium500Points", 25f)]
    [TestCase("SampleScene", 25f)]
    [TestCase("TheiaSkyRuins", 25f)]
    [TestCase("UniteRoyale", 25f)]
    public void EverySharedRigSceneResolvesTheCameraAndItsOverrides(string name, float expectedFOV)
    {
        string path = "Assets/Scenes/" + name + ".unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
        if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var camera = roots.SelectMany(root => root.GetComponentsInChildren<CinemachineCamera>(true)).Single();
            var controller = roots.SelectMany(root => root.GetComponentsInChildren(ControllerType, true)).Single();
            var brain = roots.SelectMany(root => root.GetComponentsInChildren<CinemachineBrain>(true)).Single();
            Assert.That(new SerializedObject(controller).FindProperty("virtualCamera").objectReferenceValue, Is.EqualTo(camera));
            Assert.That(camera.Lens.FieldOfView, Is.EqualTo(expectedFOV));
            Assert.That(camera.GetComponent<CinemachineFollow>().FollowOffset, Is.EqualTo(NormalOffset));
            Assert.That((brain.ChannelMask & camera.OutputChannel) != 0, Is.True);
            Assert.That(camera.transform.Find("cm"), Is.Null);
        }
        finally
        {
            if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void FollowStaysInWorldSpaceAndForcedPanningDetachesAndReattaches()
    {
        WithController((controller, camera, target) =>
        {
            ControllerType.GetMethod("Initialize").Invoke(controller, new object[] { target });
            camera.InternalUpdateCameraState(Vector3.up, -1f);
            Assert.That(Vector3.Distance(camera.transform.position, target.position + NormalOffset), Is.LessThan(0.001f));
            var rotation = camera.transform.rotation;

            ControllerType.GetMethod("ForcePan").Invoke(controller, new object[] { true });
            Assert.That(camera.Follow, Is.Null);
            Assert.That(camera.LookAt, Is.Null);
            Assert.That(ControllerType.GetProperty("IsPanning").GetValue(controller), Is.True);

            var pannedPosition = camera.transform.position + new Vector3(10f, 0f, -5f);
            camera.transform.position = pannedPosition;
            target.position += new Vector3(20f, 0f, 20f);
            camera.InternalUpdateCameraState(Vector3.up, 1f / 60f);
            Assert.That(Vector3.Distance(camera.transform.position, pannedPosition), Is.LessThan(0.001f));

            ControllerType.GetMethod("ForcePan").Invoke(controller, new object[] { false });
            camera.InternalUpdateCameraState(Vector3.up, 1f / 60f);
            Assert.That(camera.Follow, Is.EqualTo(target));
            Assert.That(camera.LookAt, Is.EqualTo(target));
            Assert.That(ControllerType.GetProperty("IsPanning").GetValue(controller), Is.False);
            Assert.That(Vector3.Distance(camera.transform.position, target.position + NormalOffset), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(0.001f));
        });
    }

    [TestCase(18.85f, -18.6f, 25f)]
    [TestCase(57.7f, -61f, 30f)]
    [TestCase(27.7f, -28.9f, 25f)]
    public void ZoomRoutineWritesTheNativeLensAndFollowOffset(float y, float z, float fieldOfView)
    {
        WithController((controller, camera, target) =>
        {
            var offset = new Vector3(0f, y, z);
            camera.Lens.FieldOfView = 60f;
            camera.GetComponent<CinemachineFollow>().FollowOffset = Vector3.back;
            var routine = (IEnumerator)ControllerType.GetMethod("SmoothZoomTransition", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { fieldOfView, offset, 0f });
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(camera.Lens.FieldOfView, Is.EqualTo(fieldOfView));
            Assert.That(camera.GetComponent<CinemachineFollow>().FollowOffset, Is.EqualTo(offset));
        });
    }

    private static void WithController(Action<Component, CinemachineCamera, Transform> test)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var cameraObject = Object.Instantiate(prefab.GetComponentInChildren<CinemachineCamera>(true).gameObject);
        var controllerObject = new GameObject("Camera controller migration test");
        var targetObject = new GameObject("Camera target migration test");
        var singleton = ControllerType.GetField("Instance");
        object previousInstance = singleton.GetValue(null);
        try
        {
            var camera = cameraObject.GetComponent<CinemachineCamera>();
            var controller = controllerObject.AddComponent(ControllerType);
            ((Behaviour)controller).enabled = false;
            ControllerType.GetField("virtualCamera", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, camera);
            targetObject.transform.SetPositionAndRotation(new Vector3(12f, 3f, 8f), Quaternion.Euler(0f, 123f, 0f));
            test(controller, camera, targetObject.transform);
        }
        finally
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(targetObject);
            singleton.SetValue(null, previousInstance);
        }
    }
}

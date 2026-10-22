using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CameraControllerPlayModeTests
{
    private static readonly Type ControllerType = Type.GetType("CameraController, Assembly-CSharp", true);
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Vector3 NormalOffset = new Vector3(0f, 18.85f, -18.6f);
    private GameObject root;
    private Component controller;
    private CinemachineCamera camera;
    private CinemachineFollow follow;
    private Camera outputCamera;
    private Transform target;
    private object previousInstance;
    private float previousTimeScale;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DefaultObjects.prefab");
#else
        Assert.Ignore("These regression tests load the authored camera rig through the Editor.");
        yield break;
#endif
#if UNITY_EDITOR
        previousInstance = ControllerType.GetField("Instance").GetValue(null);
        previousTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        root = new GameObject("Camera migration play mode test");
        camera = UnityEngine.Object.Instantiate(prefab.GetComponentInChildren<CinemachineCamera>(true), root.transform);
        follow = camera.GetComponent<CinemachineFollow>();

        var outputObject = new GameObject("Test main camera");
        outputObject.transform.SetParent(root.transform);
        outputCamera = outputObject.AddComponent<Camera>();
        outputObject.AddComponent<CinemachineBrain>();
        var lightObject = new GameObject("Test directional light");
        lightObject.transform.SetParent(root.transform);
        lightObject.AddComponent<Light>().type = LightType.Directional;

        var targetObject = new GameObject("Test local player");
        target = targetObject.transform;
        target.SetParent(root.transform);
        target.position = new Vector3(12f, 0f, 8f);

        var controllerObject = new GameObject("Test camera controller");
        controllerObject.transform.SetParent(root.transform);
        controllerObject.SetActive(false);
        controller = controllerObject.AddComponent(ControllerType);
        ((Behaviour)controller).enabled = false; // Exercise camera behavior without requiring lobby/input singletons.
        ControllerType.GetField("virtualCamera", PrivateInstance).SetValue(controller, camera);
        ControllerType.GetField("panSpeed", PrivateInstance).SetValue(controller, 15f);
        controllerObject.SetActive(true);
        ControllerType.GetMethod("Start", PrivateInstance).Invoke(controller, null);
        Invoke("Initialize", target);
        yield return null;
#endif
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (root != null) UnityEngine.Object.Destroy(root);
        Time.timeScale = previousTimeScale;
        ControllerType.GetField("Instance").SetValue(null, previousInstance);
        yield return null;
    }

    [UnityTest]
    public IEnumerator SuperJumpZoomAndResetCompleteOverRealFrames()
    {
        Invoke("SetSuperJumpCamera", true);
        yield return WaitForZoom(new Vector3(0f, 57.7f, -61f), 30f);
        Invoke("SetSuperJumpCamera", false);
        yield return WaitForZoom(NormalOffset, 25f);
    }

    [UnityTest]
    public IEnumerator NewZoomInterruptsThePreviousTransitionWithoutLaterOverwritingIt()
    {
        Invoke("ZoomCamera", new Vector3(0f, 57.7f, -61f), 30f, 0.3f);
        yield return null;
        var cannonOffset = new Vector3(0f, 27.7f, -28.9f);
        Invoke("ZoomCamera", cannonOffset, 25f, 0.15f);
        yield return WaitForZoom(cannonOffset, 25f);
        yield return new WaitForSeconds(0.4f);
        Assert.That(follow.FollowOffset, Is.EqualTo(cannonOffset));
        Assert.That(camera.Lens.FieldOfView, Is.EqualTo(25f));
        Invoke("ResetZoom");
        yield return WaitForZoom(NormalOffset, 25f);
    }

    [UnityTest]
    public IEnumerator ForcedPanMovesInWorldSpaceAndReattachesToTheLocalPlayer()
    {
        Invoke("ForcePan", true);
        Assert.That(camera.Follow, Is.Null);
        Assert.That(camera.LookAt, Is.Null);
        ControllerType.GetField("moveInput", PrivateInstance).SetValue(controller, Vector2.one);
        Vector3 initialPosition = camera.transform.position;
        Vector3 expectedPosition = initialPosition + new Vector3(1f, 0f, 1f) * 15f * Time.deltaTime;
        ControllerType.GetMethod("PanCamera", PrivateInstance).Invoke(controller, null);
        target.position += Vector3.right * 20f;
        yield return null;
        Assert.That(Vector3.Distance(camera.transform.position, expectedPosition), Is.LessThan(0.001f));
        Assert.That(Vector3.Distance(outputCamera.transform.position, expectedPosition), Is.LessThan(0.001f));

        Invoke("ForcePan", false);
        yield return null;
        Assert.That(camera.Follow, Is.EqualTo(target));
        Assert.That(camera.LookAt, Is.EqualTo(target));
        Assert.That(Vector3.Distance(camera.transform.position, target.position + NormalOffset), Is.LessThan(0.001f));
    }

    private IEnumerator WaitForZoom(Vector3 offset, float fieldOfView)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while ((follow.FollowOffset != offset || !Mathf.Approximately(camera.Lens.FieldOfView, fieldOfView))
               && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return null; // Let the Brain apply the final camera state in LateUpdate.
        Assert.That(Vector3.Distance(follow.FollowOffset, offset), Is.LessThan(0.001f));
        Assert.That(camera.Lens.FieldOfView, Is.EqualTo(fieldOfView).Within(0.001f));
        Assert.That(outputCamera.fieldOfView, Is.EqualTo(fieldOfView).Within(0.001f));
        Assert.That(Vector3.Distance(camera.transform.position, target.position + offset), Is.LessThan(0.001f));
    }

    private void Invoke(string method, params object[] arguments)
    {
        ControllerType.GetMethod(method).Invoke(controller, arguments);
    }
}

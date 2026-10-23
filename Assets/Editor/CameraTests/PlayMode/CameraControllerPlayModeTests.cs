using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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

    [UnityTest]
    public IEnumerator TouchPanTracksDisplacementThenReturnsToTheMovingPlayer()
    {
        Vector3 start = camera.transform.position;
        Assert.That(ControllerType.GetMethod("BeginTouchPan").Invoke(controller, null), Is.True);
        Assert.That(camera.Follow, Is.Null);
        Assert.That(camera.LookAt, Is.Null);
        Invoke("DragTouchPan", new Vector2(Screen.height * 0.5f, Screen.height * 0.25f));
        Vector3 expected = start + new Vector3(15f, 0f, 7.5f);
        target.position += Vector3.right * 20f;
        yield return null;
        Assert.That(Vector3.Distance(camera.transform.position, expected), Is.LessThan(0.001f));
        Assert.That(Vector3.Distance(outputCamera.transform.position, expected), Is.LessThan(0.001f));
        yield return null; // Holding still must not produce joystick-style continuous panning.
        Assert.That(Vector3.Distance(camera.transform.position, expected), Is.LessThan(0.001f));

        Invoke("EndTouchPan");
        yield return null;
        Assert.That(camera.Follow, Is.EqualTo(target));
        Assert.That(camera.LookAt, Is.EqualTo(target));
        Assert.That(ControllerType.GetProperty("IsPanning").GetValue(controller), Is.False);
        Assert.That(Vector3.Distance(outputCamera.transform.position, target.position + NormalOffset), Is.LessThan(0.001f));
    }

    [Test]
    public void TouchPanIsIndependentOfEventCountAndUsesCameraRelativeGroundAxes()
    {
        camera.transform.rotation = Quaternion.Euler(45f, 90f, 0f);
        Vector3 start = camera.transform.position;
        Invoke("BeginTouchPan");
        var delta = new Vector2(Screen.height * 0.25f, Screen.height * 0.5f);
        Invoke("DragTouchPan", delta);
        Vector3 singleEventPosition = camera.transform.position;
        Assert.That(Vector3.Distance(singleEventPosition, start + new Vector3(15f, 0f, -7.5f)), Is.LessThan(0.001f));

        camera.transform.position = start;
        for (int i = 0; i < 10; i++) Invoke("DragTouchPan", delta / 10f);
        Assert.That(Vector3.Distance(camera.transform.position, singleEventPosition), Is.LessThan(0.001f));
    }

    [Test]
    public void TouchReleasePreservesForcedPanningButRespawnCancelsAnActiveDrag()
    {
        Invoke("ForcePan", true);
        Invoke("BeginTouchPan");
        Invoke("EndTouchPan");
        Assert.That(camera.Follow, Is.Null);
        Assert.That(ControllerType.GetProperty("IsPanning").GetValue(controller), Is.True);

        Invoke("BeginTouchPan");
        Invoke("ForcePan", false);
        Vector3 start = camera.transform.position;
        Invoke("DragTouchPan", Vector2.one * Screen.height);
        Assert.That(camera.transform.position, Is.EqualTo(start));
        Assert.That(camera.Follow, Is.EqualTo(target));
    }

    [Test]
    public void PanAreaIgnoresOtherFingersAndReleasesEvenOutsideItsBounds()
    {
        GameObject area = CreatePanArea();
        var first = new PointerEventData(null) { pointerId = 11, delta = new Vector2(100f, 50f) };
        var second = new PointerEventData(null) { pointerId = 22, delta = Vector2.one * 500f };
        ExecuteEvents.Execute(area, first, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(area, first, ExecuteEvents.initializePotentialDrag);
        Assert.That(first.useDragThreshold, Is.False);
        Vector3 start = camera.transform.position;
        ExecuteEvents.Execute(area, second, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(area, second, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(area, second, ExecuteEvents.pointerUpHandler);
        Assert.That(camera.transform.position, Is.EqualTo(start));
        Assert.That(camera.Follow, Is.Null);

        ExecuteEvents.Execute(area, first, ExecuteEvents.dragHandler);
        Assert.That(Vector3.Distance(camera.transform.position, start), Is.GreaterThan(0f));
        first.position = new Vector2(-1000f, -1000f);
        ExecuteEvents.Execute(area, first, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(area, first, ExecuteEvents.endDragHandler);
        Assert.That(camera.Follow, Is.EqualTo(target));
        start = camera.transform.position;
        ExecuteEvents.Execute(area, first, ExecuteEvents.dragHandler);
        Assert.That(camera.transform.position, Is.EqualTo(start));
    }

    [UnityTest]
    public IEnumerator TransparentPanAreaOnlyRaycastsInsideItsBoundsAndYieldsToButtons()
    {
        var canvasObject = new GameObject("Pan area raycast test", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root.transform);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        GameObject area = CreatePanArea();
        area.transform.SetParent(canvasObject.transform, false);
        yield return null;

        var pointer = new PointerEventData(null) { position = new Vector2(Screen.width * 0.8f, Screen.height * 0.65f) };
        var hits = new List<RaycastResult>();
        var raycaster = canvasObject.GetComponent<GraphicRaycaster>();
        raycaster.Raycast(pointer, hits);
        Assert.That(hits.Count, Is.EqualTo(1));
        Assert.That(hits[0].gameObject, Is.EqualTo(area));

        hits.Clear();
        pointer.position = new Vector2(Screen.width * 0.1f, Screen.height * 0.1f);
        raycaster.Raycast(pointer, hits);
        Assert.That(hits, Is.Empty);

        var button = new GameObject("Overlapping move button", typeof(RectTransform), typeof(Image));
        button.transform.SetParent(canvasObject.transform, false);
        var buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = area.GetComponent<RectTransform>().anchorMin;
        buttonRect.anchorMax = area.GetComponent<RectTransform>().anchorMax;
        buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
        yield return null;
        pointer.position = new Vector2(Screen.width * 0.8f, Screen.height * 0.65f);
        raycaster.Raycast(pointer, hits);
        Assert.That(hits[0].gameObject, Is.EqualTo(button));
    }

    [TestCase("OnApplicationFocus", false)]
    [TestCase("OnApplicationPause", true)]
    public void PanAreaReleasesOnApplicationInterruption(string method, bool value)
    {
        GameObject area = CreatePanArea();
        var pointer = new PointerEventData(null) { pointerId = 11 };
        ExecuteEvents.Execute(area, pointer, ExecuteEvents.pointerDownHandler);
        Assert.That(camera.Follow, Is.Null);
        var handlerType = Type.GetType("MobileCameraPanArea, Assembly-CSharp", true);
        handlerType.GetMethod(method, PrivateInstance).Invoke(area.GetComponent(handlerType), new object[] { value });
        Assert.That(camera.Follow, Is.EqualTo(target));
        // A later drag can acquire the area again.
        ExecuteEvents.Execute(area, pointer, ExecuteEvents.pointerDownHandler);
        Assert.That(camera.Follow, Is.Null);
        area.SetActive(false);
        Assert.That(camera.Follow, Is.EqualTo(target));
    }

    private GameObject CreatePanArea()
    {
#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DefaultObjects.prefab");
        var handlerType = Type.GetType("MobileCameraPanArea, Assembly-CSharp", true);
        var area = UnityEngine.Object.Instantiate(prefab.GetComponentInChildren(handlerType, true).gameObject, root.transform);
        handlerType.GetField("cameraController", PrivateInstance).SetValue(area.GetComponent(handlerType), controller);
        return area;
#else
        throw new NotSupportedException("Requires the authored prefab in the Editor.");
#endif
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

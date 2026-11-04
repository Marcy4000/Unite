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
    public IEnumerator SpectatorStartupFollowsFirstRosterCardOnceAfterCameraAndPlayersAreReady()
    {
        var hudType = Type.GetType("SpectatorBattleHUD, Assembly-CSharp", true);
        var cardType = Type.GetType("SpectatorPlayerCard, Assembly-CSharp", true);
        var actorType = Type.GetType("PlayerManager, Assembly-CSharp", true);
        var holder = new GameObject("Inactive spectator startup fixture");
        holder.transform.SetParent(root.transform);
        holder.SetActive(false);
        var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Spectator/SpectatorBattleHUD.prefab");
        var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Spectator/SpectatorPlayerCard.prefab");
        var hud = UnityEngine.Object.Instantiate(hudPrefab, holder.transform).GetComponent(hudType);
        var cards = (IList)hudType.GetField("cards", PrivateInstance).GetValue(hud);
        var startup = hudType.GetMethod("FollowFirstPlayerOnStartup", PrivateInstance);
        var ready = hudType.GetField("cameraReady", PrivateInstance);
        var assigned = hudType.GetField("initialFollowAssigned", PrivateInstance);
        Invoke("InitializeSpectator", Vector3.zero);
        ready.SetValue(hud, true);
        startup.Invoke(hud, null);
        Assert.That(assigned.GetValue(hud), Is.False, "An empty roster must not consume the startup selection.");

        var actors = new Transform[2];
        for (int i = 0; i < actors.Length; i++)
        {
            var actorObject = new GameObject("Roster player " + i);
            actorObject.transform.SetParent(holder.transform);
            actorObject.transform.position = new Vector3(10 + i * 5, 0, 8);
            var actor = actorObject.AddComponent(actorType);
            actors[i] = actorObject.transform;
            var card = UnityEngine.Object.Instantiate(cardPrefab, hud.transform).GetComponent(cardType);
            cardType.GetField("player", PrivateInstance).SetValue(card, actor);
            cards.Add(card);
        }
        ready.SetValue(hud, false);
        startup.Invoke(hud, null);
        Assert.That(ControllerType.GetProperty("SpectatorTarget").GetValue(controller), Is.Null);
        ready.SetValue(hud, true);
        startup.Invoke(hud, null);
        Assert.That(ControllerType.GetProperty("SpectatorTarget").GetValue(controller), Is.SameAs(actors[0]));
        Assert.That(camera.Follow.position, Is.EqualTo(actors[0].position));
        Assert.That(assigned.GetValue(hud), Is.True);

        Invoke("FollowSpectator", actors[1]);
        startup.Invoke(hud, null);
        Assert.That(ControllerType.GetProperty("SpectatorTarget").GetValue(controller), Is.SameAs(actors[1]));
        Invoke("BeginTouchPan");
        Invoke("DragTouchPan", new Vector2(40, 10));
        Invoke("EndTouchPan");
        cards.RemoveAt(0); // A roster refresh must not snap back after unlocking.
        startup.Invoke(hud, null);
        Assert.That(ControllerType.GetProperty("SpectatorTarget").GetValue(controller), Is.Null);
        Assert.That(camera.Follow, Is.Null);

        assigned.SetValue(hud, false); // Also preserve a choice made before the HUD is ready.
        Invoke("FollowSpectator", target);
        startup.Invoke(hud, null);
        Assert.That(ControllerType.GetProperty("SpectatorTarget").GetValue(controller), Is.SameAs(target));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SpectatorFollowUnlocksOnlyOnActualDragAndStaysFreeAfterRelease()
    {
        ((Behaviour)controller).enabled = true;
        Invoke("InitializeSpectator", new Vector3(3, 0, 4));
        Assert.That(camera.Follow.position, Is.EqualTo(new Vector3(3, 0, 4)));
        Invoke("FollowSpectator", target);
        Assert.That(camera.Follow.position, Is.EqualTo(target.position));
        Assert.That(ControllerType.GetProperty("SpectatorTarget").GetValue(controller), Is.SameAs(target));
        target.position += new Vector3(4, 0, 2);
        yield return null;
        Assert.That(camera.Follow.position, Is.EqualTo(target.position));
        Invoke("BeginTouchPan");
        Assert.That(camera.Follow, Is.Not.Null, "Pointer-down alone must not unlock a followed player.");
        Invoke("DragTouchPan", new Vector2(40, 10));
        Assert.That(camera.Follow, Is.Null);
        Assert.That(ControllerType.GetProperty("SpectatorTarget").GetValue(controller), Is.Null);
        Invoke("EndTouchPan");
        yield return null;
        Assert.That(camera.Follow, Is.Null, "Ending a spectator pan must not snap back to the player.");
        Invoke("FollowSpectator", target);
        Assert.That(camera.Follow.position, Is.EqualTo(target.position));
        Invoke("ForcePan", true);
        Assert.That(ControllerType.GetProperty("SpectatorTarget").GetValue(controller), Is.SameAs(target), "Player death/special-mode ForcePan calls must not override an observer's selection.");
    }

    [UnityTest]
    public IEnumerator SpectatorLockDoesNotChaseDeadParkingPositionAndResumesAtRespawn()
    {
        var actorObject = new GameObject("Observed actor");
        actorObject.transform.SetParent(root.transform);
        actorObject.SetActive(false);
        var actorType = Type.GetType("PlayerManager, Assembly-CSharp", true);
        var actor = actorObject.AddComponent(actorType);
        var state = actorType.GetField("playerState", PrivateInstance).GetValue(actor);
        var value = state.GetType().GetProperty("Value");
        var stateType = value.PropertyType;
        actorObject.transform.position = new Vector3(5, 0, 7);
        Invoke("InitializeSpectator", Vector3.zero);
        Invoke("FollowSpectator", actorObject.transform);
        Assert.That(camera.Follow.position, Is.EqualTo(new Vector3(5, 0, 7)));
        value.SetValue(state, Enum.Parse(stateType, "Dead"));
        actorObject.transform.position = new Vector3(0, -50, -50);
        ControllerType.GetMethod("LateUpdate", PrivateInstance).Invoke(controller, null);
        Assert.That(camera.Follow.position, Is.EqualTo(new Vector3(5, 0, 7)));
        Invoke("FollowSpectator", actorObject.transform); // Selecting an already-dead actor keeps the current view.
        Invoke("BeginTouchPan"); Invoke("EndTouchPan");
        Assert.That(camera.Follow, Is.Null);
        actorObject.transform.position = new Vector3(14, 0, 12);
        value.SetValue(state, Enum.Parse(stateType, "Alive"));
        ControllerType.GetMethod("LateUpdate", PrivateInstance).Invoke(controller, null);
        Assert.That(camera.Follow.position, Is.EqualTo(new Vector3(14, 0, 12)));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SpectatorLockIgnoresAliveVolleyballAndRaceParkingAndReturnInterpolation()
    {
        var actorObject = new GameObject("Temporarily parked observed actor");
        actorObject.transform.SetParent(root.transform);
        actorObject.SetActive(false);
        var actorType = Type.GetType("PlayerManager, Assembly-CSharp", true);
        var actor = actorObject.AddComponent(actorType);
        var parked = actorType.GetMethod("ApplySpectatorTeleportState", PrivateInstance);
        Invoke("InitializeSpectator", Vector3.zero);
        foreach (var parkingPosition in new[] { new Vector3(0, -50, -50), new Vector3(-100, -50, 0) })
        {
            Vector3 lastPlayable = new Vector3(5, 0, 7), returned = new Vector3(14, 0, 12);
            actorObject.transform.position = lastPlayable;
            Invoke("FollowSpectator", actorObject.transform);
            parked.Invoke(actor, new object[] { parkingPosition, true });
            actorObject.transform.position = parkingPosition;
            ControllerType.GetMethod("LateUpdate", PrivateInstance).Invoke(controller, null);
            Assert.That(camera.Follow.position, Is.EqualTo(lastPlayable));
            Assert.That(actorType.GetProperty("PlayerState").GetValue(actor).ToString(), Is.EqualTo("Alive"));
            Assert.That(ControllerType.GetProperty("SpectatorTarget").GetValue(controller), Is.SameAs(actorObject.transform));
            Invoke("FollowSpectator", actorObject.transform);
            Invoke("BeginTouchPan"); Invoke("EndTouchPan");
            Assert.That(camera.Follow, Is.Null, "Selecting a parked living player must not reveal their storage location.");
            // Volleyball can issue two return teleports. Neither may clear
            // protection while the remote transform is still below the map.
            parked.Invoke(actor, new object[] { returned, false });
            parked.Invoke(actor, new object[] { returned, false });
            ControllerType.GetMethod("LateUpdate", PrivateInstance).Invoke(controller, null);
            Assert.That(camera.Follow.position, Is.EqualTo(returned));
            actorObject.transform.position = Vector3.Lerp(parkingPosition, returned, 0.5f);
            ControllerType.GetMethod("LateUpdate", PrivateInstance).Invoke(controller, null);
            Assert.That(camera.Follow.position, Is.EqualTo(returned), "Do not follow the replicated flight out of storage.");
            actorObject.transform.position = returned;
            ControllerType.GetMethod("LateUpdate", PrivateInstance).Invoke(controller, null);
            actorObject.transform.position += Vector3.forward * 3;
            ControllerType.GetMethod("LateUpdate", PrivateInstance).Invoke(controller, null);
            Assert.That(camera.Follow.position, Is.EqualTo(actorObject.transform.position));
        }
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
    public IEnumerator SkillAimReturnsOverRealFramesAndCancellationDoesNotWait()
    {
        ((Behaviour)controller).enabled = true;
        object owner = new object();
        Invoke("UpdateSkillAim", owner, Vector3.forward, 7f, 0.1f);
        Transform proxy = camera.Follow;
        Assert.That(proxy, Is.Not.Null); Assert.That(proxy, Is.Not.SameAs(target));
        Invoke("EndSkillAim", owner, false);
        yield return new WaitForSeconds(0.04f);
        Assert.That(camera.Follow, Is.SameAs(proxy));
        yield return new WaitForSeconds(0.12f);
        Assert.That(camera.Follow, Is.SameAs(target));
        Invoke("UpdateSkillAim", owner, Vector3.left, 8f, 2.2f);
        Invoke("EndSkillAim", owner, true);
        Assert.That(camera.Follow, Is.SameAs(target));
        Invoke("UpdateSkillAim", owner, Vector3.forward, 7f, 0.7f);
        Assert.That(camera.Follow, Is.Not.SameAs(target));
        ((Behaviour)controller).enabled = false;
        Assert.That(camera.Follow, Is.SameAs(target), "Disabling the camera controller releases its skill aim immediately.");
    }

    [UnityTest]
    public IEnumerator CannonActivationAndCleanupCannotOverrideOpenJumpSelection()
    {
        object pad = new object();
        object cannon = new object();
        var jumpOffset = new Vector3(0, 57.7f, -61);
        var cannonOffset = new Vector3(0, 27.7f, -28.9f);
        Invoke("SetZoomOverride", pad, jumpOffset, 30f, 100, 0.05f);
        yield return WaitForZoom(jumpOffset, 30f);
        Invoke("SetZoomOverride", cannon, cannonOffset, 25f, 10, 0.05f);
        Invoke("ResetZoom");
        Invoke("ReleaseZoomOverride", cannon, 0.05f);
        yield return WaitForZoom(jumpOffset, 30f);
        Invoke("ReleaseZoomOverride", new object(), 0.05f);
        yield return WaitForZoom(jumpOffset, 30f);
        Invoke("ReleaseZoomOverride", pad, 0.05f);
        yield return WaitForZoom(NormalOffset, 25f);
    }

    [UnityTest]
    public IEnumerator ReleasingJumpSelectionRestoresStillActiveCannonOverride()
    {
        object pad = new object();
        object cannon = new object();
        var jumpOffset = new Vector3(0, 57.7f, -61);
        var cannonOffset = new Vector3(0, 27.7f, -28.9f);
        Invoke("SetZoomOverride", cannon, cannonOffset, 25f, 10, 0.05f);
        Invoke("SetZoomOverride", pad, jumpOffset, 30f, 100, 0.05f);
        yield return WaitForZoom(jumpOffset, 30f);
        Invoke("ReleaseZoomOverride", pad, 0.05f);
        yield return WaitForZoom(cannonOffset, 25f);
        Invoke("ReleaseZoomOverride", cannon, 0.05f);
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
    public IEnumerator TransparentPanAreaOnlyRaycastsInsideItsBoundsAndYieldsToOtherCanvases()
    {
        var canvasObject = new GameObject("Mobile controls canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root.transform);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.GetComponent<Canvas>().sortingOrder = 1;
        GameObject area = CreatePanArea(canvasObject.transform);
        var areaRect = area.GetComponent<RectTransform>();
        var eventSystemObject = new GameObject("Pan raycast event system", typeof(EventSystem));
        eventSystemObject.transform.SetParent(root.transform);
        var eventSystem = eventSystemObject.GetComponent<EventSystem>();
        eventSystem.enabled = false; // Only query raycasts; do not process live editor input.
        yield return null;

        Vector2 inside = RectTransformUtility.WorldToScreenPoint(null, areaRect.TransformPoint(areaRect.rect.center));
        var pointer = new PointerEventData(eventSystem) { position = inside };
        var hits = new List<RaycastResult>();
        eventSystem.RaycastAll(pointer, hits);
        Assert.That(hits.Count, Is.EqualTo(1));
        Assert.That(hits[0].gameObject, Is.EqualTo(area));

        pointer.position = new Vector2(-100f, -100f);
        eventSystem.RaycastAll(pointer, hits);
        Assert.That(hits, Is.Empty);

        // Match the base HUD, mobile HUD and scoreboard sorting orders. A base
        // HUD button must win even though the pan area's parent sorts above it.
        foreach (int order in new[] { 0, 1, 2 })
        {
            var hud = new GameObject("Overlapping HUD canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            hud.transform.SetParent(root.transform);
            hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            hud.GetComponent<Canvas>().sortingOrder = order;
            var button = new GameObject("Overlapping HUD button", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(hud.transform, false);
            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = areaRect.anchorMin;
            buttonRect.anchorMax = areaRect.anchorMax;
            buttonRect.pivot = areaRect.pivot;
            buttonRect.anchoredPosition = areaRect.anchoredPosition;
            buttonRect.sizeDelta = areaRect.sizeDelta;
            yield return null;

            pointer.position = inside;
            eventSystem.RaycastAll(pointer, hits);
            Assert.That(hits[0].gameObject, Is.EqualTo(button), "HUD sorting order " + order);
            hud.SetActive(false);
            eventSystem.RaycastAll(pointer, hits);
            Assert.That(hits[0].gameObject, Is.EqualTo(area));
        }
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

    private GameObject CreatePanArea(Transform parent = null)
    {
#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DefaultObjects.prefab");
        var handlerType = Type.GetType("MobileCameraPanArea, Assembly-CSharp", true);
        var area = UnityEngine.Object.Instantiate(prefab.GetComponentInChildren(handlerType, true).gameObject, parent != null ? parent : root.transform);
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

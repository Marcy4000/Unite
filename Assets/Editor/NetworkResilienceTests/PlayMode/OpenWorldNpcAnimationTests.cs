using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class OpenWorldNpcAnimationTests
{
    private int previousQuality;
    private GameObject root;
    private Component npc;
    private AnimationClip idle;
    private AnimationClip interact;
    private GameObject networkRoot;
    private GameObject trainerRoot;
    private GameObject clothesRoot;
    private object previousClothes;
    private Type clothesType;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private void Set(string name, object value) => npc.GetType().GetField(name, Private).SetValue(npc, value);
    private object Field(string name) => npc.GetType().GetField(name, Private).GetValue(npc);
    private void Call(string name, params object[] args) => npc.GetType().GetMethod(name, Private).Invoke(npc, args);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        previousQuality = QualitySettings.GetQualityLevel();
        QualitySettings.SetQualityLevel(0, false);
        root = new GameObject("Animation NPC test");
        root.SetActive(false);
        npc = root.AddComponent(GameType("OpenWorldNpc"));
        var visual = new GameObject("Visual", typeof(Animator));
        visual.transform.SetParent(root.transform, false);
        var animated = new GameObject("Animated bone");
        animated.transform.SetParent(visual.transform, false);
        idle = Clip(0f, 1f);
        interact = Clip(10f, 11f);
        Set("kind", Enum.ToObject(GameType("OpenWorldNpcKind"), 1));
        Set("visual", visual);
        Set("idleAnimation", idle);
        Set("interactAnimation", interact);
        root.SetActive(true);
        yield return null;
        Assert.That(Field("animator"), Is.Not.Null);
    }

    private static AnimationClip Clip(float start, float end)
    {
        var clip = new AnimationClip();
        clip.SetCurve("Animated bone", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, start, 1f, end));
        return clip;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (trainerRoot != null) Object.Destroy(trainerRoot);
        if (networkRoot != null) networkRoot.GetComponent<NetworkManager>().Shutdown();
        if (root != null) Object.Destroy(root);
        if (networkRoot != null) Object.Destroy(networkRoot);
        if (clothesRoot != null) Object.Destroy(clothesRoot);
        Object.Destroy(idle);
        Object.Destroy(interact);
        yield return null;
        if (clothesType != null) clothesType.GetField("Instance").SetValue(null, previousClothes);
        QualitySettings.SetQualityLevel(previousQuality, false);
    }

    private Camera SamplingCamera(Vector3 position)
    {
        var cameraObject = new GameObject("NPC sampling camera", typeof(Camera));
        cameraObject.transform.SetParent(root.transform, false);
        cameraObject.transform.position = position;
        var camera = cameraObject.GetComponent<Camera>();
        cameraObject.tag = "MainCamera";
        camera.cullingMask = 0;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        npc.GetType().GetField("samplingCamera", flags).SetValue(null, camera);
        npc.GetType().GetField("samplingCameraFrame", flags).SetValue(null, Time.frameCount);
        return camera;
    }

    [UnityTest]
    public IEnumerator QualityChangesApplySamplingDistancesImmediately()
    {
        var camera = SamplingCamera(Vector3.back * 15f);
        for (int quality = 0; quality < 3; quality++)
        {
            QualitySettings.SetQualityLevel(quality, false);
            camera.transform.position = Vector3.back * 15f;
            Set("samplingBand", -1);
            Call("UpdateAnimation", 0.001f);
            Assert.That((int)Field("samplingBand"), Is.EqualTo(quality == 0 ? 1 : 0));
            camera.transform.position = Vector3.back * 35f;
            Call("UpdateAnimation", 0.001f);
            Assert.That((int)Field("samplingBand"), Is.EqualTo(quality == 0 ? 2 : 1));
            camera.transform.position = Vector3.back * 55f;
            Call("UpdateAnimation", 0.001f);
            Assert.That((int)Field("samplingBand"), Is.EqualTo(quality == 2 ? 1 : 2));
        }
        // Both tiers are already in reduced-rate playback here. Switching quality
        // must invalidate the old deadline and sample the new profile immediately.
        Set("nextAnimationSample", 1000f);
        QualitySettings.SetQualityLevel(0, false);
        Call("UpdateAnimation", 0.1f);
        Assert.That((int)Field("samplingBand"), Is.EqualTo(2));
        var bone = root.transform.Find("Visual/Animated bone");
        Assert.That(bone.localPosition.x, Is.EqualTo((float)Field("animationTime") % idle.length).Within(0.002f));
        QualitySettings.SetQualityLevel(2, false);
        camera.transform.position = Vector3.back * 29f;
        Call("UpdateAnimation", 0.001f);
        Assert.That((int)Field("samplingBand"), Is.Zero, "High quality must skip the old 12–30 unit reduced-rate band.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator DistantSamplingHoldsPoseAndCatchesUpWithoutSlowingAnimation()
    {
        SamplingCamera(Vector3.back * 100f);
        Set("animationTime", 0f);
        Set("samplingBand", -1);
        Call("UpdateAnimation", 0.1f);
        var bone = root.transform.Find("Visual/Animated bone");
        Assert.That(bone.localPosition.x, Is.EqualTo(0.1f).Within(0.002f));
        Call("UpdateAnimation", 0.001f);
        Assert.That(bone.localPosition.x, Is.EqualTo(0.1f).Within(0.002f), "Skipped sample must hold its pose.");
        Call("UpdateAnimation", 0.2f);
        Assert.That(bone.localPosition.x, Is.EqualTo(0.301f).Within(0.002f), "Sampling must retain real animation time.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator OffscreenSamplingKeepsTimeAndRefreshesImmediatelyWhenNearby()
    {
        var camera = SamplingCamera(Vector3.back * 100f);
        var visual = (GameObject)Field("visual");
        var mesh = new GameObject("Invisible renderer", typeof(MeshRenderer));
        mesh.transform.SetParent(visual.transform, false);
        Set("animationRenderers", new Renderer[] { mesh.GetComponent<Renderer>() });
        Set("animationTime", 0f);
        Set("samplingBand", -1);
        var bone = root.transform.Find("Visual/Animated bone");
        float held = bone.localPosition.x;
        Call("UpdateAnimation", 0.4f);
        Assert.That((int)Field("samplingBand"), Is.EqualTo(3));
        Assert.That(bone.localPosition.x, Is.EqualTo(held));
        camera.transform.position = Vector3.back;
        Call("UpdateAnimation", 0.1f);
        Assert.That(bone.localPosition.x, Is.EqualTo(0.5f).Within(0.002f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator DistantInteractionAndBlendRemainFullRate()
    {
        SamplingCamera(Vector3.back * 100f);
        Call("PlayClip", interact);
        Call("UpdateAnimation", 0.1f);
        var bone = root.transform.Find("Visual/Animated bone");
        float first = bone.localPosition.x;
        Call("UpdateAnimation", 0.001f);
        Assert.That(bone.localPosition.x, Is.Not.EqualTo(first), "Blends must sample every frame.");
        npc.GetType().GetProperty("IsInteracting").GetSetMethod(true).Invoke(npc, new object[] { true });
        Call("UpdateAnimation", 0.2f);
        Call("UpdateAnimation", 0.001f);
        Assert.That(bone.localPosition.x, Is.EqualTo(10.302f).Within(0.002f));
        Call("UpdateAnimation", 0.8f);
        Assert.That(npc.GetType().GetProperty("IsInteracting").GetValue(npc), Is.False);
        Assert.That(Field("currentClip"), Is.SameAs(idle));
        yield return null;
    }

    [UnityTest]
    public IEnumerator DistanceHysteresisAvoidsSwitchingRatesAtTheNearBoundary()
    {
        var camera = SamplingCamera(Vector3.back * 11f);
        Set("samplingBand", -1);
        Call("UpdateAnimation", 0.01f);
        Assert.That((int)Field("samplingBand"), Is.Zero);
        camera.transform.position = Vector3.back * 13f;
        Call("UpdateAnimation", 0.01f);
        Assert.That((int)Field("samplingBand"), Is.Zero, "Small movements outside the boundary must retain full rate.");
        camera.transform.position = Vector3.back * 15f;
        Call("UpdateAnimation", 0.01f);
        Assert.That((int)Field("samplingBand"), Is.EqualTo(1));
        camera.transform.position = Vector3.back * 13f;
        Call("UpdateAnimation", 0.01f);
        Assert.That((int)Field("samplingBand"), Is.EqualTo(1));
        camera.transform.position = Vector3.back * 11f;
        Call("UpdateAnimation", 0.01f);
        Assert.That((int)Field("samplingBand"), Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ControllerIsDetachedDuringPlaybackAndRestoredOnDisable()
    {
#if UNITY_EDITOR
        var animator = (Animator)Field("animator");
        Call("StopAnimation");
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Model/Pokemons/Eevee/Eevee.controller");
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.CullCompletely;
        Set("animator", animator);
        Call("PlayClip", idle);
        Assert.That(animator.runtimeAnimatorController, Is.Null);
        SamplingCamera(Vector3.back * 100f);
        Set("animationTime", 0f);
        Call("UpdateAnimation", 0.4f);
        Set("nextAnimationSample", 1000f);
        var bone = root.transform.Find("Visual/Animated bone");
        float displayed = bone.localPosition.x;
        yield return null;
        yield return new WaitForEndOfFrame();
        Assert.That(bone.localPosition.x, Is.EqualTo(displayed).Within(0.002f), "Normal animation update must preserve a skipped manual pose.");
        root.SetActive(false);
        Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller));
        Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.CullCompletely));
#else
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator IdleLoopsAndOneShotInteractionReturnsToIdle()
    {
        Set("animationTime", 0f);
        Call("UpdateAnimation", 2.25f);
        Assert.That(root.transform.Find("Visual/Animated bone").localPosition.x, Is.EqualTo(0.25f).Within(0.02f));
        npc.GetType().GetProperty("IsInteracting").GetSetMethod(true).Invoke(npc, new object[] { true });
        Call("PlayClip", interact);
        Call("UpdateAnimation", 0.5f);
        Assert.That(root.transform.Find("Visual/Animated bone").localPosition.x, Is.EqualTo(10.5f).Within(0.02f));
        Call("UpdateAnimation", 0.6f);
        Assert.That(npc.GetType().GetProperty("IsInteracting").GetValue(npc), Is.False);
        Assert.That(Field("currentClip"), Is.SameAs(idle));
        Assert.That(root.transform.Find("Visual/Animated bone").localPosition.x, Is.EqualTo(10.5f).Within(0.02f));
        Call("UpdateAnimation", 0.2f);
        Assert.That(root.transform.Find("Visual/Animated bone").localPosition.x, Is.EqualTo(0.2f).Within(0.02f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator DisableDestroysGraphAndReenableRestartsIdle()
    {
        Assert.That(((PlayableGraph)Field("graph")).IsValid(), Is.True);
        root.SetActive(false);
        Assert.That(((PlayableGraph)Field("graph")).IsValid(), Is.False);
        Assert.That(npc.GetType().GetProperty("IsInteracting").GetValue(npc), Is.False);
        root.SetActive(true);
        yield return null;
        Assert.That(((PlayableGraph)Field("graph")).IsValid(), Is.True);
        Assert.That(Field("currentClip"), Is.SameAs(idle));
    }

    [UnityTest]
    public IEnumerator InteractionWithoutLocalPlayerDoesNotStartPlaybackOrDialogue()
    {
        npc.GetType().GetMethod("Interact").Invoke(npc, null);
        Assert.That(npc.GetType().GetProperty("IsInteracting").GetValue(npc), Is.False);
        Assert.That(Field("currentClip"), Is.SameAs(idle));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ReplacingVisualRestartsAnimationOnTheNewAnimator()
    {
        var replacement = new GameObject("Replacement visual", typeof(Animator));
        replacement.transform.SetParent(root.transform, false);
        new GameObject("Animated bone").transform.SetParent(replacement.transform, false);
        Set("visual", replacement);
        yield return null;
        Assert.That(Field("animator"), Is.SameAs(replacement.GetComponent<Animator>()));
        Assert.That(Field("currentClip"), Is.SameAs(idle));
        Assert.That(((PlayableGraph)Field("graph")).IsValid(), Is.True);
    }

    [UnityTest]
    public IEnumerator CachedVisualFollowsActiveChildAnimatorChanges()
    {
        var container = new GameObject("Animator alternatives");
        container.transform.SetParent(root.transform, false);
        var first = new GameObject("First", typeof(Animator));
        first.transform.SetParent(container.transform, false);
        new GameObject("Animated bone").transform.SetParent(first.transform, false);
        var second = new GameObject("Second", typeof(Animator));
        second.transform.SetParent(container.transform, false);
        new GameObject("Animated bone").transform.SetParent(second.transform, false);
        second.SetActive(false);
        Set("visual", container);
        yield return null;
        Assert.That(Field("animator"), Is.SameAs(first.GetComponent<Animator>()));

        first.SetActive(false);
        second.SetActive(true);
        yield return null;
        Assert.That(Field("animator"), Is.SameAs(second.GetComponent<Animator>()));
        Assert.That(((PlayableGraph)Field("graph")).IsValid(), Is.True);
    }

    [UnityTest]
    public IEnumerator AuthoredPokemonInteractionActuallyAnimatesItsSkeleton() => CheckAuthoredClipPose("OpenWorldPokemonNpc");

    [UnityTest]
    public IEnumerator AuthoredTrainerInteractionActuallyAnimatesItsSkeleton() => CheckAuthoredClipPose("OpenWorldTrainerNpc");

    private IEnumerator CheckAuthoredClipPose(string prefabName)
    {
#if UNITY_EDITOR
        Object.Destroy(root);
        yield return null;
        clothesType = GameType("ClothesList");
        previousClothes = clothesType.GetField("Instance").GetValue(null);
        clothesRoot = new GameObject("Authored NPC empty clothing fixture");
        clothesRoot.AddComponent(clothesType);
        root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Lobby/{prefabName}.prefab"));
        npc = root.GetComponent(GameType("OpenWorldNpc"));
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!(bool)npc.GetType().GetProperty("IsReady").GetValue(npc) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(npc.GetType().GetProperty("IsReady").GetValue(npc), Is.True);
        var clip = (AnimationClip)Field("interactAnimation");
        npc.GetType().GetProperty("IsInteracting").GetSetMethod(true).Invoke(npc, new object[] { true });
        Call("PlayClip", clip);
        yield return new WaitForSeconds(0.25f);
        Set("animationTime", clip.length * 0.3f);
        yield return null;
        yield return new WaitForEndOfFrame();
        var animator = (Animator)Field("animator");
        var bones = AnimationUtility.GetCurveBindings(clip).Where(binding => binding.type == typeof(Transform) &&
            binding.propertyName.StartsWith("m_LocalRotation")).Select(binding => animator.transform.Find(binding.path))
            .Where(bone => bone != null).Distinct().ToArray();
        Assert.That(bones.Length, Is.GreaterThan(3), "Use real authored skeleton bindings, not a synthetic clip.");
        var actual = bones.Select(bone => bone.localRotation).ToArray();
        clip.SampleAnimation(animator.gameObject, (float)Field("animationTime"));
        float largestError = bones.Select((bone, index) => Quaternion.Angle(actual[index], bone.localRotation)).Max();
        Assert.That(largestError, Is.LessThan(1f), "The interaction pose was overwritten during the normal Animator update.");
#else
        Assert.Ignore("Requires authored prefabs and animation bindings.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator NearbyOwnerCanInteractButDistanceAndMenusBlockInteraction()
    {
        yield return CreateOwner(true);
        var avatar = trainerRoot.GetComponent(GameType("OpenWorldTrainerAvatar"));
        var block = avatar.GetType().GetMethod("SetInputBlocked");
        var interactMethod = npc.GetType().GetMethod("Interact");
        block.Invoke(avatar, new object[] { true });
        yield return null;
        interactMethod.Invoke(npc, null);
        Assert.That(npc.GetType().GetProperty("IsInteracting").GetValue(npc), Is.False);
        block.Invoke(avatar, new object[] { false });
        var setPosition = avatar.GetType().GetMethod("SetSpawnPosition", new[] { typeof(Vector3) });
        setPosition.Invoke(avatar, new object[] { Vector3.right * 100f });
        yield return null;
        interactMethod.Invoke(npc, null);
        Assert.That(npc.GetType().GetProperty("IsInteracting").GetValue(npc), Is.False);
        setPosition.Invoke(avatar, new object[] { Vector3.zero });
        yield return null;
        interactMethod.Invoke(npc, null);
        Assert.That(npc.GetType().GetProperty("IsInteracting").GetValue(npc), Is.True);
        Assert.That(Field("currentClip"), Is.SameAs(interact));
        Assert.That(trainerRoot.GetComponent(GameType("OpenWorldTrainerInteractionAnimation")), Is.Null,
            "The optional player animation must remain off by default.");
        Call("UpdateAnimation", 1.1f);
        Assert.That(npc.GetType().GetProperty("IsInteracting").GetValue(npc), Is.False);
        Assert.That(Field("currentClip"), Is.SameAs(idle));
    }

    [UnityTest]
    public IEnumerator GenericCustomModelAnimatesAndReturnsToIdle()
    {
        Set("kind", Enum.ToObject(GameType("OpenWorldNpcKind"), 2));
        yield return NearbyOwnerCanInteractButDistanceAndMenusBlockInteraction();
    }

    [UnityTest]
    public IEnumerator InvisibleGenericSkipsNpcClipsAndStillAnimatesPlayer()
    {
        Set("kind", Enum.ToObject(GameType("OpenWorldNpcKind"), 2));
        Set("visual", null);
        yield return null;
        Assert.That(npc.GetType().GetProperty("IsReady").GetValue(npc), Is.True);
        Assert.That(Field("animator"), Is.Null);
        yield return CheckOwnerAnimation(true);
        Assert.That(npc.GetType().GetProperty("IsInteracting").GetValue(npc), Is.False,
            "An NPC without an Animator must not wait forever for its configured clip.");
        Assert.That(((PlayableGraph)Field("graph")).IsValid(), Is.False);
    }

    [UnityTest]
    public IEnumerator InteractionPlaysMaleOwnerClipAndCleansUp() => CheckOwnerAnimation(true);

    [UnityTest]
    public IEnumerator InteractionPlaysFemaleOwnerClipAndCleansUp() => CheckOwnerAnimation(false);

    private IEnumerator CheckOwnerAnimation(bool isMale)
    {
        yield return CreateOwner(isMale);
#if UNITY_EDITOR
        var male = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Model/Clothing/pref_char_lob_9001_male/AnimationsCN/ani_interactive06a_lob_male.anim");
        var female = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Model/Clothing/pref_char_lob_9002_female/AnimationsCN/ani_interactive06a_lob_female.anim");
        Set("playTrainerAnimation", true);
        Set("maleTrainerAnimation", male);
        Set("femaleTrainerAnimation", female);
        yield return null;
        npc.GetType().GetMethod("Interact").Invoke(npc, null);
        var playback = trainerRoot.GetComponent(GameType("OpenWorldTrainerInteractionAnimation"));
        Assert.That(playback, Is.Not.Null);
        var expected = isMale ? male : female;
        Assert.That(playback.GetType().GetProperty("CurrentClip").GetValue(playback), Is.SameAs(expected));
        var avatar = trainerRoot.GetComponent(GameType("OpenWorldTrainerAvatar"));
        Assert.That(avatar.GetType().GetProperty("CanStartEmote").GetValue(avatar), Is.False);
        yield return new WaitForSeconds(0.25f);
        playback.GetType().GetField("elapsed", Private).SetValue(playback, expected.length * 0.3f);
        yield return null;
        yield return new WaitForEndOfFrame();
        var animator = (Animator)avatar.GetType().GetProperty("AnimationAnimator").GetValue(avatar);
        var bones = AnimationUtility.GetCurveBindings(expected).Where(binding => binding.type == typeof(Transform) &&
            binding.propertyName.StartsWith("m_LocalRotation")).Select(binding => animator.transform.Find(binding.path))
            .Where(bone => bone != null).Distinct().ToArray();
        Assert.That(bones.Length, Is.GreaterThan(3));
        var actual = bones.Select(bone => bone.localRotation).ToArray();
        expected.SampleAnimation(animator.gameObject, (float)playback.GetType().GetField("elapsed", Private).GetValue(playback));
        Assert.That(bones.Select((bone, index) => Quaternion.Angle(actual[index], bone.localRotation)).Max(), Is.LessThan(1f));
        playback.GetType().GetField("elapsed", Private).SetValue(playback, expected.length);
        yield return null;
        yield return new WaitForEndOfFrame();
        Assert.That(playback.GetType().GetProperty("IsBlendingOut").GetValue(playback), Is.True);
        yield return new WaitForSeconds(0.25f);
        Assert.That(playback.GetType().GetProperty("IsPlaying").GetValue(playback), Is.False);
        Assert.That(avatar.GetType().GetMethod("PlayNpcInteractionAnimation").Invoke(avatar, new object[] { male, female, 0.2f }), Is.True);
        avatar.GetType().GetMethod("Move", Private).Invoke(avatar, new object[] { Vector2.up });
        Assert.That(playback.GetType().GetProperty("IsBlendingOut").GetValue(playback), Is.True, "Movement blends back to locomotion.");
        yield return new WaitForSeconds(0.25f);
        Assert.That(playback.GetType().GetProperty("IsPlaying").GetValue(playback), Is.False);
        Assert.That(avatar.GetType().GetMethod("PlayNpcInteractionAnimation").Invoke(avatar, new object[] { male, female, 0.2f }), Is.True);
        trainerRoot.GetComponent<NetworkObject>().Despawn(false);
        Assert.That(playback.GetType().GetProperty("IsPlaying").GetValue(playback), Is.False, "Despawn must release its playable graph.");
#endif
    }

    [UnityTest]
    public IEnumerator NpcTransitionsBlendFromTheLastDisplayedPoseWithoutSnapping()
    {
        Set("animationTime", 0f);
        Call("UpdateAnimation", 0.5f);
        var bone = root.transform.Find("Visual/Animated bone");
        bone.localPosition = Vector3.right * 999f;
        npc.GetType().GetProperty("IsInteracting").GetSetMethod(true).Invoke(npc, new object[] { true });
        Call("PlayClip", interact);
        Assert.That(bone.localPosition.x, Is.EqualTo(0.5f).Within(0.02f));
        Call("UpdateAnimation", 0.1f);
        Assert.That(bone.localPosition.x, Is.EqualTo(5.3f).Within(0.02f));
        Call("UpdateAnimation", 0.1f);
        Assert.That(bone.localPosition.x, Is.EqualTo(10.2f).Within(0.02f));
        Call("UpdateAnimation", 0.9f);
        Assert.That(bone.localPosition.x, Is.EqualTo(10.2f).Within(0.02f));
        Call("UpdateAnimation", 0.1f);
        Assert.That(bone.localPosition.x, Is.EqualTo(5.15f).Within(0.02f));
        Call("UpdateAnimation", 0.1f);
        Assert.That(bone.localPosition.x, Is.EqualTo(0.2f).Within(0.02f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator InterruptedNpcBlendStartsFromTheLastDisplayedBlendedPose()
    {
        Set("animationTime", 0f);
        Call("UpdateAnimation", 0.5f);
        Call("PlayClip", interact);
        Call("UpdateAnimation", 0.1f);
        var bone = root.transform.Find("Visual/Animated bone");
        float displayed = bone.localPosition.x;
        Assert.That(displayed, Is.EqualTo(5.3f).Within(0.02f));

        // Simulate the normal Animator update overwriting the displayed blend.
        bone.localPosition = Vector3.right * 999f;
        Call("PlayClip", idle);
        Assert.That(bone.localPosition.x, Is.EqualTo(displayed).Within(0.02f));
        Call("UpdateAnimation", 0.1f);
        Assert.That(bone.localPosition.x, Is.EqualTo((displayed + 0.1f) * 0.5f).Within(0.02f));
        Call("UpdateAnimation", 0.1f);
        Assert.That(bone.localPosition.x, Is.EqualTo(0.2f).Within(0.02f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ZeroBlendDurationRetainsInstantTransitions()
    {
        Set("animationBlendDuration", 0f);
        npc.GetType().GetProperty("IsInteracting").GetSetMethod(true).Invoke(npc, new object[] { true });
        Call("PlayClip", interact);
        var bone = root.transform.Find("Visual/Animated bone");
        Assert.That(bone.localPosition.x, Is.EqualTo(10f).Within(0.02f));
        Call("UpdateAnimation", 1.1f);
        Assert.That(bone.localPosition.x, Is.EqualTo(0f).Within(0.02f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator TrainerTransitionsBlendInAndBackToTheLiveControllerPose()
    {
        var visual = root.transform.Find("Visual");
        var animator = visual.GetComponent<Animator>();
        var bone = visual.Find("Animated bone");
        root.SetActive(false);
        bone.localPosition = Vector3.right * 2f;
        var playback = root.AddComponent(GameType("OpenWorldTrainerInteractionAnimation"));
        var play = playback.GetType().GetMethod("Play");
        Assert.That(play.Invoke(playback, new object[] { animator, interact, 0.2f }), Is.False,
            "Inactive models must not start playback.");
        root.SetActive(true);
        ((Behaviour)npc).enabled = false;
        bone.localPosition = Vector3.right * 2f;
        Assert.That(play.Invoke(playback, new object[] { animator, interact, 0.2f }), Is.True);
        var lateUpdate = playback.GetType().GetMethod("LateUpdate", Private);
        lateUpdate.Invoke(playback, null);
        Assert.That(bone.localPosition.x, Is.GreaterThanOrEqualTo(2f).And.LessThan(10f));
        playback.GetType().GetMethod("CancelForMovement").Invoke(playback, null);
        Assert.That(playback.GetType().GetProperty("IsBlendingOut").GetValue(playback), Is.True);
        float deadline = Time.realtimeSinceStartup + 2f;
        while ((bool)playback.GetType().GetProperty("IsPlaying").GetValue(playback) && Time.realtimeSinceStartup < deadline)
        {
            bone.localPosition = Vector3.right * 3f;
            lateUpdate.Invoke(playback, null);
            if (!(bool)playback.GetType().GetProperty("IsPlaying").GetValue(playback)) break;
            yield return null;
        }
        Assert.That(playback.GetType().GetProperty("IsPlaying").GetValue(playback), Is.False);
        Assert.That(bone.localPosition.x, Is.EqualTo(3f).Within(0.02f));
    }

    [UnityTest]
    public IEnumerator MaleSeatLocksPoseAndRestoresControlAfterStanding() => CheckSeat(true);

    [UnityTest]
    public IEnumerator FemaleSeatLocksPoseAndRestoresControlAfterStanding() => CheckSeat(false);

    [UnityTest]
    public IEnumerator SeatReservationRejectsCompetingPlayersUntilReleased()
    {
        yield return CreateOwner(true);
        Set("kind", Enum.ToObject(GameType("OpenWorldNpcKind"), 2));
        Set("interaction", Enum.ToObject(GameType("OpenWorldNpcInteraction"), 2));
        var avatarType = GameType("OpenWorldTrainerAvatar");
        var avatar = trainerRoot.GetComponent(avatarType);
        var reserve = avatarType.GetMethod("TryReserveSeat", Private);
        var position = (Vector3)npc.GetType().GetProperty("SeatPosition").GetValue(npc);
        GameObject contender = null;
#if UNITY_EDITOR
        contender = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldTrainer.prefab"));
#endif
        try
        {
            contender.GetComponent<NetworkObject>().SpawnWithOwnership(NetworkManager.Singleton.LocalClientId);
            var otherAvatar = contender.GetComponent(avatarType);
            Assert.That(reserve.Invoke(avatar, new object[] { position }), Is.True);
            Assert.That(avatarType.GetProperty("IsSeated").GetValue(avatar), Is.False,
                "The reservation must lock the seat before the owner's sitting state arrives.");
            Assert.That(reserve.Invoke(otherAvatar, new object[] { position }), Is.False);
            Assert.That(avatarType.GetMethod("IsSeatAvailable").Invoke(otherAvatar, new object[] { npc }), Is.False);
            avatarType.GetMethod("ClearSeat").Invoke(avatar, null);
            Assert.That(reserve.Invoke(otherAvatar, new object[] { position }), Is.True);
            contender.GetComponent<NetworkObject>().Despawn();
            Assert.That(reserve.Invoke(avatar, new object[] { position }), Is.True,
                "Despawning the occupant must make the seat available again.");
        }
        finally
        {
            if (contender != null) Object.Destroy(contender);
        }
    }

    [UnityTest]
    public IEnumerator SeatReservationDoesNotBlockOtherSeats()
    {
        yield return CreateOwner(true);
        Set("kind", Enum.ToObject(GameType("OpenWorldNpcKind"), 2));
        Set("interaction", Enum.ToObject(GameType("OpenWorldNpcInteraction"), 2));
        var avatarType = GameType("OpenWorldTrainerAvatar");
        var avatar = trainerRoot.GetComponent(avatarType);
        var reserve = avatarType.GetMethod("TryReserveSeat", Private);
        var position = (Vector3)npc.GetType().GetProperty("SeatPosition").GetValue(npc);
        Assert.That(reserve.Invoke(avatar, new object[] { position + Vector3.right * 100f }), Is.False,
            "The server must reject positions that do not identify a real seat.");
        Assert.That(reserve.Invoke(avatar, new object[] { position }), Is.True);
        var otherSeat = Object.Instantiate(root);
        try
        {
            otherSeat.transform.position += Vector3.right * 2f;
            Assert.That(avatarType.GetMethod("IsSeatAvailable").Invoke(avatar,
                new object[] { otherSeat.GetComponent(npc.GetType()) }), Is.True);
        }
        finally
        {
            Object.Destroy(otherSeat);
        }
    }

    private IEnumerator CheckSeat(bool isMale)
    {
        yield return CreateOwner(isMale);
        Set("kind", Enum.ToObject(GameType("OpenWorldNpcKind"), 2));
        Set("interaction", Enum.ToObject(GameType("OpenWorldNpcInteraction"), 2));
        root.transform.SetPositionAndRotation(new Vector3(1f, 0.3f, 1f), Quaternion.Euler(0f, 90f, 0f));
        Set("seatFeetOffset", new Vector3(0f, 0f, 0.5f));
        var avatar = trainerRoot.GetComponent(GameType("OpenWorldTrainerAvatar"));
        var type = avatar.GetType();
        Assert.That(type.GetMethod("TryUseSeat").Invoke(avatar, new object[] { npc }), Is.True);
        Assert.That(trainerRoot.transform.position, Is.EqualTo(root.transform.TransformPoint(0f, 0f, 0.5f)));
        Assert.That(trainerRoot.transform.eulerAngles.y, Is.EqualTo(90f).Within(0.1f));
        Assert.That(type.GetProperty("CanStartEmote").GetValue(avatar), Is.False);
        var position = trainerRoot.transform.position;
        type.GetMethod("Move", Private).Invoke(avatar, new object[] { Vector2.up });
        Assert.That(trainerRoot.transform.position, Is.EqualTo(position), "Sitting blocks both movement and gravity.");
        var animator = (Animator)type.GetProperty("AnimationAnimator").GetValue(avatar);
        string suffix = isMale ? "male" : "female";
        yield return null;
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("ani_sitdown_lob_" + suffix), Is.True);
        var sit = animator.runtimeAnimatorController.animationClips.First(clip => clip.name == "ani_sitdown_lob_" + suffix);
        yield return new WaitForSeconds(sit.length + 0.4f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(isMale ? "ani_sit01_lob_male" : "ani_sit_lob_female"), Is.True);
        Vector3 seatPosition = root.transform.position;
        root.transform.position += Vector3.right * 100f;
        Assert.That(npc.GetType().GetMethod("CanInteract", Private).Invoke(npc, new object[] { avatar }), Is.True,
            "Standing up must remain available outside the normal interaction radius.");
        root.transform.position = seatPosition;
        type.GetMethod("SetInputBlocked").Invoke(avatar, new object[] { true });
        Assert.That(npc.GetType().GetMethod("CanInteract", Private).Invoke(npc, new object[] { avatar }), Is.False,
            "The stand-up prompt must respect menus/chat.");
        Assert.That(type.GetMethod("TryLeaveSeat").Invoke(avatar, null), Is.False, "Menus/chat must block the stand-up input.");
        type.GetMethod("SetInputBlocked").Invoke(avatar, new object[] { false });
        Assert.That(type.GetMethod("TryLeaveSeat").Invoke(avatar, null), Is.True);
        Assert.That(type.GetProperty("IsSeated").GetValue(avatar), Is.True);
        yield return null;
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("ani_standup_lob_" + suffix), Is.True);
        var stand = animator.runtimeAnimatorController.animationClips.First(clip => clip.name == "ani_standup_lob_" + suffix);
        yield return new WaitForSeconds(stand.length + 0.5f);
        Assert.That(type.GetProperty("IsSeated").GetValue(avatar), Is.False);
        Assert.That(type.GetProperty("CanStartEmote").GetValue(avatar), Is.True);
    }

    private IEnumerator CreateOwner(bool isMale)
    {
        if (NetworkManager.Singleton != null) Assert.Ignore("An external network session is active.");
        GameObject prefab = null;
#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldTrainer.prefab");
#else
        Assert.Ignore("Requires the Editor-authored trainer prefab.");
        yield break;
#endif
        clothesType = GameType("ClothesList");
        previousClothes = clothesType.GetField("Instance").GetValue(null);
        clothesRoot = new GameObject("NPC interaction empty clothing fixture");
        clothesRoot.AddComponent(clothesType);
        networkRoot = new GameObject("NPC interaction host");
        var host = networkRoot.AddComponent<NetworkManager>();
        var transport = networkRoot.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", 0, "127.0.0.1");
        host.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
        host.AddNetworkPrefab(prefab);
        Assert.That(host.StartHost(), Is.True);
        trainerRoot = Object.Instantiate(prefab);
        var avatar = trainerRoot.GetComponent(GameType("OpenWorldTrainerAvatar"));
        var clothes = Activator.CreateInstance(GameType("PlayerClothesInfo"));
        clothes.GetType().GetProperty("IsMale").SetValue(clothes, isMale);
        string clothing = (string)clothes.GetType().GetMethod("Serialize").Invoke(clothes, null);
        avatar.GetType().GetMethod("Initialize").Invoke(avatar, new object[] { "npc-test-trainer", "Trainer", clothing });
        trainerRoot.GetComponent<NetworkObject>().SpawnWithOwnership(host.LocalClientId);
        float deadline = Time.realtimeSinceStartup + 5f;
        while (avatar.GetType().GetProperty("AnimationAnimator").GetValue(avatar) == null && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(avatar.GetType().GetProperty("AnimationAnimator").GetValue(avatar), Is.Not.Null);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class GameplayLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> created = new();
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Call(object target, string method, params object[] arguments) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, arguments);

    private GameObject Node(string name, bool active = false)
    {
        var node = new GameObject(name);
        node.SetActive(active);
        created.Add(node);
        return node;
    }

    [TearDown]
    public void Cleanup()
    {
        for (int index = created.Count - 1; index >= 0; index--)
            if (created[index] != null) Object.DestroyImmediate(created[index]);
        created.Clear();
    }

    private Component Animation(out Animator animator, out GameObject model)
    {
        var root = Node("Animation lifecycle manager", true);
        var manager = root.AddComponent(GameType("AnimationManager"));
        var controller = new AnimatorController();
        created.Add(controller);
        controller.AddLayer("Base Layer");
        controller.AddParameter("Transition", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Stun", AnimatorControllerParameterType.Trigger);
        var states = controller.layers[0].stateMachine;
        var idle = states.AddState("Idle");
        var cast = states.AddState("Cast");
        var stun = states.AddState("Stunned");
        states.defaultState = idle;
        var clip = new AnimationClip { name = "Cast" };
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0, 2, 0));
        created.Add(clip);
        idle.motion = cast.motion = stun.motion = clip;
        var recover = states.AddAnyStateTransition(idle);
        recover.hasExitTime = false;
        recover.duration = 0;
        recover.AddCondition(AnimatorConditionMode.If, 0, "Transition");
        var interrupt = states.AddAnyStateTransition(stun);
        interrupt.hasExitTime = false;
        interrupt.duration = 0;
        interrupt.AddCondition(AnimatorConditionMode.If, 0, "Stun");
        model = Node("Animation lifecycle model", true);
        animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        Call(manager, "AssignAnimator", animator);
        animator.Update(0);
        return manager;
    }

    [Test]
    public void HiddenStunRecoverySupersedesStunAndAnOldCast()
    {
        var manager = Animation(out var animator, out var model);
        Call(manager, "PlayLocalAnimation", "Cast", 0.25f, 0f);
        Assert.That(animator.speed, Is.GreaterThan(1));
        model.SetActive(false);
        Call(manager, "SetLocalTrigger", Animator.StringToHash("Stun"));
        Call(manager, "SetLocalTrigger", Animator.StringToHash("Transition"));
        Assert.That(animator.speed, Is.EqualTo(1));
        Assert.That(Get(manager, "hasPendingState"), Is.False);
        model.SetActive(true);
        Call(manager, "LateUpdate");
        animator.Update(0);
        animator.Update(0);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(Animator.StringToHash("Idle")));
        Assert.That(Get(manager, "hasPendingTrigger"), Is.False);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void RevealingAModelDoesNotReplayAnAlreadyCompletedCast()
    {
        var manager = Animation(out var animator, out var model);
        Call(manager, "PlayLocalAnimation", "Cast", 0f, 0f);
        animator.Update(0);
        animator.Play("Idle", 0, 0);
        animator.Update(0);
        model.SetActive(false);
        Call(manager, "LateUpdate");
        model.SetActive(true);
        Call(manager, "LateUpdate");
        animator.Update(0);
        Assert.That(animator.keepAnimatorStateOnDisable, Is.True);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(Animator.StringToHash("Idle")));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void DisablingAnimationManagerRestoresDurationMatchedPlaybackSpeed()
    {
        var manager = Animation(out var animator, out _);
        Call(manager, "PlayLocalAnimation", "Cast", 0.25f, 0f);
        Call(manager, "OnDisable");
        Assert.That(animator.speed, Is.EqualTo(1));
        Assert.That(Get(manager, "timedPlayback"), Is.False);
    }

    [Test]
    public void AHiddenDurationMatchedCastResumesItsRequestedSpeedOnReveal()
    {
        var manager = Animation(out var animator, out var model);
        model.SetActive(false);
        Call(manager, "PlayLocalAnimation", "Cast", 0.25f, 0f);
        Assert.That(animator.speed, Is.EqualTo(1));
        model.SetActive(true);
        Call(manager, "LateUpdate");
        animator.Update(0);
        Assert.That(animator.speed, Is.EqualTo(8));
        Assert.That(Get(manager, "hasPendingState"), Is.False);
        Call(manager, "OnDisable");
        Assert.That(animator.speed, Is.EqualTo(1));
    }

    [Test]
    public void UnrelatedPlayerExitingPadCannotCloseTheLocalSelection()
    {
        var pad = Node("Pad", true).AddComponent(GameType("BigJumpPad"));
        var local = Node("Local player").AddComponent(GameType("PlayerManager"));
        var other = Node("Other player");
        other.tag = "Player";
        other.AddComponent(GameType("PlayerManager"));
        Set(pad, "currentJumper", local);
        Set(pad, "inJumpSelection", true);
        Call(pad, "OnTriggerExit", other.AddComponent<BoxCollider>());
        Assert.That(Get(pad, "currentJumper"), Is.SameAs(local));
        Assert.That(Get(pad, "inJumpSelection"), Is.True);
        Set(pad, "inJumpSelection", false);
    }

    [Test]
    public void HostOwnedBotCannotEnterLocalPadUi()
    {
        var pad = Node("Host pad", true).AddComponent(GameType("BigJumpPad"));
        var botRoot = Node("Host owned bot");
        botRoot.tag = "Player";
        var bot = botRoot.AddComponent(GameType("PlayerManager"));
        typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(bot, true);
        ((NetworkVariable<bool>)Get(bot, "botControlled")).Value = true;
        Assert.That(bot.GetType().GetProperty("IsLocalHuman").GetValue(bot), Is.False);
        Call(pad, "OnTriggerEnter", botRoot.AddComponent<BoxCollider>());
        Assert.That(Get(pad, "currentJumper"), Is.Null);
        Assert.That((IList)Get(pad, "actionRestrictions"), Is.Empty);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NonLocalDuraludonCannotAcquireOrResetTheHostCamera(bool hostOwnedBot)
    {
        var instance = GameType("CameraController").GetField("Instance");
        object previous = instance.GetValue(null);
        var camera = Node("Host camera", true).AddComponent(GameType("CameraController"));
        instance.SetValue(null, camera);
        try
        {
            object pad = new object();
            Call(camera, "SetZoomOverride", pad, new Vector3(0, 57.7f, -61), 30f, 100, 0f);
            var player = Node("Non-local Duraludon").AddComponent(GameType("PlayerManager"));
            typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(player, hostOwnedBot);
            ((NetworkVariable<bool>)Get(player, "botControlled")).Value = hostOwnedBot;
            var attack = Activator.CreateInstance(GameType("DuraBasicAtk"));
            attack.GetType().GetField("playerManager").SetValue(attack, player);
            Call(attack, "SetCannonCamera", true);
            Call(attack, "SetCannonCamera", false);
            Assert.That(((IDictionary)Get(camera, "zoomOverrides")).Count, Is.EqualTo(1));
            Assert.That(Get(camera, "effectiveZoomOffset"), Is.EqualTo(new Vector3(0, 57.7f, -61)));
            Assert.That(Get(attack, "cannonCamera"), Is.Null);
        }
        finally { instance.SetValue(null, previous); }
    }

    [Test]
    public void SpawningAnotherPadDoesNotEmitGlobalSelectionExit()
    {
        var pad = Node("New pad", true).AddComponent(GameType("BigJumpPad"));
        int exits = 0;
        Action onExit = () => exits++;
        var exitEvent = pad.GetType().GetEvent("OnExitJumpSelection");
        exitEvent.AddEventHandler(null, onExit);
        try
        {
            Call(pad, "OnNetworkSpawn");
            Assert.That(exits, Is.Zero);
            Call(pad, "OnNetworkDespawn");
        }
        finally { exitEvent.RemoveEventHandler(null, onExit); }
    }

    [Test]
    public void PadCleanupReleasesOnlyItsOwnMovementAndActionRestrictionsOnce()
    {
        var pad = Node("Interrupted pad", true).AddComponent(GameType("BigJumpPad"));
        var playerRoot = Node("Interrupted jumper");
        var player = playerRoot.AddComponent(GameType("PlayerManager"));
        var movement = playerRoot.AddComponent(GameType("PlayerMovement"));
        Set(player, "playerMovement", movement);
        Set(pad, "currentJumper", player);
        Call(movement, "AddMovementRestriction");
        Call(movement, "AddMovementRestriction");
        Set(pad, "movementRestricted", true);
        Set(pad, "isJumping", true);
        var status = Activator.CreateInstance(GameType("BattleActionStatus"), 0f);
        object busy = Enum.Parse(GameType("ActionStatusType"), "Busy");
        Call(status, "AddStatus", busy);
        var lease = (IDisposable)Call(status, "AcquireStatus", busy);
        ((IList)Get(pad, "actionRestrictions")).Add(lease);
        Call(pad, "OnDisable");
        Call(pad, "OnNetworkDespawn");
        Assert.That(Get(movement, "movementRestrictions"), Is.EqualTo(1));
        Assert.That(Call(status, "HasStatus", busy), Is.True);
        Call(status, "RemoveStatus", busy);
        Assert.That(Call(status, "HasStatus", busy), Is.False);
        Assert.That(Get(pad, "currentJumper"), Is.Null);
        Assert.That(Get(pad, "isJumping"), Is.False);
    }
}

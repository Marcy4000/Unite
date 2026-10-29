using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PsyduckRacePassiveLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private Component player, pokemon, movement, animations, passives;
    private CharacterController character;
    private ScriptableObject stats;
    private object passive;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, args.Select(a => a.GetType()).ToArray(), null).Invoke(target, args);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static bool HasSubscriber(object source, string name, object subscriber)
    {
        var callbacks = (Delegate)source.GetType().GetField(name, Private).GetValue(source);
        return callbacks != null && callbacks.GetInvocationList().Any(callback => ReferenceEquals(callback.Target, subscriber));
    }

    [SetUp]
    public void SetUp()
    {
#if UNITY_EDITOR
        stats = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Pokemons/PlayableMons/Psyduck.asset"));
#else
        Assert.Ignore("The authored fixture is Editor-only.");
#endif
        root = new GameObject("Psyduck passive lifecycle"); root.transform.position = Vector3.up * 50f;
        root.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        pokemon = root.AddComponent(GameType("Pokemon")); ((Behaviour)pokemon).enabled = false;
        movement = root.AddComponent(GameType("PlayerMovement")); ((Behaviour)movement).enabled = false;
        animations = root.AddComponent(GameType("AnimationManager"));
        passives = root.AddComponent(GameType("PassiveController")); ((Behaviour)passives).enabled = false;
        character = root.AddComponent<CharacterController>(); character.stepOffset = 0.3f;
        player = root.AddComponent(GameType("PlayerManager")); ((Behaviour)player).enabled = false;
        Set(pokemon, "baseStats", stats); Set(movement, "characterController", character);
        passive = Activator.CreateInstance(GameType("PsyduckRacePassive"));
        Set(passives, "passive", passive);
        Call(passive, "Start", player);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (passive != null) Call(passive, "Dispose");
        if (root != null) UnityEngine.Object.Destroy(root);
        if (stats != null) UnityEngine.Object.Destroy(stats);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DisposalDuringDeferredMovementChangeCannotResubscribeOldPassive()
    {
        Assert.That(character.stepOffset, Is.Zero);
        movement.GetType().GetProperty("CanMove").SetValue(movement, true);
        Assert.That(Get(movement, "CanMove"), Is.False, "Racing passive takes over regular movement.");
        Call(passive, "Dispose"); Call(passive, "Dispose");
        Assert.That(character.stepOffset, Is.EqualTo(0.3f));
        Assert.That(Get(movement, "CanMove"), Is.True, "Requested movement must be restored for a replacement passive.");
        yield return null; yield return null;
        Assert.That(HasSubscriber(movement, "OnCanMoveChanged", passive), Is.False);
        Assert.That(HasSubscriber(pokemon, "OnStatusChange", passive), Is.False);
        movement.GetType().GetProperty("CanMove").SetValue(movement, true);
        Assert.That(Get(movement, "CanMove"), Is.True);
    }

    [UnityTest]
    public IEnumerator RelearningPassiveKeepsMovementAvailableWithoutClearingExternalRestrictions()
    {
        movement.GetType().GetProperty("CanMove").SetValue(movement, true);
        Call(passive, "Dispose");
        passive = Activator.CreateInstance(GameType("PsyduckRacePassive"));
        Set(passives, "passive", passive); Call(passive, "Start", player);
        Assert.That(Get(passive, "CanMove"), Is.True);
        Call(movement, "AddMovementRestriction");
        Call(passive, "Dispose");
        yield return null;
        Assert.That(Get(movement, "CanMove"), Is.False, "Disposal must not consume an external movement restriction.");
        Assert.That(HasSubscriber(movement, "OnCanMoveChanged", passive), Is.False);
    }

    [Test]
    public void MovementAndDashAreSafeBeforeTheAnimatorHasLoaded()
    {
        Assert.DoesNotThrow(() => Call(passive, "HandleMovement", Vector2.zero));
        object dash = Activator.CreateInstance(GameType("PsyduckRaceDash"));
        Call(dash, "Start", player); Call(dash, "Finish");
        Assert.That(GameType("PsyduckRacePassive").GetField("isDashing", Private).GetValue(passive), Is.True);
    }

    [Test]
    public void LearningAndUsingDashDoesNotResetTheRacingBranch()
    {
#if UNITY_EDITOR
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Psyduck/Psyduck.prefab"), root.transform);
        var animator = model.GetComponentInChildren<Animator>(); Call(animations, "AssignAnimator", animator);
        animator.Update(0f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.ani_spell1bidle_bat_0054"), Is.True);
        object dash = Activator.CreateInstance(GameType("PsyduckRaceDash"));
        Call(dash, "Start", player); Call(dash, "Finish"); animator.Update(0f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.ani_spell1bidle_bat_0054"), Is.True);
#else
        Assert.Ignore("Requires the authored model.");
#endif
    }

    [Test]
    public void RejectedDashDoesNotSpendStoredUses()
    {
        GameType("PsyduckRacePassive").GetProperty("CanMove").SetValue(passive, false);
        object dash = Activator.CreateInstance(GameType("PsyduckRaceDash"));
        Call(dash, "Start", player); Call(dash, "Finish");
        Assert.That(Get(dash, "StoredUses"), Is.EqualTo(2));
        Assert.That(GameType("PsyduckRacePassive").GetField("isDashing", Private).GetValue(passive), Is.False);
    }

    [Test]
    public void MovementRestrictionInterruptsDashAndOverridesFreezeSliding()
    {
        GameType("PsyduckRacePassive").GetProperty("CanMove").SetValue(passive, true);
        Assert.That(Call(passive, "TryDash"), Is.True);
        Call(passive, "FreezePlayer", true);
        GameType("PsyduckRacePassive").GetProperty("CurrentVelocity").SetValue(passive, Vector3.forward * 20);
        GameType("PsyduckRacePassive").GetProperty("CanMove").SetValue(passive, false);
        Vector3 position = root.transform.position;
        Call(passive, "Update");
        Assert.That(root.transform.position, Is.EqualTo(position));
        Assert.That(Get(passive, "CurrentVelocity"), Is.EqualTo(Vector3.zero));
        Assert.That(GameType("PsyduckRacePassive").GetField("isDashing", Private).GetValue(passive), Is.False);
        Assert.That(Call(passive, "TryDash"), Is.False);
    }

    [Test]
    public void FreezePreventsNewDashAndInterruptsExistingDash()
    {
        GameType("PsyduckRacePassive").GetProperty("CanMove").SetValue(passive, true);
        Assert.That(Call(passive, "TryDash"), Is.True);
        Call(passive, "FreezePlayer", true);
        Assert.That(Call(passive, "TryDash"), Is.False);
        Call(passive, "Update");
        Assert.That(GameType("PsyduckRacePassive").GetField("isDashing", Private).GetValue(passive), Is.False);
        Call(passive, "FreezePlayer", false);
        Assert.That(Call(passive, "TryDash"), Is.True);
    }

    [Test]
    public void NonRacingPassiveDoesNotSelectTheRacingBranch()
    {
#if UNITY_EDITOR
        Set(stats, "passive", Enum.Parse(GameType("AvailablePassives"), "EmptyPassive"));
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Psyduck/Psyduck.prefab"), root.transform);
        var animator = model.GetComponentInChildren<Animator>(); Call(animations, "AssignAnimator", animator); animator.Update(0f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.ani_idle_bat_0054"), Is.True);
#else
        Assert.Ignore("Requires the authored model.");
#endif
    }
}

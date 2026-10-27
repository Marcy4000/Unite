using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class UniteRoyaleTrainerAnimationTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private Component model;
    private Component trainer;

    [SetUp]
    public void SetUp()
    {
        var modelType = Type.GetType("TrainerModel, Assembly-CSharp", true);
        var trainerType = Type.GetType("UniteRoyaleTrainer, Assembly-CSharp", true);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TrainerModels/TrainerModel.prefab");
        root = Object.Instantiate(prefab);
        model = root.GetComponent(modelType);
        trainer = root.AddComponent(trainerType);
        trainerType.GetField("trainerModel", PrivateInstance).SetValue(trainer, model);
        SetGender(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    private Animator Animator => (Animator)model.GetType().GetProperty("ActiveAnimator").GetValue(model);

    private void SetGender(bool male)
    {
        model.GetType().GetMethod("SetGender").Invoke(model, new object[] { male });
        Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Animator.Update(0f);
    }

    private void Move(bool running)
    {
        trainer.GetType().GetMethod("UpdateMovementAnimation", PrivateInstance)
            .Invoke(trainer, new object[] { running });
    }

    private void Advance(float seconds = 1f)
    {
        for (int i = 0; i < Mathf.CeilToInt(seconds * 60f); i++) Animator.Update(1f / 60f);
    }

    private void AssertRunning(bool male)
    {
        Assert.That(Animator.GetCurrentAnimatorStateInfo(0).IsName(male ? "ani_run_lob_male" : "ani_run_lob_female"),
            Is.True, "A moving trainer must remain in its gender's run state.");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RunPersistsUntilMovementStops(bool male)
    {
        SetGender(male);
        Move(true);
        Advance(3f);
        AssertRunning(male);
        Move(false);
        // The female run-stop clip is over three seconds long.
        Advance(5f);
        Assert.That(Animator.GetCurrentAnimatorStateInfo(0).IsName(male ? "ani_idleb_lob_male" : "ani_idleb_lob_female"), Is.True);
    }

    [Test]
    public void GenderChangeWhileMovingStartsTheNewAnimator()
    {
        Move(true);
        Advance();
        AssertRunning(true);
        // Reproduce the asynchronous outfit commit after following has started.
        SetGender(false);
        Move(true);
        Advance(3f);
        AssertRunning(false);
        SetGender(true);
        Move(true);
        Advance(3f);
        AssertRunning(true);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RestartBeforeStopIsConsumedDoesNotImmediatelyReturnToIdle(bool male)
    {
        SetGender(male);
        Move(true);
        Advance();
        Move(false);
        Move(true);
        Advance(3f);
        AssertRunning(male);
    }

    [Test]
    public void GenderChangeClearsUnconsumedTriggersOnTheNewAnimator()
    {
        SetGender(false);
        Animator.SetTrigger("stopRun");
        SetGender(true);
        Move(true);
        Advance();
        SetGender(false);
        Move(true);
        Advance(3f);
        AssertRunning(false);
    }
}

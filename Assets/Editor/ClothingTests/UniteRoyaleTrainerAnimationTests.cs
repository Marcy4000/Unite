using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public class UniteRoyaleTrainerAnimationTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private Component model;
    private Component trainer;
    private GameObject playerRoot;

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
        playerRoot = new GameObject("Assigned player");
        playerRoot.SetActive(false);
        var player = playerRoot.AddComponent(Type.GetType("PlayerManager, Assembly-CSharp", true));
        trainerType.GetField("assignedPlayer", PrivateInstance).SetValue(trainer, player);
        SetGender(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(playerRoot);
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

    [TestCase(true)]
    [TestCase(false)]
    public void RespawnThrowsTowardsPlayerThenReturnsToIdle(bool male)
    {
        SetGender(male);
        model.GetType().GetField("initialized", PrivateInstance).SetValue(model, true);
        playerRoot.transform.position = root.transform.position + Vector3.right * 5f;
        Move(true);
        Advance();
        trainer.GetType().GetMethod("OnPlayerRespawn", PrivateInstance).Invoke(trainer, null);
        Animator.Update(0f);
        Move(true);
        Advance(0.1f);
        Assert.That(Animator.GetCurrentAnimatorStateInfo(0).IsName(male ? "ani_throwball_lob_male" : "ani_throwball_lob_female"), Is.True);
        Assert.That(Vector3.Dot(root.transform.forward, Vector3.right), Is.GreaterThan(0.99f));
        Advance(10f);
        trainer.GetType().GetMethod("UpdateThrowAnimation", PrivateInstance).Invoke(trainer, null);
        Animator.Update(0f);
        Assert.That(Animator.GetCurrentAnimatorStateInfo(0).IsName(male ? "ani_idleb_lob_male" : "ani_idleb_lob_female"), Is.True);
        Move(true);
        Advance();
        AssertRunning(male);
    }

    [Test]
    public void RespawnSkipsThrowUntilModelIsInitialized()
    {
        trainer.GetType().GetMethod("OnPlayerRespawn", PrivateInstance).Invoke(trainer, null);
        Assert.That(trainer.GetType().GetField("throwAnimator", PrivateInstance).GetValue(trainer), Is.Null);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AnimatorExitTransitionReleasesFollowingAndFacing(bool male)
    {
        SetGender(male);
        model.GetType().GetField("initialized", PrivateInstance).SetValue(model, true);
        trainer.GetType().GetMethod("OnPlayerRespawn", PrivateInstance).Invoke(trainer, null);
        Advance(10f);
        var idleState = Animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        Assert.That(idleState, Is.Not.EqualTo(Animator.StringToHash(male ? "ani_throwball_lob_male" : "ani_throwball_lob_female")));
        trainer.GetType().GetField("nextUpdateTime", PrivateInstance).SetValue(trainer, float.MaxValue);
        var updateThrow = trainer.GetType().GetMethod("UpdateThrowAnimation", PrivateInstance);
        Assert.That(updateThrow.Invoke(trainer, null), Is.False);
        Assert.That(trainer.GetType().GetField("throwAnimator", PrivateInstance).GetValue(trainer), Is.Null);
        Assert.That(trainer.GetType().GetField("nextUpdateTime", PrivateInstance).GetValue(trainer), Is.EqualTo(0f));
        Animator.Update(0f);
        Assert.That(Animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(idleState));
        Quaternion facing = root.transform.rotation;
        playerRoot.transform.position = root.transform.position - root.transform.forward * 5f;
        Assert.That(updateThrow.Invoke(trainer, null), Is.False);
        Assert.That(Quaternion.Angle(root.transform.rotation, facing), Is.LessThan(0.01f));
        Move(true);
        Advance();
        AssertRunning(male);
    }

    [Test]
    public void InitialSpawnWaitsForClothesThenThrowsOnlyOnce()
    {
        trainer.GetType().GetField("initialThrowPending", PrivateInstance).SetValue(trainer, true);
        var update = trainer.GetType().GetMethod("UpdatePlayerState", PrivateInstance);
        update.Invoke(trainer, null);
        Assert.That(trainer.GetType().GetField("throwAnimator", PrivateInstance).GetValue(trainer), Is.Null);
        model.GetType().GetField("initialized", PrivateInstance).SetValue(model, true);
        update.Invoke(trainer, null);
        Assert.That(trainer.GetType().GetField("throwAnimator", PrivateInstance).GetValue(trainer), Is.SameAs(Animator));
        Assert.That(trainer.GetType().GetField("initialThrowPending", PrivateInstance).GetValue(trainer), Is.False);
        trainer.GetType().GetMethod("FinishThrowAnimation", PrivateInstance).Invoke(trainer, null);
        update.Invoke(trainer, null);
        Assert.That(trainer.GetType().GetField("throwAnimator", PrivateInstance).GetValue(trainer), Is.Null);
    }

    [Test]
    public void ThrowDoesNotFinishUsingPreviousIdleStateTime()
    {
        model.GetType().GetField("initialized", PrivateInstance).SetValue(model, true);
        Advance(10f);
        trainer.GetType().GetMethod("OnPlayerRespawn", PrivateInstance).Invoke(trainer, null);
        Assert.That(trainer.GetType().GetMethod("UpdateThrowAnimation", PrivateInstance).Invoke(trainer, null), Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void GenderChangeDuringThrowAllowsNewAnimatorToRun(bool male)
    {
        SetGender(male);
        model.GetType().GetField("initialized", PrivateInstance).SetValue(model, true);
        trainer.GetType().GetMethod("OnPlayerRespawn", PrivateInstance).Invoke(trainer, null);
        Animator.Update(0f);
        SetGender(!male);
        Assert.That(trainer.GetType().GetMethod("UpdateThrowAnimation", PrivateInstance).Invoke(trainer, null), Is.False);
        Move(true);
        Advance();
        AssertRunning(!male);
    }

    [Test]
    public void FollowOffsetIsSmallNonzeroAndHorizontal()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            trainer.GetType().GetMethod("ChooseFollowOffset", PrivateInstance).Invoke(trainer, null);
            var offset = (Vector3)trainer.GetType().GetField("followOffset", PrivateInstance).GetValue(trainer);
            Assert.That(offset.y, Is.Zero);
            Assert.That(offset.magnitude, Is.InRange(0.499f, 1.001f));
        }
    }

    [Test]
    public void DeadPlayerSendsTrainerToSpawnInsteadOfFollowingDeathPosition()
    {
        var sources = new System.Collections.Generic.List<NavMeshBuildSource>
        {
            new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(new Vector3(0f, -0.1f, 0f), Quaternion.identity, Vector3.one),
                size = new Vector3(40f, 0.2f, 40f),
                area = 0
            }
        };
        var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
            new Bounds(Vector3.zero, Vector3.one * 50f), Vector3.zero, Quaternion.identity);
        var instance = NavMesh.AddNavMeshData(data);
        try
        {
            var agent = root.AddComponent<NavMeshAgent>();
            Assert.That(agent.Warp(Vector3.zero), Is.True);
            trainer.GetType().GetField("navMeshAgent", PrivateInstance).SetValue(trainer, agent);
            Vector3 spawn = new Vector3(8f, 0f, 0f);
            trainer.GetType().GetField("spawnPosition", PrivateInstance).SetValue(trainer, spawn);
            playerRoot.transform.position = new Vector3(0f, -50f, -50f);
            var player = trainer.GetType().GetField("assignedPlayer", PrivateInstance).GetValue(trainer);
            var state = player.GetType().GetField("playerState", PrivateInstance).GetValue(player);
            var value = state.GetType().GetProperty("Value");
            value.SetValue(state, Enum.Parse(value.PropertyType, "Dead"));
            trainer.GetType().GetField("nextUpdateTime", PrivateInstance).SetValue(trainer, float.MaxValue);
            trainer.GetType().GetMethod("UpdatePlayerState", PrivateInstance).Invoke(trainer, null);
            Assert.That(trainer.GetType().GetField("nextUpdateTime", PrivateInstance).GetValue(trainer), Is.EqualTo(0f));
            trainer.GetType().GetMethod("UpdateFollowPosition", PrivateInstance).Invoke(trainer, null);
            Assert.That(Vector3.Distance(agent.destination, spawn), Is.LessThan(0.1f));
        }
        finally
        {
            instance.Remove();
            Object.DestroyImmediate(data);
        }
    }
}

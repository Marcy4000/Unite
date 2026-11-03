using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

public class OpenWorldPokemonFollowerTests
{
    private readonly Vector3 origin = new Vector3(1000f, 0f, 1000f);
    private GameObject trainer;
    private GameObject follower;
    private NavMeshData mesh;
    private NavMeshDataInstance meshInstance;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [SetUp]
    public void SetUp()
    {
#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldPokemonFollower.prefab");
        Assert.That(prefab, Is.Not.Null);
        var settings = NavMesh.GetSettingsByID(prefab.GetComponent<NavMeshAgent>().agentTypeID);
        var sources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(40f, 0.2f, 40f),
                transform = Matrix4x4.TRS(origin + Vector3.down * 0.1f, Quaternion.identity, Vector3.one),
                area = 0
            }
        };
        mesh = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(origin, new Vector3(50f, 10f, 50f)), Vector3.zero, Quaternion.identity);
        Assert.That(mesh, Is.Not.Null);
        meshInstance = NavMesh.AddNavMeshData(mesh);
        trainer = new GameObject("Companion trainer fixture");
        trainer.transform.position = origin;
        follower = Object.Instantiate(prefab, origin, Quaternion.identity);
        var component = follower.GetComponent(GameType("OpenWorldPokemonFollower"));
        component.GetType().GetField("updateInterval", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, 0.05f);
        component.GetType().GetField("catchUpDistance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, 12f);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Absol/Absol.prefab");
        component.GetType().GetMethod("Initialize").Invoke(component, new object[] { trainer.transform, model });
        foreach (var animator in follower.GetComponentsInChildren<Animator>(true)) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
#else
        Assert.Ignore("Requires Editor-authored companion assets.");
#endif
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (follower != null) Object.Destroy(follower);
        if (trainer != null) Object.Destroy(trainer);
        yield return null;
        if (meshInstance.valid) meshInstance.Remove();
        if (mesh != null) Object.Destroy(mesh);
    }

    private static IEnumerator Until(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "The companion did not reach the expected movement state.");
    }

    [UnityTest]
    public IEnumerator FollowsTrainerAndSwitchesWalkingBoolBackToIdle()
    {
        var agent = follower.GetComponent<NavMeshAgent>();
        var animator = follower.GetComponentInChildren<Animator>();
        Assert.That(agent.isOnNavMesh, Is.True);
        Assert.That(animator.applyRootMotion, Is.False);
        Assert.That(animator.GetBool("Walking"), Is.False);
        trainer.transform.position = origin + Vector3.right * 8f;
        yield return Until(() => animator.GetBool("Walking"));
        yield return Until(() => Vector3.Distance(follower.transform.position, trainer.transform.position) < 2f && !animator.GetBool("Walking"));
        Assert.That(Vector3.Distance(follower.transform.position, trainer.transform.position), Is.GreaterThan(0.5f));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator RecoversAfterTrainerTeleportAndMissingNavMesh()
    {
        trainer.transform.position = origin + Vector3.right * 16f;
        yield return Until(() => Vector3.Distance(follower.transform.position, trainer.transform.position) < 3f);
        var agent = follower.GetComponent<NavMeshAgent>();
        Assert.That(agent.isOnNavMesh, Is.True);
        agent.enabled = false;
        trainer.transform.position = origin + Vector3.forward * 100f;
        yield return new WaitForSeconds(0.15f);
        Assert.That(agent.enabled, Is.False);
        trainer.transform.position = origin;
        yield return Until(() => agent.enabled && agent.isOnNavMesh);
        Assert.That(Vector3.Distance(follower.transform.position, trainer.transform.position), Is.LessThan(3f));
        LogAssert.NoUnexpectedReceived();
    }
}

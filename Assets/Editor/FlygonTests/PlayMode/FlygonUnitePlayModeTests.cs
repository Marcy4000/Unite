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

// Real-frame animation/lifecycle coverage, isolated from lobby and match singletons.
public class FlygonUnitePlayModeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root, model;
    private Animator animator;
    private Transform bone;
    private Vector3 normalScale;
    private Component effect;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        root = new GameObject("Flygon Unite lifecycle test");
        model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Flygon/Flygon.prefab"), root.transform);
        animator = model.GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        yield return null;
        bone = animator.transform.Find("Armature/pm0330_00/Origin");
        normalScale = bone.localScale;

        var actor = new GameObject("Isolated Flygon actor"); actor.transform.SetParent(root.transform);
        actor.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        ((Behaviour)actor.AddComponent(GameType("Pokemon"))).enabled = false;
        var animations = actor.AddComponent(GameType("AnimationManager"));
        animations.GetType().GetMethod("AssignAnimator").Invoke(animations, new object[] { animator });
        var manager = actor.AddComponent(GameType("PlayerManager"));
        ((Behaviour)manager).enabled = false; // No match, input, lobby or player Update is required.
        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Flygon/FlygonTornado.prefab"), root.transform);
        effect = instance.GetComponent(GameType("TornadoHitbox"));
        GameType("FlygonMoveObject").GetProperty("Actor", PrivateInstance).SetValue(effect, manager);
        animator.Play("Tornado", 0, 0);
        yield return null;
        Assert.That(bone.localScale.x, Is.LessThan(normalScale.x * 0.02f));
#else
        Assert.Ignore("These tests inspect the authored Flygon animator in the Editor.");
        yield break;
#endif
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (root != null) UnityEngine.Object.Destroy(root);
        yield return null;
    }
    [UnityTest]
    public IEnumerator FinalPhaseMakesTheModelVisibleAgain()
    {
        effect.GetType().GetMethod("PresentPhase", PrivateInstance).Invoke(effect, new object[] { (byte)5 });
        yield return null;
        Assert.That(Vector3.Distance(bone.localScale, normalScale), Is.LessThan(0.001f));
    }
    [UnityTest]
    public IEnumerator DespawnWithoutAFinalPhaseRestoresTheModel()
    {
        effect.GetType().GetMethod("OnNetworkDespawn").Invoke(effect, null);
        yield return null;
        Assert.That(Vector3.Distance(bone.localScale, normalScale), Is.LessThan(0.001f));
    }
    [UnityTest]
    public IEnumerator DestroyedTornadoCannotLeaveTheModelHidden()
    {
        UnityEngine.Object.Destroy(effect.gameObject);
        yield return null;
        yield return null; // Allow the Animator to evaluate after end-of-frame destruction.
        Assert.That(Vector3.Distance(bone.localScale, normalScale), Is.LessThan(0.001f));
    }
    [UnityTest]
    public IEnumerator LateDespawnDoesNotCancelANewAttack()
    {
        int attack = Animator.StringToHash("Armature_pm0330_00_ba20_buturi01");
        animator.Play(attack, 0, 0);
        yield return null;
        effect.GetType().GetMethod("OnNetworkDespawn").Invoke(effect, null);
        yield return null;
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(attack));
    }
}

using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class UIObject3DTimerPlayModeTests
{
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private Type timerType;
    private GameObject target;
    private MonoBehaviour callbackTarget;

    [SetUp]
    public void SetUp()
    {
        timerType = Type.GetType("UI.ThreeDimensional.UIObject3DTimer, Assembly-CSharp", true);
        timerType.GetProperty("IsQuitting", StaticMembers).SetValue(null, false);
        DestroyTimer();
        target = new GameObject("UIObject3D timer regression target");
        callbackTarget = (MonoBehaviour)target.AddComponent(
            Type.GetType("UI.ThreeDimensional.UIObject3DContainer, Assembly-CSharp", true));
    }

    [TearDown]
    public void TearDown()
    {
        timerType.GetProperty("IsQuitting", StaticMembers).SetValue(null, false);
        DestroyTimer();
        Object.DestroyImmediate(target);
    }

    [UnityTest]
    public IEnumerator NewPlaySessionAfterQuittingStillInitializesPreviews()
    {
        for (int session = 0; session < 3; session++)
        {
            timerType.GetProperty("IsQuitting", StaticMembers).SetValue(null, true);
            DestroyTimer();
            timerType.GetMethod("OnLoad", StaticMembers).Invoke(null, null);

            bool initialized = false;
            Schedule(() => initialized = true);
            yield return new WaitForSecondsRealtime(0.05f);

            Assert.That(initialized, Is.True,
                "A previous Play Mode exit must not silently suppress every preview's startup callback.");
        }
    }

    [Test]
    public void SessionResetRunsBeforeSceneInitializationWithoutDomainReload()
    {
        var initialization = timerType.GetMethod("OnLoad", StaticMembers)
            .GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
        Assert.That(initialization, Is.Not.Null);
        Assert.That(initialization.loadType, Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration));
    }

    [UnityTest]
    public IEnumerator QuittingStillPreventsCreatingAnotherTimer()
    {
        timerType.GetProperty("IsQuitting", StaticMembers).SetValue(null, true);
        bool initialized = false;
        Schedule(() => initialized = true);
        yield return new WaitForSecondsRealtime(0.05f);
        Assert.That(initialized, Is.False);
        Assert.That((Component)timerType.GetField("_timerComponent", StaticMembers).GetValue(null), Is.Null);
    }

    [UnityTest]
    public IEnumerator DestroyedTimerIsReplacedForSubsequentPreviews()
    {
        bool firstInitialized = false;
        Schedule(() => firstInitialized = true);
        yield return new WaitForSecondsRealtime(0.05f);
        Assert.That(firstInitialized, Is.True);

        DestroyTimer();
        bool nextInitialized = false;
        Schedule(() => nextInitialized = true);
        yield return new WaitForSecondsRealtime(0.05f);
        Assert.That(nextInitialized, Is.True);
    }

    private void Schedule(Action action)
    {
        timerType.GetMethod("DelayedCall", StaticMembers).Invoke(null,
            new object[] { 0.01f, action, callbackTarget, true });
    }

    private void DestroyTimer()
    {
        var field = timerType.GetField("_timerComponent", StaticMembers);
        var component = (Component)field.GetValue(null);
        if (component != null) Object.DestroyImmediate(component.gameObject);
        field.SetValue(null, null);
    }
}

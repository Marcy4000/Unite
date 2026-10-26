using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BoostedAttackCounterLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (root != null) UnityEngine.Object.Destroy(root);
        yield return null;
    }

    [UnityTest]
    public IEnumerator CounterFollowsGaugeAcrossRealFramesAndDestroysWithHpBar()
    {
#if UNITY_EDITOR
        root = new GameObject("Runtime HP bar", typeof(RectTransform));
        Component hp = root.AddComponent(GameType("HPBar"));
        var barObject = new GameObject("HP", typeof(RectTransform));
        var bar = (RectTransform)barObject.transform;
        bar.SetParent(root.transform, false);
        bar.sizeDelta = new Vector2(2f, 0.3f);
        Set(hp, "barRectTransform", bar);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HPBarModules/BoostedAttackCounter.prefab");
        Set(hp, "boostedAttackCounterPrefab", prefab.GetComponent(GameType("BoostedAttackCounterModule")));
        Assert.That(root.GetComponentsInChildren(GameType("BoostedAttackCounterModule"), true), Is.Empty);

        Component counter = (Component)Call(hp, "GetOrCreateBoostedAttackCounter", 2);
        Call(counter, "SetState", 2, true);
        Image[] icons = counter.GetComponentsInChildren<Image>(true);
        Assert.That(icons.Count(i => i.gameObject.activeInHierarchy), Is.EqualTo(3));

        var gaugeObject = new GameObject("Gauge", typeof(RectTransform));
        var gaugeRect = (RectTransform)gaugeObject.transform;
        gaugeRect.SetParent(root.transform, false);
        gaugeRect.sizeDelta = new Vector2(2f, 0.2f);
        gaugeRect.anchoredPosition = new Vector2(0f, -0.5f);
        Component gauge = gaugeObject.AddComponent(GameType("GaugeModule"));
        Call(hp, "AddModule", gauge);
        // Two frames allow LateUpdate to run without a manual layout call.
        yield return null;
        yield return null;
        Assert.That(counter.transform.localPosition.y, Is.EqualTo(-0.64f).Within(0.0001f));
        Call(gauge, "SetVisibility", false);
        yield return null;
        yield return null;
        Assert.That(counter.transform.localPosition.y, Is.EqualTo(-0.19f).Within(0.0001f));

        Call(counter, "SetState", 0, false);
        Call(counter, "SetState", 0, true);
        CollectionAssert.AreEqual(icons, counter.GetComponentsInChildren<Image>(true));
        Assert.That(icons.Count(i => i.gameObject.activeInHierarchy), Is.EqualTo(1));
        UnityEngine.Object.Destroy(root);
        yield return null;
        Assert.That(counter == null, Is.True);
        Assert.That(icons.All(i => i == null), Is.True);
#else
        Assert.Ignore("Requires Editor-authored prefab.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator DestroyedHpBarUnsubscribesInitializedModules()
    {
#if UNITY_EDITOR
        root = new GameObject("Runtime module owner");
        var pokemonObject = new GameObject("Pokemon");
        pokemonObject.transform.SetParent(root.transform);
        Component pokemon = pokemonObject.AddComponent(GameType("Pokemon"));
        ((Behaviour)pokemon).enabled = false;
        Set(pokemon, "baseStats", AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Cinderace.asset"));

        var hpObject = new GameObject("HP bar", typeof(RectTransform));
        hpObject.transform.SetParent(root.transform);
        Component hp = hpObject.AddComponent(GameType("HPBar"));
        var moduleObject = new GameObject("EXP module", typeof(RectTransform));
        moduleObject.transform.SetParent(hpObject.transform);
        Component module = moduleObject.AddComponent(GameType("ExpModule"));
        Call(hp, "AddModule", module);
        hp.GetType().GetMethod("SetPokemon", new[] { GameType("Pokemon"), typeof(bool) })
            .Invoke(hp, new object[] { pokemon, true });
        FieldInfo eventField = pokemon.GetType().GetField("OnExpChange", Private);
        Assert.That(((Delegate)eventField.GetValue(pokemon)).GetInvocationList().Length, Is.EqualTo(2));
        UnityEngine.Object.Destroy(hpObject);
        yield return null;
        Assert.That(eventField.GetValue(pokemon), Is.Null);
#else
        Assert.Ignore("Requires Editor-authored stats.");
        yield break;
#endif
    }
}

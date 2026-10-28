using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class EnemyDeathInfoLayoutTests
{
    private GameObject root;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static void Call(Component target, string method, params object[] arguments) => target.GetType()
        .GetMethod(method, Private | BindingFlags.Public).Invoke(target, arguments);

    private Component CreateEntry()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnemyDeathInfo.prefab");
        var entry = Object.Instantiate(prefab, root.transform);
        return entry.GetComponent(GameType("EnemyDeathInfoUI"));
    }

    private void RebuildLayout() => GameType("UnityEngine.UI.LayoutRebuilder")
        .GetMethod("ForceRebuildLayoutImmediate").Invoke(null, new object[] { (RectTransform)root.transform });

    [SetUp]
    public void Setup()
    {
        root = new GameObject("Enemy layout regression fixture", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(813f, 93f);
        var defaults = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DefaultObjects.prefab");
        var layoutType = GameType("UnityEngine.UI.HorizontalLayoutGroup");
        var source = defaults.GetComponentsInChildren(layoutType, true)
            .First(component => component.name == "EnemyInfoHolder");
        EditorUtility.CopySerialized(source, root.AddComponent(layoutType));
    }

    [TearDown]
    public void Cleanup() => Object.DestroyImmediate(root);

    [Test]
    public void PrefabStartsHiddenWithoutReservingLayoutSpace()
    {
        var entry = CreateEntry();
        var layout = entry.GetComponent(GameType("UnityEngine.UI.LayoutElement"));
        Assert.That(layout, Is.Not.Null);
        Assert.That(layout.GetType().GetProperty("ignoreLayout").GetValue(layout), Is.True);
        Assert.That(entry.transform.Find("Holder").gameObject.activeSelf, Is.False);
        Assert.That(entry.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void HiddenEntriesLeaveNoLeadingOrMiddleGapsAndCanReappear()
    {
        var entries = Enumerable.Range(0, 4).Select(index => CreateEntry()).ToArray();
        foreach (var entry in entries) Call(entry, "SetVisible", true);
        Call(entries[0], "OnRespawn");
        Call(entries[2], "OnRespawn");
        RebuildLayout();
        float width = ((RectTransform)entries[1].transform).rect.width;
        float firstPosition = ((RectTransform)entries[1].transform).anchoredPosition.x;
        Assert.That(firstPosition, Is.EqualTo(width / 2f).Within(0.01f));
        Assert.That(((RectTransform)entries[3].transform).anchoredPosition.x - firstPosition,
            Is.EqualTo(width + 10f).Within(0.01f));
        Assert.That(entries.All(entry => entry.gameObject.activeSelf), Is.True);

        Call(entries[2], "OnDeath", Activator.CreateInstance(GameType("DamageInfo")));
        RebuildLayout();
        Assert.That(entries[2].transform.Find("Holder").gameObject.activeSelf, Is.True);
        Assert.That(((RectTransform)entries[2].transform).anchoredPosition.x - firstPosition,
            Is.EqualTo(width + 10f).Within(0.01f));
        Assert.That(((RectTransform)entries[3].transform).anchoredPosition.x - firstPosition,
            Is.EqualTo(2f * (width + 10f)).Within(0.01f));
    }

    [Test]
    public void ClearingPlayerHidesPreviouslyVisibleEntry()
    {
        var entry = CreateEntry();
        Call(entry, "SetVisible", true);
        Call(entry, "InitializeUI", new object[] { null });
        var layout = entry.GetComponent(GameType("UnityEngine.UI.LayoutElement"));
        Assert.That(layout.GetType().GetProperty("ignoreLayout").GetValue(layout), Is.True);
        Assert.That(entry.transform.Find("Holder").gameObject.activeSelf, Is.False);
    }
}

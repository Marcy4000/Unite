using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

public class VirtualClothingListTests
{
    private GameObject root;
    private Component list;
    private ScrollRect scroll;
    private Scene scene;
    private IList items;
    private readonly List<Object> assets = new List<Object>();
    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Get(object target, string field) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private IDictionary Visible => (IDictionary)Get(list, "visible");
    private IList Pool => (IList)Get(list, "rows");

    [SetUp]
    public void SetUp()
    {
        scene = EditorSceneManager.NewPreviewScene();
        root = new GameObject("Virtual wardrobe tests", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(root, scene);
        var viewportObject = new GameObject("Viewport", typeof(RectTransform));
        viewportObject.transform.SetParent(root.transform, false);
        var viewport = (RectTransform)viewportObject.transform;
        viewport.sizeDelta = new Vector2(1000f, 500f);
        var contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewport, false);
        var content = (RectTransform)contentObject.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding.top = 36;
        layout.spacing = 15f;
        layout.childAlignment = TextAnchor.UpperCenter;
        scroll = root.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        var group = root.AddComponent<ToggleGroup>();
        list = root.AddComponent(GameType("VirtualClothingList"));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/ClothesMenu/ClothingSelectionItem.prefab");
        Call(list, "Initialize", scroll, prefab, group);
        items = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(GameType("ClothingItem")));
        for (int i = 0; i < 1000; i++)
        {
            var item = ScriptableObject.CreateInstance(GameType("ClothingItem"));
            item.name = "Test clothing " + i;
            assets.Add(item);
            items.Add(item); // No sprite keys: layout tests must not load game assets.
        }
    }

    [Test]
    public void ThousandItemsOnlyCreateViewportSizedPool()
    {
        Call(list, "SetItems", items, 0, (Action<int>)(_ => { }));
        Assert.That(Pool.Count, Is.InRange(1, 7));
        Assert.That(Visible.Count, Is.EqualTo(Pool.Count));
        Assert.That(scroll.content.rect.height, Is.GreaterThan(180000f));
        Assert.That(scroll.content.GetComponent<VerticalLayoutGroup>().enabled, Is.False);
        Assert.That(scroll.content.GetComponent<ContentSizeFitter>().enabled, Is.False);
    }

    [Test]
    public void ScrollingRecyclesRowsAndRetainsOverlappingBindings()
    {
        Call(list, "SetItems", items, 0, (Action<int>)(_ => { }));
        ScrollTo(500f);
        int index = Visible.Keys.Cast<int>().OrderBy(value => value).Skip(2).First();
        var retained = Visible[index];
        ScrollTo(710f);
        Assert.That(Visible[index], Is.SameAs(retained));
        ScrollTo(50000f);
        Assert.That(Pool.Count, Is.LessThanOrEqualTo(7));
        foreach (DictionaryEntry entry in Visible)
            Assert.That(Property(Get(entry.Value, "Item"), "Item"), Is.SameAs(items[(int)entry.Key]));
    }

    [Test]
    public void OffscreenSelectionDoesNotSelectFirstVisibleItemOrInvokeCallbacks()
    {
        int calls = 0;
        int selected = -1;
        Call(list, "SetItems", items, 500, (Action<int>)(index => { calls++; selected = index; }));
        Assert.That(calls, Is.Zero);
        foreach (DictionaryEntry entry in Visible)
            Assert.That(((Toggle)Property(Get(entry.Value, "Item"), "ItemToggle")).isOn, Is.False);
        var row = Visible[1];
        ((Toggle)Property(Get(row, "Item"), "ItemToggle")).isOn = true;
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(selected, Is.EqualTo(1));
        ScrollTo(50000f);
        ScrollTo(0f);
        Assert.That(((Toggle)Property(Get(Visible[1], "Item"), "ItemToggle")).isOn, Is.True);
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void SwitchingCategoryReusesPoolAndEmptyCategoryHidesEveryRow()
    {
        Call(list, "SetItems", items, 0, (Action<int>)(_ => { }));
        int count = Pool.Count;
        Call(list, "SetItems", items, 3, (Action<int>)(_ => { }));
        Assert.That(Pool.Count, Is.EqualTo(count));
        Call(list, "SetItems", null, 0, (Action<int>)(_ => { }));
        Assert.That(Visible.Count, Is.Zero);
        foreach (var row in Pool)
            Assert.That(((Component)Get(row, "Item")).gameObject.activeSelf, Is.False);
    }

    [Test]
    public void RebindingAndHidingRowsReleaseTheirIconHandles()
    {
        Call(list, "SetItems", items, 0, (Action<int>)(_ => { }));
        var item = Get(Visible[0], "Item");
        var handle = Addressables.ResourceManager.CreateCompletedOperation<Sprite>(null, null);
        item.GetType().GetField("iconHandle", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(item, handle);
        Call(item, "SetItem", items[50]);
        Assert.That(handle.IsValid(), Is.False);
        Assert.That(((Image)Get(item, "itemIcon")).enabled, Is.False);
        handle = Addressables.ResourceManager.CreateCompletedOperation<Sprite>(null, null);
        item.GetType().GetField("iconHandle", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(item, handle);
        Call(item, "OnDisable");
        Assert.That(handle.IsValid(), Is.False);
        Assert.DoesNotThrow(() => Call(item, "OnDisable"));
    }

    [TestCase(0, 0f)]
    [TestCase(1000, -100f)]
    [TestCase(1000, 50000f)]
    [TestCase(1000, 1000000f)]
    public void VisibleRangeRemainsBoundedDuringElasticOverscroll(int count, float offset)
    {
        var range = (Vector2Int)GameType("VirtualClothingList").GetMethod("GetVisibleRange")
            .Invoke(null, new object[] { count, 500f, offset, 188f, 15f, 36f });
        Assert.That(range.x, Is.GreaterThanOrEqualTo(0));
        Assert.That(range.y, Is.LessThanOrEqualTo(count));
        Assert.That(range.y - range.x, Is.InRange(0, 7));
    }

    private void ScrollTo(float offset)
    {
        scroll.content.anchoredPosition = new Vector2(0f, offset);
        Call(list, "RefreshVisibleRows");
    }

    [TearDown]
    public void TearDown()
    {
        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        foreach (var asset in assets) Object.DestroyImmediate(asset);
        assets.Clear();
    }
}

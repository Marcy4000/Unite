using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class BoostedAttackCounterTests
{
    private const string CounterPath = "Assets/Prefabs/HPBarModules/BoostedAttackCounter.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Color ReadyColor = new Color(1f, 1f, 0f, 1f);
    private GameObject root;
    private Component hpBar;
    private RectTransform bar;
    private RectTransform legacyGauge;

    // Runtime code is in Assembly-CSharp; follow the project's reflection-based test pattern.
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method).Invoke(target, args);
    private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("HP bar test", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(5f, 2f);
        hpBar = root.AddComponent(GameType("HPBar"));
        bar = ChildRect("HP", new Vector2(2f, 0.3f), Vector2.zero);
        legacyGauge = ChildRect("Legacy gauge", new Vector2(2f, 0.2f), new Vector2(0f, -0.3f));
        legacyGauge.gameObject.SetActive(false);
        Set(hpBar, "barRectTransform", bar);
        Set(hpBar, "generigGuage", legacyGauge.gameObject);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CounterPath);
        Assert.That(prefab, Is.Not.Null);
        Set(hpBar, "boostedAttackCounterPrefab", prefab.GetComponent(GameType("BoostedAttackCounterModule")));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    private RectTransform ChildRect(string name, Vector2 size, Vector2 position)
    {
        var child = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)child.transform;
        rect.SetParent(root.transform, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private Component Counter(int capacity = 2) => (Component)Call(hpBar, "GetOrCreateBoostedAttackCounter", capacity);
    private static Image[] VisibleIcons(Component counter) => counter.GetComponentsInChildren<Image>(true)
        .Where(i => i.gameObject.activeSelf).ToArray();
    private void Layout() => hpBar.GetType().GetMethod("LateUpdate", Private).Invoke(hpBar, null);

    [Test]
    public void MainPrefabReferencesCounterButDoesNotEnableItForCharacters()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HpCanvas.prefab");
        var data = new SerializedObject(prefab.GetComponent(GameType("HPBar")));
        Assert.That(data.FindProperty("boostedAttackCounterPrefab").objectReferenceValue, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren(GameType("BoostedAttackCounterModule"), true), Is.Empty);
        Assert.That(root.GetComponentsInChildren(GameType("BoostedAttackCounterModule"), true), Is.Empty);
    }

    [TestCase(0, false, 0)]
    [TestCase(1, false, 1)]
    [TestCase(2, false, 2)]
    [TestCase(2, true, 3)]
    [TestCase(0, true, 1)]
    [TestCase(-4, false, 0)]
    [TestCase(100, true, 3)]
    public void EarnedChargesAreWhiteAndOnlyReadyIconIsYellow(int normal, bool ready, int expected)
    {
        Component counter = Counter();
        Call(counter, "SetState", normal, ready);
        Image[] icons = VisibleIcons(counter);
        Assert.That(icons.Length, Is.EqualTo(expected));
        Assert.That(Property<int>(counter, "VisibleIndicatorCount"), Is.EqualTo(expected));
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GenericUI/Common/t_common_bg_id.png");
        Assert.That(sprite, Is.Not.Null);
        for (int i = 0; i < icons.Length; i++)
        {
            Assert.That(icons[i].sprite, Is.SameAs(sprite));
            Assert.That(icons[i].color, Is.EqualTo(ready && i == icons.Length - 1 ? ReadyColor : Color.white));
            Assert.That(icons[i].raycastTarget, Is.False);
        }
    }

    [Test]
    public void CounterIsSingletonAndChangingCapacityClampsAndReusesIcons()
    {
        Component counter = Counter(4);
        Call(counter, "SetState", 4, true);
        Image[] before = counter.GetComponentsInChildren<Image>(true);
        Assert.That(before.Length, Is.EqualTo(5));
        Assert.That(Counter(1), Is.SameAs(counter));
        Assert.That(Property<int>(counter, "NormalCharges"), Is.EqualTo(1));
        Assert.That(VisibleIcons(counter).Length, Is.EqualTo(2));
        Call(counter, "Configure", -1);
        Assert.That(Property<int>(counter, "MaxNormalCharges"), Is.Zero);
        Assert.That(VisibleIcons(counter).Single().color, Is.EqualTo(ReadyColor));
        Call(counter, "Configure", 4);
        Call(counter, "SetState", 4, true);
        CollectionAssert.AreEqual(before, counter.GetComponentsInChildren<Image>(true));
    }

    [Test]
    public void HideShowPreservesStateWhileResetAndCleanupClearIt()
    {
        Component counter = Counter();
        Call(counter, "SetState", 2, true);
        Call(counter, "SetVisibility", false);
        Assert.That(counter.gameObject.activeSelf, Is.False);
        Call(counter, "SetVisibility", true);
        Assert.That(VisibleIcons(counter).Length, Is.EqualTo(3));
        Call(counter, "ResetCounter");
        Assert.That(VisibleIcons(counter), Is.Empty);
        Call(counter, "SetNormalCharges", 1);
        Call(counter, "SetBoostedReady", true);
        Assert.That(VisibleIcons(counter).Length, Is.EqualTo(2));
        Call(counter, "Cleanup");
        Assert.That(VisibleIcons(counter), Is.Empty);
    }

    [Test]
    public void StoredBoostedAttacksUseYellowIconsAndReturnToNormalCycleWithoutExtras()
    {
        Component counter = Counter(1);
        Call(counter, "SetChargeCounts", 0, 3);
        Assert.That(VisibleIcons(counter).Length, Is.EqualTo(3));
        Assert.That(VisibleIcons(counter).All(i => i.color == ReadyColor), Is.True);
        Call(counter, "SetChargeCounts", 0, 1);
        Assert.That(VisibleIcons(counter).Single().color, Is.EqualTo(ReadyColor));
        Call(counter, "SetState", 1, true);
        Assert.That(VisibleIcons(counter).Select(i => i.color).ToArray(), Is.EqualTo(new[] { Color.white, ReadyColor }));
        Call(counter, "SetChargeCounts", 0, -1);
        Assert.That(VisibleIcons(counter), Is.Empty);
    }

    [Test]
    public void IndicatorsStayLeftAlignedAsChargesChangeAndDoNotOverlap()
    {
        Component counter = Counter();
        Call(counter, "SetState", 2, true);
        Image[] icons = VisibleIcons(counter);
        Assert.That(counter.transform.localPosition.x, Is.EqualTo(bar.rect.xMin).Within(0.0001f));
        Assert.That(((RectTransform)counter.transform).pivot, Is.EqualTo(new Vector2(0f, 1f)));
        Assert.That(icons[0].rectTransform.anchoredPosition.x, Is.Zero);
        for (int i = 1; i < icons.Length; i++)
            Assert.That(icons[i].rectTransform.anchoredPosition.x - icons[i - 1].rectTransform.anchoredPosition.x,
                Is.GreaterThan(icons[i].rectTransform.rect.width));
        Vector3 firstPosition = icons[0].transform.position;
        Call(counter, "SetState", 1, false);
        Layout();
        Assert.That(VisibleIcons(counter).Single().transform.position, Is.EqualTo(firstPosition));
        Call(counter, "SetState", 0, false);
        Call(counter, "SetState", 2, true);
        Layout();
        Assert.That(VisibleIcons(counter)[0].transform.position, Is.EqualTo(firstPosition));
    }

    [Test]
    public void CounterMovesBelowLegacyGaugeAndCollapsesGapWhenHidden()
    {
        Component counter = Counter();
        Assert.That(counter.transform.localPosition.y, Is.EqualTo(-0.19f).Within(0.0001f));
        Call(hpBar, "ShowGenericGuage", true);
        Assert.That(counter.transform.localPosition.y, Is.EqualTo(-0.44f).Within(0.0001f));
        Call(hpBar, "ShowGenericGuage", false);
        Assert.That(counter.transform.localPosition.y, Is.EqualTo(-0.19f).Within(0.0001f));
    }

    [Test]
    public void VisibleVisionIconDoesNotOverlapCounter()
    {
        Component counter = Counter();
        RectTransform eye = ChildRect("Vision", new Vector2(0.373f, 0.373f), new Vector2(0f, -0.3105f));
        Set(hpBar, "eyeIcon", eye.gameObject);
        Layout();
        Assert.That(counter.transform.localPosition.y, Is.EqualTo(-0.537f).Within(0.0001f));
        eye.gameObject.SetActive(false);
        Layout();
        Assert.That(counter.transform.localPosition.y, Is.EqualTo(-0.19f).Within(0.0001f));
    }

    [Test]
    public void CounterTracksModularGaugeVisibilityMovementAndRemoval()
    {
        Component counter = Counter();
        RectTransform gaugeRect = ChildRect("Module gauge", new Vector2(2f, 0.2f), new Vector2(0f, -0.6f));
        Component gauge = gaugeRect.gameObject.AddComponent(GameType("GaugeModule"));
        Call(hpBar, "AddModule", gauge);
        Assert.That(counter.transform.localPosition.y, Is.EqualTo(-0.74f).Within(0.0001f));
        gaugeRect.anchoredPosition = new Vector2(0f, -0.8f);
        Layout();
        Assert.That(counter.transform.localPosition.y, Is.EqualTo(-0.94f).Within(0.0001f));
        Call(gauge, "SetVisibility", false);
        Layout();
        Assert.That(counter.transform.localPosition.y, Is.EqualTo(-0.19f).Within(0.0001f));
        Call(gauge, "SetVisibility", true);
        hpBar.GetType().GetMethod("RemoveModule", new[] { GameType("IHPBarModule"), typeof(bool) })
            .Invoke(hpBar, new object[] { gauge, false });
        Layout();
        Assert.That(counter.transform.localPosition.y, Is.EqualTo(-0.19f).Within(0.0001f));
    }

    [Test]
    public void LayoutSupportsScaledNestedModuleContainer()
    {
        RectTransform container = ChildRect("Modules", new Vector2(3f, 1f), new Vector2(0.2f, 0.4f));
        container.localScale = new Vector3(0.5f, 0.5f, 1f);
        Set(hpBar, "modulesContainer", container);
        Component counter = Counter();
        var rect = (RectTransform)counter.transform;
        Assert.That(rect.parent, Is.SameAs(container));
        float bottomInContainer = container.InverseTransformPoint(bar.TransformPoint(new Vector3(0f, bar.rect.yMin, 0f))).y;
        Assert.That(rect.localPosition.y, Is.EqualTo(bottomInContainer - 0.04f).Within(0.0001f));
        Assert.That(root.transform.InverseTransformPoint(rect.position).x, Is.EqualTo(bar.rect.xMin).Within(0.0001f));
    }

    [Test]
    public void RemovingAndRecreatingCounterDoesNotReuseStaleState()
    {
        Component counter = Counter();
        Call(counter, "SetState", 2, true);
        hpBar.GetType().GetMethod("RemoveModule", new[] { GameType("IHPBarModule"), typeof(bool) })
            .Invoke(hpBar, new object[] { counter, false });
        Assert.That(VisibleIcons(counter), Is.Empty);
        Object.DestroyImmediate(counter.gameObject);
        Component replacement = Counter();
        Assert.That(replacement, Is.Not.SameAs(counter));
        Assert.That(VisibleIcons(replacement), Is.Empty);
    }

    [TestCase("prefab")]
    [TestCase("before")]
    [TestCase("after")]
    public void ModulesInitializeExactlyOnceAndUnsubscribeWhenRemoved(string registration)
    {
        Component pokemon = CreatePokemon();
        Component module = ChildRect("EXP module", Vector2.one, Vector2.zero).gameObject.AddComponent(GameType("ExpModule"));
        if (registration == "prefab") ((IList)hpBar.GetType().GetField("prefabModules", Private).GetValue(hpBar)).Add(module);
        if (registration == "before") Call(hpBar, "AddModule", module);
        Bind(pokemon);
        if (registration == "after") Call(hpBar, "AddModule", module);
        Call(hpBar, "AddModule", module); // Re-registering the same component is a no-op.
        Assert.That(Subscriptions(pokemon, module), Is.EqualTo(1));
        Assert.That(((IList)hpBar.GetType().GetProperty("ActiveModules").GetValue(hpBar)).Count, Is.EqualTo(1));
        hpBar.GetType().GetMethod("RemoveModule", new[] { GameType("IHPBarModule"), typeof(bool) })
            .Invoke(hpBar, new object[] { module, false });
        Assert.That(Subscriptions(pokemon, module), Is.Zero);
    }

    [Test]
    public void RebindingTransfersSubscriptionsAndDoesNotDuplicateThem()
    {
        Component first = CreatePokemon();
        Component second = CreatePokemon();
        Component module = ChildRect("EXP module", Vector2.one, Vector2.zero).gameObject.AddComponent(GameType("ExpModule"));
        Call(hpBar, "AddModule", module);
        Bind(first);
        Bind(second);
        Bind(second);
        Assert.That(Subscriptions(first, module), Is.Zero);
        Assert.That(Subscriptions(second, module), Is.EqualTo(1));
        // Non-ExecuteAlways components do not receive engine lifecycle callbacks
        // in EditMode. Exercise the teardown callback explicitly here.
        hpBar.GetType().GetMethod("OnDestroy", Private).Invoke(hpBar, null);
        Assert.That(Subscriptions(second, module), Is.Zero);
    }

    private Component CreatePokemon()
    {
        var node = new GameObject("Test Pokemon");
        node.transform.SetParent(root.transform);
        Component pokemon = node.AddComponent(GameType("Pokemon"));
        if (pokemon.GetType().GetField("statChanges", Private).GetValue(pokemon) == null)
            pokemon.GetType().GetMethod("Awake", Private).Invoke(pokemon, null);
        Set(pokemon, "baseStats", AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Cinderace.asset"));
        return pokemon;
    }

    private void Bind(Component pokemon) => hpBar.GetType().GetMethod("SetPokemon", new[] { GameType("Pokemon"), typeof(bool) })
        .Invoke(hpBar, new object[] { pokemon, true });

    private static int Subscriptions(Component pokemon, Component module)
    {
        var handler = (Delegate)pokemon.GetType().GetField("OnExpChange", Private).GetValue(pokemon);
        return handler == null ? 0 : handler.GetInvocationList().Count(d => ReferenceEquals(d.Target, module));
    }
}

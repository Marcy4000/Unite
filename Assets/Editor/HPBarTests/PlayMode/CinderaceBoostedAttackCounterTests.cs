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

// Isolate owner-side presentation from lobby, input, and server combat. The fixture
// supplies replicated charge/health snapshots; production attack rules are unchanged.
public class CinderaceBoostedAttackCounterTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private Component pokemon, manager, combat, hpBar;
    private Component counterPrefab;
    private object attack;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static T Property<T>(object target, string property) => (T)target.GetType().GetProperty(property).GetValue(target);

    private static void Snapshot(object target, string field, object value)
    {
        object variable = target.GetType().GetField(field, Private).GetValue(target);
        variable.GetType().GetField("m_InternalValue", Private).SetValue(variable, value);
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        root = new GameObject("Cinderace counter fixture");
        pokemon = root.AddComponent(GameType("Pokemon"));
        ((Behaviour)pokemon).enabled = false;
        Set(pokemon, "baseStats", AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Cinderace.asset"));
        Snapshot(pokemon, "currentHp", 1000);
        combat = root.AddComponent(GameType("MovesController"));
        ((Behaviour)combat).enabled = false;
        manager = root.AddComponent(GameType("PlayerManager"));
        ((Behaviour)manager).enabled = false;
        Set(combat, "pokemon", pokemon);
        Set(combat, "playerManager", manager);
        counterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HPBarModules/BoostedAttackCounter.prefab")
            .GetComponent(GameType("BoostedAttackCounterModule"));
        hpBar = CreateHpBar();
        Set(manager, "hpBar", hpBar);
        attack = Activator.CreateInstance(GameType("CinderBasicAtk"));
        yield return null;
#else
        Assert.Ignore("Requires Editor-authored prefab and Pokemon data.");
        yield break;
#endif
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (attack != null) Call(attack, "Dispose");
        if (root != null) UnityEngine.Object.Destroy(root);
        yield return null;
    }

    private Component CreateHpBar()
    {
        var node = new GameObject("HP", typeof(RectTransform));
        node.transform.SetParent(root.transform, false);
        Component hp = node.AddComponent(GameType("HPBar"));
        var bar = new GameObject("Bar", typeof(RectTransform));
        bar.transform.SetParent(node.transform, false);
        ((RectTransform)bar.transform).sizeDelta = new Vector2(2f, 0.3f);
        Set(hp, "barRectTransform", bar.transform);
        Set(hp, "boostedAttackCounterPrefab", counterPrefab);
        return hp;
    }

    private Component Counter => hpBar.GetComponentsInChildren(GameType("BoostedAttackCounterModule"), true).Single();
    private int ModuleCount(Component hp) => ((ICollection)hp.GetType().GetProperty("ActiveModules").GetValue(hp)).Count;
    private void Initialize() => Call(attack, "Initialize", manager);
    private void Tick() => Call(attack, "Update");
    private void Charge(byte value) => Snapshot(combat, "cinderAttackCharge", value);

    private void AssertState(int normal, bool ready)
    {
        Component counter = Counter;
        Assert.That(Property<int>(counter, "MaxNormalCharges"), Is.EqualTo(1));
        Assert.That(Property<int>(counter, "NormalCharges"), Is.EqualTo(normal));
        Assert.That(Property<bool>(counter, "BoostedReady"), Is.EqualTo(ready));
        Image[] icons = counter.GetComponentsInChildren<Image>(true).Where(i => i.gameObject.activeSelf).ToArray();
        Assert.That(icons.Length, Is.EqualTo(normal + (ready ? 1 : 0)));
        for (int i = 0; i < icons.Length; i++)
        {
            Assert.That(icons[i].color, Is.EqualTo(ready && i == normal ? new Color(1f, 1f, 0f, 1f) : Color.white));
            Assert.That(icons[i].sprite.name, Is.EqualTo("t_common_bg_id"));
        }
    }

    [UnityTest]
    public IEnumerator NormalCycleTracksReplicatedStateWithoutAdvancingItLocally()
    {
        Initialize();
        AssertState(0, false);
        Charge(1); Tick();
        AssertState(1, false);
        Tick(); Tick(); // Idle/failed attacks cannot invent another charge.
        AssertState(1, false);
        Assert.That(Property<int>(combat, "CinderaceAttackCharge"), Is.EqualTo(1));
        Charge(2); Tick();
        AssertState(1, true);
        Charge(0); Tick();
        AssertState(0, false);
        yield return null;
    }

    [UnityTest]
    public IEnumerator InitialAndMoveGrantedBoostsDisplayWithoutPerformingBasicAttack()
    {
        Charge(2);
        Initialize();
        AssertState(1, true);
        Charge(0); Tick();
        AssertState(0, false);
        Charge(2); Tick(); // Move grants readiness via the same server variable.
        AssertState(1, true);
        Assert.That(Property<int>(combat, "CinderaceAttackCharge"), Is.EqualTo(2));
        yield return null;
    }

    [UnityTest]
    public IEnumerator DeathResetClearsUiWithoutWritingCombatStateAndRespawnStartsEmpty()
    {
        Charge(2); Initialize();
        AssertState(1, true);
        Call(attack, "ResetAttack");
        AssertState(0, false);
        Assert.That(Property<int>(combat, "CinderaceAttackCharge"), Is.EqualTo(2));
        Snapshot(pokemon, "currentHp", 0);
        Tick(); // Health may arrive before the server's charge-reset packet.
        AssertState(0, false);
        Charge(0);
        Snapshot(pokemon, "currentHp", 1000);
        Tick();
        AssertState(0, false);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DelayedAndReplacedHpBarsDoNotLeaveDuplicateOrStaleCounters()
    {
        Set(manager, "hpBar", null);
        Initialize(); Tick();
        Assert.That(ModuleCount(hpBar), Is.Zero);
        Set(manager, "hpBar", hpBar);
        Charge(2); Tick();
        AssertState(1, true);
        Component oldHp = hpBar;
        Component oldCounter = Counter;
        hpBar = CreateHpBar();
        Set(manager, "hpBar", hpBar);
        Tick();
        Assert.That(ModuleCount(oldHp), Is.Zero);
        Assert.That(ModuleCount(hpBar), Is.EqualTo(1));
        AssertState(1, true);
        yield return null;
        Assert.That(oldCounter == null, Is.True);
        Call(attack, "Dispose");
        Call(attack, "Dispose");
        Tick();
        Assert.That(ModuleCount(hpBar), Is.Zero);
        Assert.That(Property<int>(combat, "CinderaceAttackCharge"), Is.EqualTo(2));
        yield return null;
        Assert.That(hpBar.GetComponentsInChildren(GameType("BoostedAttackCounterModule"), true), Is.Empty);
    }

    [UnityTest]
    public IEnumerator RelearningAndHpRebindingKeepOnlyOneSharedCounter()
    {
        Charge(2);
        Call(combat, "LearnBasicAttack", attack);
        AssertState(1, true);
        object replacement = Activator.CreateInstance(GameType("CinderBasicAtk"));
        Call(combat, "LearnBasicAttack", replacement);
        attack = replacement;
        Assert.That(ModuleCount(hpBar), Is.EqualTo(1));
        yield return null;
        AssertState(1, true);
        hpBar.GetType().GetMethod("SetPokemon", new[] { GameType("Pokemon"), typeof(bool) })
            .Invoke(hpBar, new object[] { pokemon, true });
        Tick();
        AssertState(1, true);
        Assert.That(ModuleCount(hpBar), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator SharedCounterDoesNotReplaceOrHideAnAbilityGauge()
    {
        var gauge = new GameObject("Ability gauge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gauge.transform.SetParent(hpBar.transform, false);
        var rect = (RectTransform)gauge.transform;
        rect.sizeDelta = new Vector2(2f, 0.2f);
        rect.anchoredPosition = new Vector2(0f, -0.3f);
        gauge.GetComponent<Image>().fillAmount = 0.37f;
        Set(hpBar, "generigGuage", gauge);
        Initialize();
        Charge(2); Tick();
        yield return null;
        AssertState(1, true);
        Assert.That(gauge.activeSelf, Is.True);
        Assert.That(gauge.GetComponent<Image>().fillAmount, Is.EqualTo(0.37f));
        Assert.That(Counter.transform.localPosition.y, Is.EqualTo(-0.44f).Within(0.0001f));
        Assert.That(Counter.transform.localPosition.x, Is.EqualTo(-1f).Within(0.0001f));
        Call(attack, "Dispose");
        Assert.That(gauge.activeSelf, Is.True);
    }

    [UnityTest]
    public IEnumerator ChargeIsOwnerReadableAndServerWritableOnly()
    {
        object variable = combat.GetType().GetField("cinderAttackCharge", Private).GetValue(combat);
        Assert.That(variable.GetType().GetField("ReadPerm").GetValue(variable).ToString(), Is.EqualTo("Owner"));
        Assert.That(variable.GetType().GetProperty("WritePerm").GetValue(variable).ToString(), Is.EqualTo("Server"));
        // Initializing the combat component alone never creates a personal UI.
        Assert.That(ModuleCount(hpBar), Is.Zero);
        yield return null;
    }
}

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

// Feed real attack implementations combat-state snapshots without starting lobby,
// input, or RPC/projectile loading. Only their actual Update/presentation is run.
public class PlayableAttackCounterTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] Cycles = { "CinderBasicAtk", "GlaceBasicAtk", "SylvBasicAtk", "VaporBasicAtk", "JoltBasicAtk",
        "FlygonBasicAtk", "MeowsticMBasicAtk", "MarshadowBasicAtk", "EmboarBasicAtk", "FlareonBasicAtk", "DuraBasicAtk" };
    private GameObject root, gauge;
    private Component manager, pokemon, combat, hp;
    private object attack;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static void Snapshot(object target, string field, object value)
    {
        object variable = target.GetType().GetField(field, Private).GetValue(target);
        variable.GetType().GetField("m_InternalValue", Private).SetValue(variable, value);
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        root = new GameObject("Playable attack UI fixture");
        root.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        pokemon = root.AddComponent(GameType("Pokemon")); ((Behaviour)pokemon).enabled = false;
        Set(pokemon, "baseStats", AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Cinderace.asset"));
        Snapshot(pokemon, "currentHp", 1000);
        combat = root.AddComponent(GameType("MovesController")); ((Behaviour)combat).enabled = false;
        manager = root.AddComponent(GameType("PlayerManager")); ((Behaviour)manager).enabled = false;
        Set(combat, "pokemon", pokemon); Set(combat, "playerManager", manager);
        var node = new GameObject("HP", typeof(RectTransform)); node.transform.SetParent(root.transform, false);
        hp = node.AddComponent(GameType("HPBar")); Set(manager, "hpBar", hp);
        var bar = new GameObject("Bar", typeof(RectTransform)); bar.transform.SetParent(node.transform, false);
        ((RectTransform)bar.transform).sizeDelta = new Vector2(2f, 0.3f); Set(hp, "barRectTransform", bar.transform);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HPBarModules/BoostedAttackCounter.prefab");
        Set(hp, "boostedAttackCounterPrefab", prefab.GetComponent(GameType("BoostedAttackCounterModule")));
        gauge = new GameObject("Preserved special gauge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gauge.transform.SetParent(node.transform, false);
        ((RectTransform)gauge.transform).sizeDelta = new Vector2(2f, 0.2f);
        ((RectTransform)gauge.transform).anchoredPosition = new Vector2(0f, -0.3f);
        gauge.GetComponent<Image>().fillAmount = 0.37f; Set(hp, "generigGuage", gauge);
        yield return null;
#else
        Assert.Ignore("Requires Editor-authored sprite and Pokemon data."); yield break;
#endif
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (attack != null) Call(attack, "Dispose");
        if (root != null) UnityEngine.Object.Destroy(root);
        yield return null;
    }

    private object Bind(string name)
    {
        object value = Activator.CreateInstance(GameType(name));
        value.GetType().GetField("playerManager").SetValue(value, manager);
        FieldInfo cooldown = value.GetType().GetField("cooldown", Private);
        if (cooldown != null) cooldown.SetValue(value, 10f);
        return value;
    }
    private void Charge(int amount)
    {
        string network = attack.GetType().Name switch
        {
            "CinderBasicAtk" => "cinderAttackCharge", "VaporBasicAtk" => "vaporAttackCharge",
            "MeowsticMBasicAtk" => "meowsticAttackCharge", "EmboarBasicAtk" => "emboarAttackCharge",
            "FlareonBasicAtk" => "flareonAttackCharge", _ => null
        };
        if (network != null) Snapshot(combat, network, (byte)amount);
        else
        {
            FieldInfo field = attack.GetType().GetField("charge", Private);
            field.SetValue(attack, Convert.ChangeType(amount, field.FieldType));
        }
    }
    private Component Counter => hp.GetComponentsInChildren(GameType("BoostedAttackCounterModule"), true).Single();
    private void AssertProgress(int charge)
    {
        Component counter = Counter;
        Image[] icons = counter.GetComponentsInChildren<Image>(true).Where(i => i.gameObject.activeSelf).ToArray();
        Assert.That(icons.Length, Is.EqualTo(charge), attack.GetType().Name);
        for (int i = 0; i < icons.Length; i++)
        {
            Assert.That(icons[i].color, Is.EqualTo(charge == 2 && i == 1 ? new Color(1, 1, 0, 1) : Color.white));
            Assert.That(icons[i].sprite.name, Is.EqualTo("t_common_bg_id"));
        }
        Assert.That(Get(counter, "IsNetworked"), Is.EqualTo(false));
        Assert.That(counter.transform.localPosition.x, Is.EqualTo(-1f).Within(0.0001f));
        Assert.That(counter.transform.localPosition.y, Is.EqualTo(-0.44f).Within(0.0001f));
        Assert.That(gauge.activeSelf, Is.True);
        Assert.That(gauge.GetComponent<Image>().fillAmount, Is.EqualTo(0.37f));
    }

    [UnityTest]
    public IEnumerator EveryDiscreteAttackUsesTheSameTwoSlotCycleAndPreservesSpecialGauge()
    {
        foreach (string name in Cycles)
        {
            attack = Bind(name);
            foreach (int charge in new[] { 0, 1, 2, 0 })
            {
                Charge(charge); Call(attack, "Update");
                yield return null; AssertProgress(charge);
            }
            Charge(2); Call(attack, "Update");
            Call(attack, "ResetAttack"); AssertProgress(0);
            Snapshot(pokemon, "currentHp", 0); Call(attack, "Update"); AssertProgress(0);
            Charge(0); Snapshot(pokemon, "currentHp", 1000); Call(attack, "Update"); AssertProgress(0);
            Call(attack, "Dispose"); Call(attack, "Dispose"); attack = null;
            yield return null;
            Assert.That(hp.GetComponentsInChildren(GameType("BoostedAttackCounterModule"), true), Is.Empty, name);
            Assert.That(gauge.activeSelf, Is.True);
        }
    }

    [UnityTest]
    public IEnumerator LocalExpiryAndGrantedBoostsRemainDrivenByOriginalAttackLogic()
    {
        foreach (string name in new[] { "GlaceBasicAtk", "SylvBasicAtk" })
        {
            attack = Bind(name); Charge(2); Call(attack, "Update");
            yield return null; AssertProgress(2);
            Set(attack, "cooldown", 0f); Call(attack, "Update"); AssertProgress(0);
            if (name == "GlaceBasicAtk")
            {
                Call(attack, "FillChargeAmount"); Call(attack, "Update"); AssertProgress(2);
                Call(attack, "ChangeBasicAtkType", (byte)1); Call(attack, "Update");
                Assert.That(Counter.gameObject.activeSelf, Is.False);
                Assert.That(gauge.activeSelf, Is.True);
                Call(attack, "ChangeBasicAtkType", (byte)0); Call(attack, "Update"); AssertProgress(2);
            }
            Call(attack, "Dispose"); attack = null; yield return null;
        }
        attack = Bind("DuraBasicAtk"); Call(attack, "GainBoostedAttack"); Call(attack, "Update");
        yield return null; AssertProgress(2);
    }

    [UnityTest]
    public IEnumerator TimedBasicAttackGaugesRemainTheirOnlyChargeUi()
    {
        foreach (string name in new[] { "CresseliaBasicAtk", "SerperiorBasicAtk" })
        {
            attack = Activator.CreateInstance(GameType(name)); Call(attack, "Initialize", manager);
            Call(attack, "Update"); yield return null;
            Assert.That(hp.GetComponentsInChildren(GameType("BoostedAttackCounterModule"), true), Is.Empty, name);
            Assert.That(gauge.activeSelf, Is.True, name);
            Assert.That(gauge.GetComponent<Image>().fillAmount, Is.InRange(0f, 1f));
            Call(attack, "Dispose"); attack = null;
        }
    }

    [UnityTest]
    public IEnumerator EmboarStoredPledgeAttacksUseRemainingYellowIconsWithoutTouchingRecklessGauge()
    {
        attack = Bind("EmboarBasicAtk"); Charge(2);
        foreach (int hits in new[] { 3, 2, 1 })
        {
            Snapshot(combat, "emboarPledgeHits", hits); Call(attack, "Update"); yield return null;
            Image[] icons = Counter.GetComponentsInChildren<Image>(true).Where(i => i.gameObject.activeSelf).ToArray();
            Assert.That(icons.Length, Is.EqualTo(hits));
            Assert.That(icons.All(i => i.color == new Color(1, 1, 0, 1)), Is.True);
            Assert.That(gauge.GetComponent<Image>().fillAmount, Is.EqualTo(0.37f));
        }
        Snapshot(combat, "emboarPledgeHits", 0); Charge(0); Call(attack, "Update"); AssertProgress(0);
    }

    [UnityTest]
    public IEnumerator LegacyPipsAreRetiredButPassiveAndHeatReferencesRemain()
    {
#if UNITY_EDITOR
        foreach (string path in new[] { "Assets/Prefabs/Objects/Moves/Meowstic/Male/Rework/State.prefab",
            "Assets/Prefabs/Objects/Moves/Flareon/FlareonStateObject.prefab" })
        {
            GameObject instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), root.transform);
            Component view = instance.GetComponent(GameType(path.Contains("Meowstic") ? "MeowsticVisual" : "FlareonMoveObject"));
            ((Behaviour)view).enabled = false;
            foreach (GameObject pip in (GameObject[])view.GetType().GetField("attackPips", Private).GetValue(view))
                Assert.That(pip.activeSelf, Is.False, path);
            if (path.Contains("Meowstic"))
            {
                var ready = (GameObject)view.GetType().GetField("boostedReady", Private).GetValue(view);
                Assert.That(ready.activeSelf, Is.False);
                Assert.That(((GameObject[])view.GetType().GetField("enemyPips", Private).GetValue(view)).Length, Is.EqualTo(5));
            }
            else Assert.That(((GameObject[])view.GetType().GetField("heatPips", Private).GetValue(view)).Length, Is.EqualTo(3));
        }
        GameObject pledge = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Objects/Moves/Emboar/EmboarFirePledgeObject.prefab"), root.transform);
        Component pledgeView = pledge.GetComponent(GameType("EmboarMoveObject")); ((Behaviour)pledgeView).enabled = false;
        foreach (GameObject pip in (GameObject[])pledgeView.GetType().GetField("pledgePips", Private).GetValue(pledgeView))
            Assert.That(pip.activeSelf, Is.False);
        yield return null;
#else
        yield break;
#endif
    }
}

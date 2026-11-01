using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class ZoroarkTests
{
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string name) => Enum.Parse(T(type), name);
    private static object Rule(string method, params object[] args) => T("ZoroarkMoveRules").GetMethod(method).Invoke(null, args);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, args.Select(a => a.GetType()).ToArray()).Invoke(target, args);

    [Test]
    public void ZoroarkExecutionDeclaresEveryMoveEntryPoint()
    {
        var execution = T("ZoroarkExecution");
        foreach (string name in new[] { "ExecuteSlash", "ExecuteFurySwipes", "ExecuteShadowClaw", "ExecuteCut",
                     "ExecuteNightSlash", "ExecuteFeintAttack", "ExecuteUnite" })
        {
            var method = execution.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            Assert.That(method.GetParameters(), Is.Empty, name);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(System.Collections.IEnumerator)), name);
        }
        Assert.That(execution.BaseType, Is.EqualTo(T("CombatExecution")));
    }

    [TestCase("ZoroarkSlash", 5.5f, false)]
    [TestCase("ZoroarkFurySwipes", 7.5f, true)]
    [TestCase("ZoroarkShadowClaw", 4.5f, false)]
    [TestCase("ZoroarkCut", 4.5f, false)]
    [TestCase("ZoroarkNightSlash", 7f, true)]
    [TestCase("ZoroarkFeintAttack", 6f, true)]
    [TestCase("ZoroarkNightfallDaze", 0f, false)]
    public void MovesHaveFactoriesAssetsArtAndNetworkRegistration(string name, float cooldown, bool chain)
    {
        object id = E("AvailableMoves", name);
        var move = T("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { id });
        Assert.That(move.GetType().GetField("Cooldown").GetValue(move), Is.EqualTo(cooldown));
        Assert.That(Get(move, "CooldownOnEffectEnd"), Is.EqualTo(chain));
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Zoroark/" + name + ".asset");
        Assert.That(asset, Is.Not.Null);
        var data = new SerializedObject(asset);
        Assert.That(data.FindProperty("icon").objectReferenceValue, Is.Not.Null);
        Assert.That(data.FindProperty("preview").FindPropertyRelative("m_AssetGUID").stringValue, Is.Not.Empty);
        string path = "Assets/Prefabs/Objects/Moves/Zoroark/" + name + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab.GetComponent(T("PrefabMoveEffect")), Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThan(0));
        Assert.That(new SerializedObject(prefab.GetComponent(T("Unity.Netcode.NetworkObject"))).FindProperty("GlobalObjectIdHash").longValue, Is.Not.Zero);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")).FindProperty("List");
        Assert.That(Enumerable.Range(0, registry.arraySize).Any(i => registry.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab), Is.True);
    }

    [Test]
    public void LearnsetUsesCorrectBranchesAndZeroBasedLevels()
    {
        var pokemon = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Zoroark.asset"));
        var rows = pokemon.FindProperty("learnableMoves");
        Assert.That(rows.arraySize, Is.EqualTo(7));
        Assert.That(Enumerable.Range(0, rows.arraySize).Select(i => rows.GetArrayElementAtIndex(i).FindPropertyRelative("level").intValue).ToArray(), Is.EqualTo(new[] { 0, 2, 4, 6, 8, 10, 12 }));
        Assert.That(pokemon.FindProperty("passive").enumValueIndex, Is.EqualTo(Convert.ToInt32(E("AvailablePassives", "ZoroarkPassive"))));
        Assert.That(pokemon.FindProperty("evolutions").GetArrayElementAtIndex(1).FindPropertyRelative("level").intValue, Is.EqualTo(4));
        var night = Activator.CreateInstance(T("ZoroarkNightSlash"));
        Assert.That(Call(night, "EffectiveCooldown", true), Is.EqualTo(6f));
        Assert.That(Call(Activator.CreateInstance(T("ZoroarkFeintAttack")), "EffectiveCooldown", true), Is.EqualTo(5f));
    }

    [TestCase(false, 226)]
    [TestCase(true, 265)]
    public void CutHealingUsesDocumentedFormula(bool plus, int expected)
    {
        Assert.That(Rule("CutHeal", 130, 0, plus), Is.EqualTo(expected));
    }

    [Test]
    public void FinishersHaveMissingHpDamageAndWildCaps()
    {
        Assert.That(Rule("FinisherDamage", 100, 4, 1000, false, false), Is.EqualTo(958));
        Assert.That(Rule("FinisherDamage", 100, 4, 1000, true, false), Is.EqualTo(1250));
        Assert.That(Rule("FinisherDamage", 1000, 14, 10000, false, true), Is.EqualTo(1000));
        Assert.That(Rule("FinisherDamage", 1000, 14, 10000, true, true), Is.EqualTo(1200));
        Assert.That(Rule("NightHeal", 100, 4), Is.EqualTo(344));
    }

    [Test]
    public void OnlyBasicAttacksHaveCriticalFlag()
    {
        object basic = Rule("Basic", 1ul, true);
        Assert.That(Convert.ToInt32(basic.GetType().GetField("proprieties").GetValue(basic)) & 1, Is.EqualTo(1));
        foreach (var name in new[] { "ZoroarkSlash", "ZoroarkFurySwipes", "ZoroarkShadowClaw", "ZoroarkCut", "ZoroarkNightSlash", "ZoroarkFeintAttack" })
        {
            var damage = Rule("Dash", 1ul, E("AvailableMoves", name));
            Assert.That(Convert.ToInt32(damage.GetType().GetField("proprieties").GetValue(damage)) & 1, Is.Zero);
        }
    }

    [Test]
    public void SegmentedGaugeSeparatesStepsClampsAndReusesItsLines()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HPBarModules/SegmentedGauge.prefab"));
        try
        {
            var gauge = root.GetComponent(T("SegmentedGaugeModule"));
            Call(gauge, "SetSteps", 3, 6);
            Assert.That(Get(gauge, "NormalizedValue"), Is.EqualTo(0.5f));
            var lines = root.GetComponentsInChildren<Image>(true).Where(i => i.name == "StepSeparator").ToArray();
            Assert.That(lines.Length, Is.EqualTo(5));
            Assert.That(lines.All(i => i.color == Color.black && !i.raycastTarget), Is.True);
            for (int i = 0; i < lines.Length; i++) Assert.That(lines[i].rectTransform.anchorMin.x, Is.EqualTo((i + 1f) / 6).Within(0.001f));
            Call(gauge, "SetSteps", 99, 4);
            Assert.That(Get(gauge, "Steps"), Is.EqualTo(4));
            Assert.That(lines.Count(i => i.gameObject.activeSelf), Is.EqualTo(3));
            Call(gauge, "SetSteps", -5, 6);
            Assert.That(Get(gauge, "NormalizedValue"), Is.Zero);
            CollectionAssert.AreEqual(lines, root.GetComponentsInChildren<Image>(true).Where(i => i.name == "StepSeparator").ToArray());
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void AuxiliaryButtonIsOptionalAndAuthoredForBothInputLayouts()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DefaultObjects.prefab");
        foreach (var holder in root.GetComponentsInChildren(T("MovesHolderUI"), true))
            Assert.That(holder.GetComponentInChildren(T("AuxiliaryActionUI"), true), Is.Not.Null);
        Assert.That(Resources.Load<Sprite>("ZoroarkIllusionIcon"), Is.Not.Null);
    }

    [Test]
    public void AuxiliaryButtonUsesMoveUiFrameAndCooldownArtwork()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MoveUI.prefab");
        var auxiliary = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/AuxiliaryAction.prefab");
        var frame = auxiliary.transform.Find("Content/MainBG").GetComponent<Image>();
        var sourceFrame = source.transform.Find("MainBG").GetComponent<Image>();
        Assert.That(frame.sprite, Is.SameAs(sourceFrame.sprite));
        Assert.That(frame.color, Is.EqualTo(sourceFrame.color));
        var sourceData = new SerializedObject(source.GetComponent(T("MoveUI")));
        var data = new SerializedObject(auxiliary.GetComponent(T("AuxiliaryActionUI")));
        var fill = (Image)data.FindProperty("cooldownFill").objectReferenceValue;
        var sourceFill = ((GameObject)sourceData.FindProperty("cdBg").objectReferenceValue).GetComponent<Image>();
        Assert.That(fill.sprite, Is.SameAs(sourceFill.sprite));
        Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
        Assert.That(fill.fillMethod, Is.EqualTo(Image.FillMethod.Radial360));
        Assert.That(data.FindProperty("lockImage").objectReferenceValue, Is.Not.Null);
        var icon = (Image)data.FindProperty("icon").objectReferenceValue;
        Assert.That(icon.rectTransform.rect.width, Is.LessThan(frame.rectTransform.rect.width));
    }

    [Test]
    public void AuxiliaryActionHonorsAvailabilityCooldownAndDisposal()
    {
        int calls = 0; bool available = true; float remaining = 0;
        object action = Activator.CreateInstance(T("AuxiliaryAction"), new object[] { "Example", null, 15f,
            new Action(() => calls++), new Func<bool>(() => available), new Func<float>(() => remaining) });
        Call(action, "TryActivate"); Assert.That(calls, Is.EqualTo(1));
        remaining = 5; Call(action, "TryActivate"); Assert.That(calls, Is.EqualTo(1));
        remaining = 0; available = false; Call(action, "TryActivate"); Assert.That(calls, Is.EqualTo(1));
        available = true; Call(action, "Dispose"); Call(action, "TryActivate");
        Assert.That(calls, Is.EqualTo(1)); Assert.That(Get(action, "Available"), Is.False);
    }

    [Test]
    public void HpPresentationOverrideSnapshotsWithoutRebindingGameplay()
    {
        var root = new GameObject("Presentation test", typeof(RectTransform));
        try
        {
            var bar = root.AddComponent(T("HPBar"));
            var imageObject = new GameObject("HP", typeof(RectTransform), typeof(Image)); imageObject.transform.SetParent(root.transform);
            var image = imageObject.GetComponent<Image>();
            bar.GetType().GetField("hpBar", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bar, image);
            Call(bar, "UpdatePlayerName", "Real Zoroark");
            Call(bar, "SetPresentationOverride", "Copied player", 2400, 4000, 8, 200, 25, true);
            Assert.That(image.fillAmount, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(Get(bar, "DisplayName"), Is.EqualTo("Real Zoroark"));
            Assert.That(Get(bar, "Pokemon"), Is.Null, "A disguise must not rebind gameplay references.");
            Call(bar, "ClearPresentationOverride");
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void AerialRepositionIgnoresNormalCooldownButNeverIgnoresStunsOrBusyLocks()
    {
        var root = new GameObject("Aerial input test");
        try
        {
            var moves = root.AddComponent(T("MovesController"));
            var statuses = (Array)moves.GetType().GetField("moveStatuses", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(moves);
            var status = Activator.CreateInstance(T("BattleActionStatus"), 5f);
            statuses.SetValue(status, 0);
            Call(status, "AddStatus", E("ActionStatusType", "Cooldown"));
            var player = root.AddComponent(T("PlayerManager"));
            player.GetType().GetField("movesController", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, moves);
            var runtime = Activator.CreateInstance(T("ZoroarkCharacterRuntime"), new object[] { player });
            runtime.GetType().GetField("zoroarkAirborne", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runtime, true);
            var method = runtime.GetType().GetMethod("CanRecast");
            Assert.That(method.Invoke(runtime, new object[] { 0 }), Is.True);
            Assert.That(method.Invoke(runtime, new object[] { 1 }), Is.False);
            Call(status, "AddStatus", E("ActionStatusType", "Stunned"));
            Assert.That(method.Invoke(runtime, new object[] { 0 }), Is.False);
            Call(status, "RemoveStatus", E("ActionStatusType", "Stunned"));
            Call(status, "AddStatus", E("ActionStatusType", "Busy"));
            Assert.That(method.Invoke(runtime, new object[] { 0 }), Is.False);
            var move = Activator.CreateInstance(T("ZoroarkCut"));
            T("ZoroarkMove").GetField("aerialCast", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(move, true);
            Assert.That(Get(move, "CooldownOnFinish"), Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }
}

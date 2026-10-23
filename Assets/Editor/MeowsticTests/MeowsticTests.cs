using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// The production assembly is Assembly-CSharp; exercise it through reflection,
// rather than copying gameplay formulas into a second implementation.
public class MeowsticTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Value(string type, string name) => Enum.Parse(GameType(type), name);
    private static object Rule(string method, params object[] args) => GameType("MeowsticMoveRules").GetMethod(method).Invoke(null, args);
    private static object Call(object instance, string method, params object[] args) => instance.GetType().GetMethod(method).Invoke(instance, args);
    private static object Change(bool buff, short amount = 20, bool timed = true, bool removable = true) => Activator.CreateInstance(GameType("StatChange"),
        amount, Value("Stat", "Attack"), 3f, timed, buff, true, (ushort)0, removable);

    [TestCase("Scratch", "MeowsticMScratch", 0)]
    [TestCase("Leer", "MeowsticMLeer", 1)]
    [TestCase("Reflect", "MeowsticMReflect", 0)]
    [TestCase("MagicCoat", "MeowsticMMagicCoat", 0)]
    [TestCase("Psychic", "MeowsticMPsychic", 1)]
    [TestCase("WonderRoom", "MeowsticMWonderRoom", 1)]
    [TestCase("Unite", "MeowsticMUnite", 2)]
    public void FactoriesAssetsAndRulesAgree(string kind, string name, int slot)
    {
        object available = Value("AvailableMoves", name), cast = Value("MeowsticCast", kind);
        object move = GameType("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { available });
        Assert.That(move.GetType().Name, Is.EqualTo(name));
        Assert.That(Rule("Move", cast), Is.EqualTo(available)); Assert.That(Rule("Slot", cast), Is.EqualTo(slot));
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Meowstic/Male/" + name + ".asset");
        var data = new SerializedObject(asset);
        Assert.That(data.FindProperty("moveType").intValue, Is.EqualTo(slot));
        Assert.That(data.FindProperty("move").intValue, Is.EqualTo(Convert.ToInt32(available)));
        Assert.That(data.FindProperty("icon").objectReferenceValue, Is.Not.Null);
        Assert.That(data.FindProperty("description").stringValue, Is.Not.Empty);
        Assert.That(data.FindProperty("cooldown").floatValue, Is.EqualTo(Rule("Cooldown", cast)));
        Call(move, "Cancel"); Call(move, "ResetMove"); Call(move, "ResetMove");
        Assert.That(move.GetType().GetField("IsActive").GetValue(move), Is.False);
    }
    [Test]
    public void LearnsetEvolutionEnergyAndRangedClassificationArePreserved()
    {
        var mon = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/MeowsticMale.asset"));
        int[] levels = { 0, 1, 3, 5, 7, 9, 11 };
        var moves = mon.FindProperty("learnableMoves");
        Assert.That(moves.arraySize, Is.EqualTo(levels.Length));
        for (int i = 0; i < levels.Length; i++) Assert.That(moves.GetArrayElementAtIndex(i).FindPropertyRelative("level").intValue, Is.EqualTo(levels[i]));
        Assert.That(mon.FindProperty("description").stringValue, Does.Contain("Espurr"));
        Assert.That(mon.FindProperty("evolutions").GetArrayElementAtIndex(1).FindPropertyRelative("level").intValue, Is.EqualTo(3));
        var info = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/PokemonSelectInfo/MeowsticMale.asset"));
        Assert.That(info.FindProperty("Range").intValue, Is.EqualTo(1));
        var unite = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Meowstic/Male/MeowsticMUnite.asset"));
        Assert.That(unite.FindProperty("uniteEnergyCost").intValue, Is.EqualTo(108000));
    }
    [TestCase(-1, 0)] [TestCase(0, 0)] [TestCase(1, 5)] [TestCase(5, 25)] [TestCase(12, 25)]
    public void PassiveGivesExplicitCdrPoints(int enemies, int points) { Assert.That(Rule("EnemyCdr", enemies), Is.EqualTo(points)); }
    [Test]
    public void EspurrBlocksOnceWeakensDebuffsOnlyAndRecovers()
    {
        object state = Activator.CreateInstance(GameType("MeowsticEspurrState"));
        Assert.That(Call(state, "Filter", Change(false), 10d, false), Is.Null);
        object weakened = Call(state, "Filter", Change(false), 10.1d, false);
        Assert.That(weakened.GetType().GetField("Amount").GetValue(weakened), Is.EqualTo(8));
        object buff = Change(true);
        Assert.That(Call(state, "Filter", buff, 10.2d, false), Is.EqualTo(buff));
        object normal = Call(state, "Filter", Change(false), 13.01d, false);
        Assert.That(normal.GetType().GetField("Amount").GetValue(normal), Is.EqualTo(20));
        Assert.That(Call(state, "Filter", Change(false), 33d, false), Is.Null);
    }
    [Test]
    public void EspurrNeverConsumesItsProtectionOnPersistentOrUnremovableEffectsOrAfterEvolution()
    {
        foreach (object change in new[] { Change(false, timed: false), Change(false, removable: false) })
        {
            object state = Activator.CreateInstance(GameType("MeowsticEspurrState"));
            Assert.That(Call(state, "Filter", change, 1d, false), Is.EqualTo(change));
            Assert.That(Call(state, "Filter", Change(false), 1.1d, false), Is.Null);
        }
        object evolved = Activator.CreateInstance(GameType("MeowsticEspurrState")), debuff = Change(false);
        Assert.That(Call(evolved, "Filter", debuff, 1d, true), Is.EqualTo(debuff));
    }
    [TestCase("Physical", 0, 1000, false, 350)]
    [TestCase("Special", 100, 1000, false, 100)]
    [TestCase("True", 100, 1000, false, 100)]
    [TestCase("Physical", 2400, 1000, false, 2500)]
    [TestCase("Physical", 2400, 1000, true, 2750)]
    [TestCase("Physical", 0, -100, false, 0)]
    public void ReflectStoresOnlyResolvedPhysicalDamageAndRespectsCap(string type, int stored, int received, bool upgraded, int expected)
    { Assert.That(Rule("StoreReflect", stored, received, Value("DamageType", type), upgraded), Is.EqualTo(expected)); }
    [Test]
    public void PsychicStacksCapRefreshAndExpireIndependently()
    {
        object stacks = Activator.CreateInstance(GameType("MeowsticPsychicStacks"));
        Assert.That(Call(stacks, "Pulse", 1d), Is.EqualTo(20));
        Assert.That(Call(stacks, "Pulse", 1.8d), Is.EqualTo(40));
        Assert.That(Call(stacks, "Pulse", 2.6d), Is.EqualTo(60));
        Assert.That(Call(stacks, "Pulse", 3.4d), Is.EqualTo(60));
        Assert.That(Call(stacks, "Amount", 6.39d), Is.EqualTo(60));
        Assert.That(Call(stacks, "Amount", 6.41d), Is.Zero);
        Assert.That(Call(stacks, "Pulse", 7d), Is.EqualTo(20));
    }
    [TestCase("Immobilized", true)] [TestCase("Incapacitated", true)] [TestCase("Asleep", true)]
    [TestCase("Frozen", true)] [TestCase("VisionObscuring", true)] [TestCase("Bound", false)]
    [TestCase("Unstoppable", false)] [TestCase("Burned", false)] [TestCase("Poisoned", false)]
    public void MagicCoatUsesAnExplicitHindranceWhitelist(string name, bool expected)
    {
        object effect = Activator.CreateInstance(GameType("StatusEffect"), Value("StatusType", name), 2f, true, (ushort)0);
        Assert.That(Rule("Reflectable", effect), Is.EqualTo(expected));
    }
    [Test]
    public void ReflectionContextCannotRemainActiveAfterAnException()
    {
        var type = GameType("MeowsticReflection");
        bool observed = false;
        Action action = () => { observed = (bool)type.GetProperty("Active").GetValue(null); throw new InvalidOperationException("test"); };
        Assert.Throws<TargetInvocationException>(() => type.GetMethod("Run").Invoke(null, new object[] { action }));
        Assert.That(observed, Is.True); Assert.That(type.GetProperty("Active").GetValue(null), Is.False);
    }
    [Test]
    public void SourceOwnedModifiersAreStrongestWinsAndDisposeIndependently()
    {
        Type type = GameType("MeowsticModifier"); object defense = Value("Stat", "Defense");
        object first = Activator.CreateInstance(type, null, defense, (short)15, true, true, 5, 0f);
        object second = Activator.CreateInstance(type, null, defense, (short)20, true, true, 5, 0f);
        try
        {
            Assert.That(type.GetProperty("EffectiveAmount").GetValue(first), Is.EqualTo(20));
            ((IDisposable)second).Dispose();
            Assert.That(type.GetProperty("EffectiveAmount").GetValue(first), Is.EqualTo(15));
            ((IDisposable)second).Dispose();
            Assert.That(type.GetProperty("EffectiveAmount").GetValue(first), Is.EqualTo(15));
        }
        finally { ((IDisposable)first).Dispose(); ((IDisposable)second).Dispose(); }
    }
    [Test]
    public void EveryUpgradeChangesItsAdvertisedBenefit()
    {
        foreach (string method in new[] { "ReflectCap", "CoatDuration", "RoomDefense", "PsychicHealingRatio" })
            Assert.That(Convert.ToDouble(Rule(method, true)), Is.GreaterThan(Convert.ToDouble(Rule(method, false))));
        Assert.That(Rule("Upgraded", Value("MeowsticCast", "Reflect"), 8), Is.False);
        Assert.That(Rule("Upgraded", Value("MeowsticCast", "Reflect"), 9), Is.True);
        Assert.That(Rule("Upgraded", Value("MeowsticCast", "Psychic"), 10), Is.False);
        Assert.That(Rule("Upgraded", Value("MeowsticCast", "Psychic"), 11), Is.True);
    }
    [Test]
    public void ConeRejectsRearSideAndOutOfRangeTargets()
    {
        Assert.That(Rule("InCone", Vector3.zero, Vector3.forward, new Vector3(0, 0, 2), 3f, 45f), Is.True);
        foreach (Vector3 point in new[] { Vector3.back, Vector3.right * 2, Vector3.forward * 4 })
            Assert.That(Rule("InCone", Vector3.zero, Vector3.forward, point, 3f, 45f), Is.False);
    }
    [Test]
    public void DamageCalculationSwapsScalingNotDamageTypeAndReflectIsPhysicalOnly()
    {
        var a = new GameObject("Meowstic formula attacker"); var b = new GameObject("Meowstic formula recipient");
        ScriptableObject stats = ScriptableObject.CreateInstance(GameType("PokemonBase"));
        try
        {
            a.AddComponent(GameType("Unity.Netcode.NetworkObject")); b.AddComponent(GameType("Unity.Netcode.NetworkObject"));
            var attacker = a.AddComponent(GameType("Pokemon")); var target = b.AddComponent(GameType("Pokemon"));
            // EditMode does not run MonoBehaviour.Awake for these isolated roots.
            foreach (Component component in new[] { attacker, target })
                if (GameType("Pokemon").GetField("statChanges", Private).GetValue(component) == null)
                    GameType("Pokemon").GetMethod("Awake", Private).Invoke(component, null);
            foreach (string field in new[] { "attack", "spAttack", "defense", "spDefense", "critRate", "cdr", "speed" })
                GameType("PokemonBase").GetField(field, Private).SetValue(stats, Enumerable.Repeat(field == "attack" ? 600 : field == "spAttack" ? 120 : field == "defense" ? 600 : 0, 15).ToArray());
            GameType("Pokemon").GetField("baseStats", Private).SetValue(attacker, stats); GameType("Pokemon").GetField("baseStats", Private).SetValue(target, stats);
            object physical = Activator.CreateInstance(GameType("DamageInfo"), 0ul, 1f, (short)0, (short)0, Value("DamageType", "Physical"), Value("DamageProprieties", "None"));
            object special = Activator.CreateInstance(GameType("DamageInfo"), 0ul, 1f, (short)0, (short)0, Value("DamageType", "Special"), Value("DamageProprieties", "None"));
            var calculate = GameType("Pokemon").GetMethod("CalculateDamage", new[] { GameType("DamageInfo"), GameType("Pokemon") });
            Assert.That(calculate.Invoke(target, new[] { physical, attacker }), Is.EqualTo(300));
            Assert.That(calculate.Invoke(target, new[] { special, attacker }), Is.EqualTo(120));
            object flip = GameType("Pokemon").GetField("flipAtkStat", Private).GetValue(attacker);
            flip.GetType().GetField("m_InternalValue", Private).SetValue(flip, true);
            Assert.That(calculate.Invoke(target, new[] { physical, attacker }), Is.EqualTo(60), "Still mitigated by physical Defense.");
            Assert.That(calculate.Invoke(target, new[] { special, attacker }), Is.EqualTo(600));
            object list = GameType("Pokemon").GetField("statChanges", Private).GetValue(target);
            object reduction = Activator.CreateInstance(GameType("StatChange"), (short)35, Value("Stat", "PhysicalDamageReduction"), 4f, true, true, false, (ushort)0, true);
            Call(list, "Add", reduction);
            Assert.That(calculate.Invoke(target, new[] { physical, attacker }), Is.EqualTo(39));
            Assert.That(calculate.Invoke(target, new[] { special, attacker }), Is.EqualTo(600));
        }
        finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(stats); }
    }
    [Test]
    public void ProjectilesStartAtTheirOriginAndNeverConsumeThePreviousFrameDelta()
    {
        Vector3 origin = new Vector3(12, 0.7f, -8), target = origin + Vector3.forward * 4;
        object flight = Activator.CreateInstance(GameType("MeowsticProjectileMotion"), origin, 100f);
        Assert.That(Call(flight, "Step", target, 100f), Is.EqualTo(origin));
        Assert.That(Call(flight, "Step", target, 100f), Is.EqualTo(origin), "Initialization and Update in the same frame must not advance twice.");
        Assert.That(Vector3.Distance((Vector3)Call(flight, "Step", target, 100.05f), origin), Is.EqualTo(1).Within(0.001f));
        Assert.That(Vector3.Distance((Vector3)Call(flight, "Step", target, 100.1f), origin), Is.EqualTo(2).Within(0.001f));
    }
    [TestCase("Assets/Prefabs/PokemoModels/Meowstic/Espurr.prefab", false)]
    [TestCase("Assets/Prefabs/PokemoModels/Meowstic/Espurr.prefab", true)]
    [TestCase("Assets/Prefabs/PokemoModels/Meowstic/Meowstic(male).prefab", false)]
    [TestCase("Assets/Prefabs/PokemoModels/Meowstic/Meowstic(male).prefab", true)]
    public void BasicProjectileOriginFollowsTheActualModelSocketIncludingLevitation(string path, bool boosted)
    {
        var root = new GameObject("Meowstic projectile origin test");
        try
        {
            root.AddComponent(GameType("Unity.Netcode.NetworkObject"));
            Component pokemon = root.AddComponent(GameType("Pokemon"));
            GameObject model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), root.transform);
            GameType("Pokemon").GetField("activeModel", Private).SetValue(pokemon, model);
            root.transform.SetPositionAndRotation(new Vector3(10, 2, -4), Quaternion.Euler(0, 90, 0));
            model.transform.localPosition = Vector3.up * 0.3f;
            Transform socket = model.GetComponentsInChildren<Transform>(true).First(bone => bone.name == (boosted ? "EffHeadCenter01" : "EffHand01"));
            Assert.That(Rule("BasicAttackOrigin", pokemon, boosted), Is.EqualTo(socket.position));
            model.transform.position += new Vector3(1, 0.5f, 0);
            Assert.That(Rule("BasicAttackOrigin", pokemon, boosted), Is.EqualTo(socket.position), "The collision-root position is not the animated model position.");
        }
        finally { Object.DestroyImmediate(root); }
    }
    [Test]
    public void EveryEffectIsAuthoredWiredAndAddressableWithoutRuntimeGeometry()
    {
        var group = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovesAssets.asset"));
        var entries = group.FindProperty("m_SerializeEntries");
        foreach (string name in Enum.GetNames(GameType("MeowsticEffect")))
        {
            string path = "Assets/Prefabs/Objects/Moves/Meowstic/Male/Rework/" + name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(prefab, Is.Not.Null, path);
            Component visual = prefab.GetComponent(GameType("MeowsticVisual")); Assert.That(visual, Is.Not.Null);
            Assert.That(new SerializedObject(visual).FindProperty("graphics").objectReferenceValue, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true)) foreach (Material material in renderer.sharedMaterials)
            { Assert.That(material, Is.Not.Null); Assert.That(material.shader, Is.Not.Null); Assert.That(material.shader.name, Does.Not.Contain("Error")); }
            string guid = AssetDatabase.AssetPathToGUID(path); int matches = 0;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("m_GUID").stringValue == guid) { matches++; Assert.That(entry.FindPropertyRelative("m_Address").stringValue, Is.EqualTo(path)); }
            }
            Assert.That(matches, Is.EqualTo(1));
        }
        string source = File.ReadAllText("Assets/Scripts/Moves/Meowstic/Male/MeowsticVisual.cs");
        foreach (string forbidden in new[] { "CreatePrimitive(", "new Mesh", "AddComponent<LineRenderer>" }) Assert.That(source, Does.Not.Contain(forbidden));
        Assert.That(File.ReadAllText("Assets/Scripts/Player/MovesController.Meowstic.cs"), Does.Not.Contain("onObjectSpawned"));
    }
}

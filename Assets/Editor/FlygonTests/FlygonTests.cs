using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class FlygonTests
{
    private const string Folder = "Assets/Prefabs/Objects/Moves/Flygon/";
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Cast(string name) => Enum.Parse(Type("FlygonCast"), name);
    private static object Rule(string name, params object[] args) => Type("FlygonMoveRules").GetMethod(name).Invoke(null, args);

    [TestCase("Bite", "FlygonBite", 6f)]
    [TestCase("Dig", "FlygonDig", 8f)]
    [TestCase("Supersonic", "FlygonSupersonic", 7f)]
    [TestCase("Boomburst", "FlygonBoomburst", 7.5f)]
    [TestCase("Earthquake", "FlygonEarthquake", 9.5f)]
    [TestCase("Sandstorm", "FlygonSandstorm", 11f)]
    [TestCase("Unite", "FlygonUnite", 0f)]
    public void FactoryAndRuntimeUseTheSameCastAndCooldown(string kind, string className, float cooldown)
    {
        object move = Type("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { Rule("Move", Cast(kind)) });
        Assert.That(move.GetType().Name, Is.EqualTo(className));
        Assert.That(move.GetType().GetProperty("Kind").GetValue(move), Is.EqualTo(Cast(kind)));
        Assert.That(Type("MoveBase").GetField("Cooldown").GetValue(move), Is.EqualTo(cooldown));
    }
    [TestCase("Bite", "FlygonBite")]
    [TestCase("Dig", "FlygonDig")]
    [TestCase("Supersonic", "FlygonSupersonic")]
    [TestCase("Boomburst", "FlygonBoomburst")]
    [TestCase("Earthquake", "FlygonEarthquake")]
    [TestCase("Sandstorm", "FlygonSandstorm")]
    [TestCase("Unite", "FlygonUnite")]
    public void ServerAndOwnerSlotsMatchTheSerializedMoveAssets(string kind, string assetName)
    {
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Flygon/" + assetName + ".asset");
        var serialized = new SerializedObject(asset);
        int slot = serialized.FindProperty("moveType").enumValueIndex;
        Assert.That(Rule("Slot", Cast(kind)), Is.EqualTo(slot), "A mismatched slot rejects the cast before spawning and routes acceptance to the wrong owner move.");
    }
    [TestCase("Supersonic")] [TestCase("Boomburst")] [TestCase("Earthquake")] [TestCase("Sandstorm")]
    public void UpgradeThresholdComesFromTheCharacterLearnsetNotTheSlotNumber(string kind)
    {
        var pokemon = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Flygon.asset"));
        var learnset = pokemon.FindProperty("learnableMoves");
        int move = Convert.ToInt32(Rule("Move", Cast(kind)));
        int upgradeLevel = -1;
        for (int i = 0; i < learnset.arraySize; i++)
        {
            var entry = learnset.GetArrayElementAtIndex(i);
            if (!entry.FindPropertyRelative("isUpgraded").boolValue) continue;
            var moves = entry.FindPropertyRelative("moves");
            for (int j = 0; j < moves.arraySize; j++)
            {
                var asset = new SerializedObject(moves.GetArrayElementAtIndex(j).objectReferenceValue);
                if (asset.FindProperty("move").enumValueIndex == move) upgradeLevel = entry.FindPropertyRelative("level").intValue;
            }
        }
        Assert.That(upgradeLevel, Is.GreaterThanOrEqualTo(0));
        Assert.That(Rule("Upgraded", Cast(kind), upgradeLevel - 1), Is.False);
        Assert.That(Rule("Upgraded", Cast(kind), upgradeLevel), Is.True);
    }
    [TestCase("Supersonic", 9, false)]
    [TestCase("Supersonic", 10, true)]
    [TestCase("Boomburst", 10, true)]
    [TestCase("Earthquake", 11, false)]
    [TestCase("Earthquake", 12, true)]
    [TestCase("Sandstorm", 12, true)]
    [TestCase("Dig", 14, false)]
    public void UpgradesRespectTheActualLearnset(string kind, int level, bool expected)
    {
        Assert.That(Rule("Upgraded", Cast(kind), level), Is.EqualTo(expected));
    }
    [TestCase(0f, 0)] [TestCase(2.999f, 0)] [TestCase(3f, 1)] [TestCase(4f, 2)] [TestCase(5f, 3)]
    public void TornadoDecayHasExactBoundaries(float age, int stage)
    {
        Assert.That(Rule("TornadoStage", age), Is.EqualTo(stage));
    }
    [Test]
    public void TornadoLosesPowerWithoutSlowingBelowNormalWalkingSpeed()
    {
        for (int stage = 1; stage < 4; stage++)
        {
            Assert.That((float)Rule("TornadoPower", stage), Is.LessThan((float)Rule("TornadoPower", stage - 1)));
            Assert.That((float)Rule("TornadoLift", stage), Is.LessThan((float)Rule("TornadoLift", stage - 1)));
            Assert.That((short)Rule("TornadoSpeed", stage), Is.InRange((short)0, (short)15));
        }
    }
    [TestCase(0f, 15)] [TestCase(2f, 15)] [TestCase(2.01f, 10)] [TestCase(3.99f, 10)] [TestCase(4f, 0)] [TestCase(5f, 0)]
    public void BoomburstBandsMatchTheDescription(float distance, int reduction)
    {
        Assert.That(Rule("BurstReduction", distance), Is.EqualTo((short)reduction));
    }
    [Test]
    public void WaveDamageAndUpgradeBudgetsRemainBounded()
    {
        float previous = 0;
        for (int wave = 0; wave < 3; wave++)
        {
            object damage = Rule("Damage", (ulong)1, Cast("Earthquake"), wave);
            float ratio = (float)damage.GetType().GetField("ratio").GetValue(damage);
            Assert.That(ratio, Is.GreaterThan(previous)); previous = ratio;
        }
        Assert.That(Rule("SonicSlow", false), Is.EqualTo((short)20));
        Assert.That(Rule("SonicSlow", true), Is.EqualTo((short)25));
        Assert.That(Rule("BurstReductionDuration", false), Is.EqualTo(3f));
        Assert.That(Rule("BurstReductionDuration", true), Is.EqualTo(4f));
    }
    [TestCase("Bite", 1.2f)] [TestCase("Dig", 1.5f)] [TestCase("Supersonic", 1.2f)]
    [TestCase("Boomburst", 5f)] [TestCase("Earthquake", 2.5f)] [TestCase("Sandstorm", 4.5f)] [TestCase("Unite", 4f)]
    public void EveryCastHasRegisteredAuthoredArtAndAnExplicitHitbox(string kind, float radius)
    {
        string path = (string)Rule("Path", Cast(kind));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        var effect = prefab.GetComponent(Type("FlygonMoveObject"));
        Assert.That(effect, Is.Not.Null);
        var serialized = new SerializedObject(effect);
        Assert.That(serialized.FindProperty("graphics").objectReferenceValue, Is.Not.Null);
        var area = serialized.FindProperty("area").objectReferenceValue as SphereCollider;
        Assert.That(area, Is.Not.Null); Assert.That(area.radius, Is.EqualTo(radius));
        Assert.That(prefab.GetComponentsInChildren<Collider>(true).All(c => c.isTrigger && !c.enabled), Is.True);
        Assert.That(prefab.GetComponentInChildren(Type("FlygonMovePresentation"), true), Is.Not.Null);
        var network = prefab.GetComponent(Type("Unity.Netcode.NetworkObject"));
        Assert.That(new SerializedObject(network).FindProperty("GlobalObjectIdHash").longValue, Is.Not.Zero);
        Assert.That(prefab.GetComponent(Type("Unity.Netcode.Components.NetworkTransform")).GetType().Name, Is.EqualTo("NetworkTransform"));
        string guid = AssetDatabase.AssetPathToGUID(path);
        Assert.That(Regex.Matches(File.ReadAllText("Assets/DefaultNetworkPrefabs.asset"), guid).Count, Is.EqualTo(1));
        Assert.That(Regex.Matches(File.ReadAllText("Assets/AddressableAssetsData/AssetGroups/MovesAssets.asset"), guid).Count, Is.EqualTo(1));
        foreach (MeshFilter mesh in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            Assert.That(mesh.sharedMesh, Is.Not.Null); Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh), Is.Not.Empty);
        }
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.That(material, Is.Not.Null); Assert.That(material.shader, Is.Not.Null); Assert.That(material.shader.name, Does.Not.Contain("Error"));
            }
    }
    [Test]
    public void SupersonicRetainsItsExistingTravelingParticles()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "FlygonSupersonic.prefab");
        var effect = new SerializedObject(prefab.GetComponent(Type("SupersonicHitbox")));
        var particles = effect.FindProperty("particles").objectReferenceValue as ParticleSystem;
        Assert.That(particles, Is.Not.Null);
        Assert.That(particles.name, Is.EqualTo("Particle System"));
        Assert.That(particles.main.startSpeed.constant, Is.EqualTo(7));
        Assert.That(particles.main.startLifetime.constant, Is.EqualTo(1));
        Assert.That(particles.main.playOnAwake, Is.False);
        var area = effect.FindProperty("waveArea").objectReferenceValue as BoxCollider;
        Assert.That(area.size, Is.EqualTo(new Vector3(1.6f, 1, 7)));
        Assert.That(area.enabled, Is.False, "The server sweeps the leading edge, not the full lane.");
    }
    [Test]
    public void EveryCastHasADistinctPersistedSpawnHash()
    {
        var hashes = Enum.GetValues(Type("FlygonCast")).Cast<object>().Select(kind =>
        {
            string path = (string)Rule("Path", kind);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            long live = new SerializedObject(prefab.GetComponent(Type("Unity.Netcode.NetworkObject"))).FindProperty("GlobalObjectIdHash").longValue;
            var match = Regex.Match(File.ReadAllText(path), @"(?m)^  GlobalObjectIdHash: (\d+)");
            Assert.That(match.Success, Is.True); Assert.That(long.Parse(match.Groups[1].Value), Is.EqualTo(live));
            return live;
        }).ToArray();
        Assert.That(hashes.Distinct().Count(), Is.EqualTo(7));
    }
    [Test]
    public void CharacterHasFifteenSpellVampEntriesAndDocumentedPassive()
    {
        var data = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Flygon.asset"));
        Assert.That(data.FindProperty("spellVamp").arraySize, Is.EqualTo(15));
        Assert.That(data.FindProperty("description").stringValue, Does.Contain("30%"));
    }
    [Test]
    public void RuntimeDoesNotConstructGeometryOrUseTheSharedSpawnCallback()
    {
        foreach (string path in Directory.GetFiles("Assets/Scripts/Moves/Flygon", "*.cs"))
        {
            string source = File.ReadAllText(path);
            foreach (string pattern in new[] { "new GameObject(", "CreatePrimitive(", "new Mesh ", "onObjectSpawned", "DOJump(" })
                Assert.That(source, Does.Not.Contain(pattern), path);
        }
    }
    [Test]
    public void DigResetRestoresGroundTraversalWithoutBorrowingFlying()
    {
        var root = new GameObject("Flygon traversal test");
        var ground = new GameObject("ground walkmesh"); var crossing = new GameObject("crossing walkmesh");
        try
        {
            var controller = root.AddComponent<CharacterController>();
            var movement = root.AddComponent(Type("PlayerMovement"));
            var withWalls = ground.AddComponent<BoxCollider>(); withWalls.size = new Vector3(20, 0.1f, 20); ground.transform.position = Vector3.down * 30;
            var withoutWalls = crossing.AddComponent<BoxCollider>(); withoutWalls.size = new Vector3(20, 0.1f, 20); crossing.transform.position = Vector3.down * 40;
            void Field(string name, object value) => movement.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, value);
            Field("characterController", controller); Field("walkableWithWalls", withWalls); Field("walkableNoWalls", withoutWalls);
            Physics.SyncTransforms();
            movement.GetType().GetMethod("BeginFlygonDig").Invoke(movement, null);
            Assert.That(movement.GetType().GetProperty("IsFlygonDigging").GetValue(movement), Is.True);
            Assert.That(movement.GetType().GetProperty("IsFlying").GetValue(movement), Is.False);
            root.transform.position = Vector3.right * 100;
            movement.GetType().GetMethod("EndFlygonDig").Invoke(movement, new object[] { true });
            Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(movement.GetType().GetProperty("IsFlygonDigging").GetValue(movement), Is.False);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(ground); Object.DestroyImmediate(crossing); }
    }
    [Test]
    public void DisablingPresentationRestoresTheModelOffset()
    {
        var root = new GameObject("Flygon visual test"); var model = new GameObject("model");
        try
        {
            var view = root.AddComponent(Type("FlygonMovePresentation"));
            view.GetType().GetField("model", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, model.transform);
            view.GetType().GetField("modelOrigin", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, Vector3.up * 0.2f);
            model.transform.localPosition = Vector3.down * 2;
            // EditMode does not dispatch ordinary MonoBehaviour activation callbacks.
            view.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            Assert.That(model.transform.localPosition, Is.EqualTo(Vector3.up * 0.2f));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(model); }
    }
    [Test]
    public void GroundTelegraphSeparatesAreaRadiusFromCastRangeAndResetsForOtherMoves()
    {
        var root = new GameObject("Flygon aim test");
        var area = new GameObject("ground area"); var range = new GameObject("cast range");
        try
        {
            var aim = root.AddComponent(Type("Aim"));
            aim.GetType().GetField("circleAreaIndicator", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(aim, area);
            aim.GetType().GetField("circleIndicator", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(aim, range);
            aim.GetType().GetField("playerTransform", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(aim, root.transform);
            aim.GetType().GetMethod("InitializeSizedCircleAreaIndicator").Invoke(aim, new object[] { 3.5f, 2.5f });
            Assert.That(area.transform.localScale, Is.EqualTo(new Vector3(2.5f, 1, 2.5f)));
            Assert.That(range.transform.localScale.x, Is.EqualTo(3.5f / 2.5f));
            aim.GetType().GetMethod("InitializeCircleAreaIndicator").Invoke(aim, new object[] { 3f });
            Assert.That(area.transform.localScale, Is.EqualTo(Vector3.one));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(area); Object.DestroyImmediate(range); }
    }
    [Test]
    public void UniteRestorationExitsTheAuthoredHiddenPoseWithoutEnablingFogHiddenRenderers()
    {
        var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Flygon/Flygon.prefab"));
        try
        {
            var animator = model.GetComponentInChildren<Animator>();
            animator.Rebind(); animator.Update(0);
            Transform bone = animator.transform.Find("Armature/pm0330_00/Origin");
            Vector3 normal = bone.localScale;
            animator.Play("Tornado", 0, 0); animator.Update(0);
            Assert.That(bone.localScale.x, Is.LessThan(normal.x * 0.02f), "Exercise the real animation that hides Flygon.");
            var renderer = model.GetComponentInChildren<Renderer>(); renderer.enabled = false;
            bool restored = (bool)Type("TornadoHitbox").GetMethod("RestoreModelAnimation").Invoke(null, new object[] { animator });
            animator.Update(0);
            Assert.That(restored, Is.True);
            Assert.That(Vector3.Distance(bone.localScale, normal), Is.LessThan(0.001f));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.Not.EqualTo(Animator.StringToHash("Tornado")));
            Assert.That(renderer.enabled, Is.False, "Unite cleanup must not override fog/death visibility.");
        }
        finally { Object.DestroyImmediate(model); }
    }
    [Test]
    public void LateUniteCleanupDoesNotInterruptAnAlreadyStartedMove()
    {
        var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Flygon/Flygon.prefab"));
        try
        {
            var animator = model.GetComponentInChildren<Animator>(); animator.Rebind();
            int attack = Animator.StringToHash("Armature_pm0330_00_ba20_buturi01");
            animator.Play(attack, 0, 0); animator.Update(0);
            bool restored = (bool)Type("TornadoHitbox").GetMethod("RestoreModelAnimation").Invoke(null, new object[] { animator });
            animator.Update(0);
            Assert.That(restored, Is.False);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(attack));
        }
        finally { Object.DestroyImmediate(model); }
    }
}

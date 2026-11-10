using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TrevenantTests
{
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string name) => Enum.Parse(T(type), name);
    private static object P(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
    private static object F(object value, string name) => value.GetType().GetField(name).GetValue(value);
    private static object Rule(string method, params object[] args) => T("TrevenantMoveRules").GetMethod(method, args.Select(a => a.GetType()).ToArray()).Invoke(null, args);

    [TestCase("TrevenantWillOWisp", 6)]
    [TestCase("TrevenantBranchPoke", 7.5f)]
    [TestCase("TrevenantWoodHammer", 8)]
    [TestCase("TrevenantCurse", 6)]
    [TestCase("TrevenantHornLeech", 9)]
    [TestCase("TrevenantPainSplit", 10)]
    [TestCase("TrevenantPhantomForest", 0)]
    public void FactoriesAssetsAndNetworkRegistration(string name, float cooldown)
    {
        var id = E("AvailableMoves", name);
        var move = T("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { id });
        Assert.That(F(move, "Cooldown"), Is.EqualTo(cooldown));
        Assert.That(P(move, "RequiresServerAcceptance"), Is.True);
        Assert.That(P(move, "CooldownOnEffectEnd"), Is.True);
        Assert.That(P(move, "CooldownOnFinish"), Is.False);
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Trevenant/" + name + ".asset"); Assert.That(asset, Is.Not.Null);
        var data = new SerializedObject(asset); Assert.That(data.FindProperty("icon").objectReferenceValue, Is.Not.Null);
        string guid = data.FindProperty("preview").FindPropertyRelative("m_AssetGUID").stringValue; Assert.That(guid, Is.Not.Empty);
        Assert.That(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)), Is.Not.Null);
        var previews = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovePreviews.asset")).FindProperty("m_SerializeEntries");
        Assert.That(Enumerable.Range(0, previews.arraySize).Any(i => previews.GetArrayElementAtIndex(i).FindPropertyRelative("m_GUID").stringValue == guid), Is.True);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>((string)P(move, "PrefabPath")); Assert.That(prefab, Is.Not.Null);
        var effect = prefab.GetComponent(T("TrevenantMoveEffect")); Assert.That(effect, Is.Not.Null);
        Assert.That(new SerializedObject(prefab.GetComponent(T("Unity.Netcode.NetworkObject"))).FindProperty("GlobalObjectIdHash").longValue, Is.Not.Zero);
        var network = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")).FindProperty("List");
        Assert.That(Enumerable.Range(0, network.arraySize).Any(i => network.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab), Is.True);
        var effects = new SerializedObject(effect);
        foreach (string field in new[] { "graphics", "castVisual", "activeVisual", "impactVisual" }) Assert.That(effects.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        Assert.That(prefab.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True, "Authoring colliders must not block characters.");
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset")).FindProperty("moveEntries");
        Assert.That(Enumerable.Range(0, registry.arraySize).Any(i => registry.GetArrayElementAtIndex(i).FindPropertyRelative("moveAsset").objectReferenceValue == asset), Is.True);
    }
    [Test]
    public void ProgressionPassiveAndSelection()
    {
        var data = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Trevenant.asset")); var rows = data.FindProperty("learnableMoves");
        CollectionAssert.AreEqual(new[] { 0, 2, 4, 6, 8, 10, 12 }, Enumerable.Range(0, rows.arraySize).Select(i => rows.GetArrayElementAtIndex(i).FindPropertyRelative("level").intValue));
        Assert.That(rows.GetArrayElementAtIndex(5).FindPropertyRelative("isUpgraded").boolValue, Is.True);
        Assert.That(rows.GetArrayElementAtIndex(6).FindPropertyRelative("isUpgraded").boolValue, Is.True);
        Assert.That(data.FindProperty("evolutions").GetArrayElementAtIndex(1).FindPropertyRelative("level").intValue, Is.EqualTo(4));
        Assert.That(data.FindProperty("passive").enumValueIndex, Is.EqualTo(Convert.ToInt32(E("AvailablePassives", "TrevenantPassive"))));
        var basic = T("BasicAttacksDatabase").GetMethod("GetBasicAttack").Invoke(null, new object[] { "Trevenant" }); Assert.That(P(basic, "ServerResolved"), Is.True);
        var selection = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/PokemonSelectInfo/Trevenant.asset"));
        Assert.That(selection.FindProperty("pClass").enumValueIndex, Is.EqualTo(1)); Assert.That(selection.FindProperty("Difficulty").enumValueIndex, Is.EqualTo(1));
    }
    [TestCase(0.71f, 1.1f, 12, 0.05f, 0.3f)]
    [TestCase(0.7f, 1.3f, 17, 0.06f, 0.4f)]
    [TestCase(0.4f, 1.3f, 17, 0.06f, 0.4f)]
    [TestCase(0.39f, 1.5f, 22, 0.07f, 0.5f)]
    public void HpThresholds(float hp, float stun, short slow, float heal, float redirect)
    {
        Assert.That((float)Rule("HammerStun", hp), Is.EqualTo(stun).Within(0.0001));
        Assert.That(Rule("CurseSlow", hp), Is.EqualTo(slow));
        Assert.That((float)Rule("HornHeal", hp), Is.EqualTo(heal).Within(0.0001));
        Assert.That((float)Rule("PainRedirect", hp), Is.EqualTo(redirect).Within(0.0001));
    }
    [TestCase(1000, 10000, 0.05f, false, 50)]
    [TestCase(1000, 10000, 0.02f, true, 200)]
    [TestCase(100, 10000, 0.02f, true, 99)]
    [TestCase(1, 10000, 0.02f, true, 0)]
    public void HealthCostsAreNonlethal(int hp, int maximum, float ratio, bool fromMax, int expected)
        => Assert.That(Rule("HealthCost", hp, maximum, ratio, fromMax), Is.EqualTo(expected));
    [TestCase(0, 0)]
    [TestCase(1, 0.05f)]
    [TestCase(10, 0.5f)]
    [TestCase(20, 1)]
    [TestCase(40, 1)]
    public void CurseCountsEventsAndCaps(int count, float expected) => Assert.That((float)Rule("GaugeFraction", count), Is.EqualTo(expected).Within(0.0001));
    [Test]
    public void LiveCurseGaugeAcceptsExactlyTwentyEvents()
    {
        var root = new GameObject("CurseTest");
        try
        {
            var curse = root.AddComponent(T("TrevenantCurseEffect"));
            curse.GetType().GetField("active", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(curse, true);
            for (int i = 0; i < 19; i++) curse.GetType().GetMethod("RecordDamageEvent").Invoke(curse, null);
            Assert.That((float)P(curse, "Gauge"), Is.EqualTo(0.95f).Within(0.0001));
            curse.GetType().GetMethod("RecordDamageEvent").Invoke(curse, null); Assert.That(P(curse, "Gauge"), Is.EqualTo(1f));
            curse.GetType().GetMethod("RecordDamageEvent").Invoke(curse, null); Assert.That(P(curse, "Gauge"), Is.EqualTo(1f));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [Test]
    public void RedirectedDamageCannotReenterRedirectionHooks()
    {
        var root = new GameObject("DamageFilterTest");
        try
        {
            var pokemon = root.AddComponent(T("Pokemon"));
            var hook = T("Pokemon").GetEvent("ModifyIncomingDamage");
            var damageArg = Expression.Parameter(T("DamageInfo"), "damage"); var amount = Expression.Parameter(typeof(int), "amount");
            var half = Expression.Lambda(hook.EventHandlerType, Expression.Divide(amount, Expression.Constant(2)), damageArg, amount).Compile();
            hook.AddEventHandler(pokemon, half);
            var filter = T("Pokemon").GetMethod("FilterIncomingDamage", BindingFlags.Instance | BindingFlags.NonPublic);
            var damage = Activator.CreateInstance(T("DamageInfo"));
            Assert.That(filter.Invoke(pokemon, new[] { damage, (object)100 }), Is.EqualTo(50));
            T("DamageInfo").GetField("source").SetValue(damage, E("DamageSource", "Redirected"));
            Assert.That(filter.Invoke(pokemon, new[] { damage, (object)100 }), Is.EqualTo(100));
            T("Pokemon").GetEvent("ModifyHealingReceived").AddEventHandler(pokemon, new Func<int, int>(n => n * 3 / 2));
            Assert.That(T("Pokemon").GetMethod("FilterHealingReceived", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pokemon, new object[] { 100 }), Is.EqualTo(150));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [Test]
    public void GeometryIsTunableAndUpgradesUseActualRadius()
    {
        var field = T("TrevenantMoveRules").GetField("WispRange"); float original = (float)field.GetValue(null);
        try { field.SetValue(null, 6.25f); Assert.That(P(Activator.CreateInstance(T("TrevenantWillOWisp")), "Range"), Is.EqualTo(6.25f)); }
        finally { field.SetValue(null, original); }
        Assert.That(Rule("CurseArea", false), Is.EqualTo(3.5f)); Assert.That(Rule("CurseArea", true), Is.EqualTo(4.3f));
        Assert.That(T("TrevenantMoveRules").GetField("PainPlusDuration").GetValue(null), Is.EqualTo(8.5f));
        Assert.That(T("TrevenantMoveRules").GetField("UniteRadius").GetValue(null), Is.EqualTo(4.5f));
        Assert.That((float)Rule("PassiveHeal", 14), Is.EqualTo(0.0452f).Within(0.0001));
    }
    [TestCase(0)]
    [TestCase(90)]
    [TestCase(217)]
    public void WispsDivergeSymmetricallyRelativeToAim(float yaw)
    {
        Vector3 aim = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        var origin = new Vector3(7, 2, -3);
        var left = (Vector3)Rule("WispDirection", aim, 0); var middle = (Vector3)Rule("WispDirection", aim, 1); var right = (Vector3)Rule("WispDirection", aim, 2);
        Assert.That(Vector3.Distance(middle, aim), Is.LessThan(0.0001f));
        Assert.That(Vector3.SignedAngle(middle, left, Vector3.up), Is.EqualTo(-25).Within(0.001f));
        Assert.That(Vector3.SignedAngle(middle, right, Vector3.up), Is.EqualTo(25).Within(0.001f));
        for (int i = 0; i < 3; i++)
        {
            Vector3 start = (Vector3)Rule("WispPosition", origin, aim, i, 0f);
            Vector3 end = (Vector3)Rule("WispPosition", origin, aim, i, 5f);
            Assert.That(Vector3.Distance(start, end), Is.EqualTo(5).Within(0.001f));
            Assert.That(Vector3.Distance((end - start).normalized, (Vector3)Rule("WispDirection", aim, i)), Is.LessThan(0.001f));
        }
        var angle = T("TrevenantMoveRules").GetField("WispSpreadAngle"); var original = angle.GetValue(null);
        try
        {
            angle.SetValue(null, 12f);
            Assert.That(Vector3.Angle(aim, (Vector3)Rule("WispDirection", aim, 0)), Is.EqualTo(12).Within(0.001f));
        }
        finally { angle.SetValue(null, original); }
    }

    [TestCase(1)]
    [TestCase(2)]
    public void BothHammerAnimationsStrikeAtWindupAndFinishAtRecoveryWithoutRestarting(int stage)
    {
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Trevenant/Trevenant.prefab"));
            SceneManager.MoveGameObjectToScene(model, preview);
            var animator = model.GetComponentInChildren<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Update(0); animator.speed = 1.25f;
            var manager = model.AddComponent(T("AnimationManager")); var type = manager.GetType();
            type.GetMethod("AssignAnimator").Invoke(manager, new object[] { animator });
            string state = "ani_spell1a" + stage + "_bat_0709";
            float contact = (float)T("TrevenantMoveRules").GetField(stage == 1 ? "HammerSweepContact" : "HammerSlamContact").GetValue(null);
            type.GetMethod("PlayLocalContactAnimation").Invoke(manager, new object[] { state, 0.35f, 0.2f, contact }); animator.Update(0);
            animator.Update(0.35f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(contact).Within(0.001f));
            type.GetField("pendingStateAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, Time.time - 0.35f);
            type.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(contact).Within(0.001f), "Speed change must not restart the clip.");
            animator.Update(0.2f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(1).Within(0.001f));
            Assert.That(type.GetMethod("RecoverLocalAnimation").Invoke(manager, new object[] { state, "ani_idle_bat_0709", 0f }), Is.True);
            Assert.That(animator.speed, Is.EqualTo(1.25f));

            // Visibility-delayed presentation resumes inside the windup rather
            // than replaying an old cast or reverting to whole-clip timing.
            model.SetActive(false);
            type.GetMethod("PlayLocalContactAnimation").Invoke(manager, new object[] { state, 0.35f, 0.2f, contact });
            type.GetField("pendingStateAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, Time.time - 0.175f);
            model.SetActive(true); animator.Update(0);
            type.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null); animator.Update(0);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(contact * 0.5f).Within(0.002f));
            type.GetMethod("PlayLocalAnimation").Invoke(manager, new object[] { "ani_idle_bat_0709", 0f, 0f });
            Assert.That(animator.speed, Is.EqualTo(1.25f));
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
    [TestCase("Wisp", 0.58f, 10, 190, true)]
    [TestCase("Hammer", 1.1f, 22, 242, false)]
    [TestCase("Branch", 0.75f, 9, 280, false)]
    [TestCase("Horn", 0.43f, 4, 200, false)]
    [TestCase("Pain", 0.29f, 2, 80, false)]
    [TestCase("Unite", 1.6f, 15, 700, false)]
    public void DamageFormulas(string method, float ratio, int slider, int value, bool parameter)
    {
        object damage = method == "Wisp" ? Rule(method, 1ul, parameter) : Rule(method, 1ul);
        Assert.That(F(damage, "ratio"), Is.EqualTo(ratio)); Assert.That(F(damage, "slider"), Is.EqualTo(slider)); Assert.That(F(damage, "baseDmg"), Is.EqualTo(value));
        Assert.That(F(damage, "type").ToString(), Is.EqualTo("Physical"));
    }
    [Test]
    public void AnimatorStatesAndIndicatorsExist()
    {
        foreach (string form in new[] { "Phantump", "Trevenant" })
        {
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Model/Pokemons/" + form + "/" + form + ".controller");
            string suffix = form == "Phantump" ? "0708" : "0709";
            string[] clips = form == "Phantump" ? new[] { "idle", "atk1", "atk2", "atk3", "spell1", "spell21", "spell22", "spell23" }
                : new[] { "idle", "atk1", "atk2", "atk3", "spell1", "spell21", "spell22", "spell23", "spell1a1", "spell1a2", "spell1b1", "spell1b2", "spell2a1", "spell2a2", "spell2a3", "spell2b", "spellu1", "spellu2", "spellu3" };
            foreach (string clip in clips) Assert.That(controller.layers[0].stateMachine.states.Any(s => s.state.name == "ani_" + clip + "_bat_" + suffix), Is.True, form + ": " + clip);
        }
        foreach (string name in new[] { "HammerFollowup", "CurseDetonate" }) Assert.That(Resources.Load<Sprite>("Effects/Trevenant/" + name), Is.Not.Null);
        foreach (string name in new[] { "pref_Indicator_Sector_180_lv3", "HammerLineIndicator" }) Assert.That(Resources.Load<GameObject>("Effects/Trevenant/" + name), Is.Not.Null);
    }
    [Test]
    public void HammerUsesSuppliedSectorMeshAtTheCorrectRadius()
    {
        var move = Activator.CreateInstance(T("TrevenantWoodHammer"));
        Assert.That(P(move, "IndicatorPath"), Is.EqualTo("Effects/Trevenant/pref_Indicator_Sector_180_lv3"));
        var source = Resources.Load<GameObject>((string)P(move, "IndicatorPath"));
        Assert.That(source.GetComponentsInChildren<MeshFilter>(true).Any(m => AssetDatabase.GetAssetPath(m.sharedMesh)
            == "Assets/Sprites/AimIndicator/pref_Indicator_Sector_180_lv3/pref_Indicator_Sector_180_lv3.fbx"), Is.True);
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = UnityEngine.Object.Instantiate(source); SceneManager.MoveGameObjectToScene(root, preview);
            var renderer = root.GetComponentInChildren<Renderer>();
            Assert.That(renderer.bounds.max.z, Is.EqualTo(4).Within(0.02f));
            Assert.That(renderer.bounds.size.x, Is.EqualTo(8).Within(0.1f));
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
    [TestCase(1, 0.1f)]
    [TestCase(1, 0.35f)]
    [TestCase(1, 0.65f)]
    [TestCase(1, 0.85f)]
    [TestCase(2, 0.1f)]
    [TestCase(2, 0.35f)]
    [TestCase(2, 0.65f)]
    [TestCase(2, 0.85f)]
    public void HammerAttachmentsFollowTheAnimatedHands(int stage, float fraction)
    {
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Trevenant/Trevenant.prefab"));
            SceneManager.MoveGameObjectToScene(model, preview);
            var effect = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Trevenant/TrevenantWoodHammer.prefab"));
            SceneManager.MoveGameObjectToScene(effect, preview);
            var view = effect.GetComponentInChildren(T("TrevenantWoodHammerView"), true); Assert.That(view, Is.Not.Null);
            var animator = model.GetComponentInChildren<Animator>();
            var clip = animator.runtimeAnimatorController.animationClips.First(c => c.name == "ani_spell1a" + stage + "_bat_0709");
            clip.SampleAnimation(animator.gameObject, clip.length * fraction);
            view.GetType().GetMethod("BindModel").Invoke(view, new object[] { animator.transform });
            double now = Time.timeAsDouble;
            view.GetType().GetMethod("Swing").Invoke(view, new object[] { stage, now, 1f });
            view.GetType().GetMethod("ApplyPose").Invoke(view, new object[] { now + 0.2 });
            var bones = model.GetComponentsInChildren<Transform>();
            var left = bones.First(b => b.name == "Bip001 L Hand"); var right = bones.First(b => b.name == "Bip001 R Hand");
            var data = new SerializedObject(view);
            var leftHammer = (Transform)data.FindProperty("leftHammer").objectReferenceValue;
            var rightHammer = (Transform)data.FindProperty("rightHammer").objectReferenceValue;
            var slamHammer = (Transform)data.FindProperty("slamHammer").objectReferenceValue;
            Assert.That(leftHammer.gameObject.activeSelf, Is.EqualTo(stage == 1)); Assert.That(slamHammer.gameObject.activeSelf, Is.EqualTo(stage == 2));
            if (stage == 1)
            {
                Assert.That(Vector3.Distance(leftHammer.position, left.position), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(rightHammer.position, right.position), Is.LessThan(0.0001f));
            }
            else Assert.That(Vector3.Distance(slamHammer.position, (left.position + right.position) * 0.5f), Is.LessThan(0.0001f));
            model.transform.SetPositionAndRotation(new Vector3(7, 0, -3), Quaternion.Euler(0, 70, 0));
            view.GetType().GetMethod("ApplyPose").Invoke(view, new object[] { now + 0.25 });
            Assert.That(Vector3.Distance(stage == 1 ? leftHammer.position : slamHammer.position,
                stage == 1 ? left.position : (left.position + right.position) * 0.5f), Is.LessThan(0.0001f));
            view.GetType().GetMethod("Hide").Invoke(view, null);
            Assert.That(leftHammer.gameObject.activeSelf || rightHammer.gameObject.activeSelf || slamHammer.gameObject.activeSelf, Is.False);
            Assert.That(effect.GetComponentsInChildren<TrailRenderer>(true).All(t => !t.emitting), Is.True);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
}

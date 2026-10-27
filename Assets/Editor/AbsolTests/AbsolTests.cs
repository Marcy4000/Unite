using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class AbsolTests
{
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string value) => Enum.Parse(T(type), value);
    private static object Rule(string name, params object[] args) => T("AbsolMoveRules").GetMethod(name).Invoke(null, args);
    private static V Field<V>(object target, string name) => (V)target.GetType().GetField(name).GetValue(target);

    [TestCase("AbsolFeint", 8)]
    [TestCase("AbsolSlash", 6)]
    [TestCase("AbsolNightSlash", 5.5f)]
    [TestCase("AbsolPursuit", 5.5f)]
    [TestCase("AbsolPsychoCut", 7)]
    [TestCase("AbsolSuckerPunch", 5)]
    [TestCase("AbsolMidnightSlash", 0)]
    public void MovesAssetsIconsPreviewsAndRegistryAreWired(string name, float cooldown)
    {
        object id = E("AvailableMoves", name);
        var move = T("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { id });
        Assert.That(move.GetType().Name, Is.EqualTo(name));
        Assert.That(Field<float>(move, "Cooldown"), Is.EqualTo(cooldown));
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Absol/" + name + ".asset");
        Assert.That(asset, Is.Not.Null);
        var serialized = new SerializedObject(asset);
        Assert.That(serialized.FindProperty("move").longValue, Is.EqualTo(Convert.ToInt64(id)));
        Assert.That(serialized.FindProperty("cooldown").floatValue, Is.EqualTo(cooldown));
        Assert.That(serialized.FindProperty("icon").objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("preview").FindPropertyRelative("m_AssetGUID").stringValue, Is.Not.Empty);
        Assert.That(serialized.FindProperty("description").stringValue, Is.Not.Empty);
        if (name == "AbsolMidnightSlash") Assert.That(serialized.FindProperty("uniteEnergyCost").intValue, Is.EqualTo(80000));
        var registry = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset");
        Assert.That(registry.GetType().GetMethod("GetMoveAsset").Invoke(registry, new[] { id }), Is.SameAs(asset));
        move.GetType().GetMethod("ResetMove").Invoke(move, null);
        move.GetType().GetMethod("ResetMove").Invoke(move, null);
    }

    [Test]
    public void LearnsetMatchesDatabaseAndExistingSerializedValuesStayStable()
    {
        var pokemon = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Absol.asset"));
        Assert.That(pokemon.FindProperty("passive").enumValueIndex, Is.EqualTo(Convert.ToInt32(E("AvailablePassives", "AbsolPassive"))));
        int[] levels = { 0, 2, 4, 6, 8, 10, 12 }; var rows = pokemon.FindProperty("learnableMoves");
        Assert.That(rows.arraySize, Is.EqualTo(levels.Length));
        for (int i = 0; i < levels.Length; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            Assert.That(row.FindPropertyRelative("level").intValue, Is.EqualTo(levels[i]));
            Assert.That(row.FindPropertyRelative("isUpgraded").boolValue, Is.EqualTo(i >= 5));
            Assert.That(row.FindPropertyRelative("moves").arraySize, Is.EqualTo(i == 4 ? 1 : 2));
        }
        Assert.That(Convert.ToInt32(E("AvailableMoves", "SylvMysticalCringe")), Is.EqualTo(17));
        Assert.That(Convert.ToInt32(E("AvailableMoves", "DuraDragonPulse")), Is.EqualTo(92));
        Assert.That(Convert.ToInt32(E("AvailableMoves", "CinderBlazeKick")), Is.EqualTo(97));
        Assert.That(T("BasicAttacksDatabase").GetMethod("GetBasicAttack").Invoke(null, new object[] { "Absol" }).GetType().Name, Is.EqualTo("AbsolBasicAtk"));
        Assert.That(T("PassiveDatabase").GetMethod("GetPassive").Invoke(null, new[] { E("AvailablePassives", "AbsolPassive") }).GetType().Name, Is.EqualTo("AbsolPassive"));
    }

    [TestCase(0, 0)] [TestCase(0.1f, 8)] [TestCase(0.375f, 30)] [TestCase(1, 30)]
    public void NightSlashCritScalesWithMissingHealthAndCaps(float missing, float expected) => Assert.That(Rule("NightCrit", missing), Is.EqualTo(expected).Within(0.0001f));

    [Test]
    public void FormulasPreserveReferenceCoefficientsAndDamageClassification()
    {
        var isolated = Rule("BasicDamage", 12ul, true, true);
        Assert.That(Field<float>(isolated, "ratio"), Is.EqualTo(0.885f));
        Assert.That(Field<short>(isolated, "slider"), Is.EqualTo(6));
        Assert.That(Field<short>(isolated, "baseDmg"), Is.EqualTo(120));
        Assert.That(Field<object>(isolated, "proprieties").ToString(), Does.Contain("CanCrit"));
        var feint = Rule("DashDamage", 12ul, true, false, 0f);
        Assert.That(Field<object>(feint, "type").ToString(), Is.EqualTo("True"));
        var second = Rule("DashDamage", 12ul, false, true, 0.5f);
        Assert.That(Field<float>(second, "ratio"), Is.EqualTo(1.035f));
        Assert.That(Field<float>(second, "bonusCritRate"), Is.EqualTo(30));
        var back = Rule("PursuitDamage", 12ul, true);
        Assert.That(Field<float>(back, "ratio"), Is.EqualTo(2.14f));
        var mark = Rule("MarkDamage", 12ul, 1);
        Assert.That(Field<float>(mark, "ratio"), Is.EqualTo(1.08f));
        Assert.That(Field<float>(mark, "bonusCritRate"), Is.EqualTo(15));
        Assert.That(Rule("CanConsumeMark", mark), Is.False);
        Assert.That(Rule("CanConsumeMark", isolated), Is.True);
        T("DamageInfo").GetField("proprieties").SetValue(isolated, E("DamageProprieties", "IsMuscleBand"));
        Assert.That(Rule("CanConsumeMark", isolated), Is.False);
        T("DamageInfo").GetField("source").SetValue(back, E("DamageSource", "Item"));
        Assert.That(Rule("CanConsumeMark", back), Is.False);
    }

    [TestCase(1, 4)] [TestCase(3, 3)] [TestCase(5, 2)] [TestCase(8, 1)]
    public void MidnightSlashRetainsDistanceDependentFinalHits(float distance, int count) => Assert.That(Rule("FinalHitCount", distance), Is.EqualTo(count));

    [Test]
    public void BackstabsUseRearSectorAndCurrentFacing()
    {
        Assert.That(Rule("IsBackstab", Vector3.back, Vector3.zero, Vector3.forward), Is.True);
        Assert.That(Rule("IsBackstab", Vector3.forward, Vector3.zero, Vector3.forward), Is.False);
        Assert.That(Rule("IsBackstab", Vector3.right, Vector3.zero, Vector3.forward), Is.False);
        Assert.That(Rule("IsBackstab", Vector3.back, Vector3.zero, Vector3.back), Is.False);
    }

    [TestCase("AbsolFeint", "AbsolDashEffect")]
    [TestCase("AbsolNightSlash", "AbsolDashEffect")]
    [TestCase("AbsolPursuit", "AbsolDashEffect")]
    [TestCase("AbsolSlash", "AbsolSlashEffect")]
    [TestCase("AbsolPsychoCut", "AbsolPsychoCutEffect")]
    [TestCase("AbsolSuckerPunch", "AbsolSuckerPunchEffect")]
    [TestCase("AbsolMidnightSlash", "AbsolMidnightSlashEffect")]
    public void EffectsHavePersistedNetworkHashesBakedArtAndRegistrations(string name, string component)
    {
        string path = "Assets/Prefabs/Objects/Moves/Absol/" + name + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null); Assert.That(prefab.GetComponent(T(component)), Is.Not.Null);
        long hash = new SerializedObject(prefab.GetComponent(T("Unity.Netcode.NetworkObject"))).FindProperty("GlobalObjectIdHash").longValue;
        Assert.That(hash, Is.Not.Zero); Assert.That(File.ReadAllText(path), Does.Contain("GlobalObjectIdHash: " + hash));
        Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
        Assert.That(prefab.GetComponentsInChildren<LineRenderer>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), Is.True);
        foreach (var mesh in prefab.GetComponentsInChildren<MeshFilter>(true)) Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh), Is.Not.Empty);
        var list = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")).FindProperty("List");
        int matches = 0; for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) matches++;
        Assert.That(matches, Is.EqualTo(1));
        var entries = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovesAssets.asset")).FindProperty("m_SerializeEntries");
        matches = 0;
        for (int i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("m_GUID").stringValue == AssetDatabase.AssetPathToGUID(path))
        { matches++; Assert.That(entries.GetArrayElementAtIndex(i).FindPropertyRelative("m_Address").stringValue, Is.EqualTo(path)); }
        Assert.That(matches, Is.EqualTo(1));
    }

    [Test]
    public void AimAndModelAssetsArePresentAndRootMotionStaysDisabled()
    {
        foreach (string name in new[] { "AbsolArc", "AbsolSlash", "AbsolMidnightSlash" })
        {
            var indicator = Resources.Load<GameObject>("AimIndicators/" + name);
            Assert.That(indicator, Is.Not.Null); Assert.That(indicator.GetComponentInChildren<MeshFilter>().sharedMesh, Is.Not.Null);
        }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Absol/Absol.prefab");
        Assert.That(model.GetComponentInChildren<Animator>().applyRootMotion, Is.False);
        var controller = model.GetComponentInChildren<Animator>().runtimeAnimatorController;
        Assert.That(controller.animationClips.Any(c => c.name.StartsWith("ani_spellu1")), Is.True);
        Assert.That(controller.animationClips.Any(c => c.name.StartsWith("ani_spellu2")), Is.True);
        var night = Activator.CreateInstance(T("AbsolNightSlash"));
        Assert.That(night.GetType().GetProperty("CooldownOnFinish").GetValue(night), Is.False);
        Assert.That(Activator.CreateInstance(T("SylvCalmMind")).GetType().GetProperty("CooldownOnFinish").GetValue(Activator.CreateInstance(T("SylvCalmMind"))), Is.True);
    }

    [Test]
    public void TimedAnimationPlaybackRestoresLegacySpeedOnRecovery()
    {
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Absol/Absol.prefab"));
        try
        {
            var animator = model.GetComponentInChildren<Animator>(); animator.Update(0);
            var manager = model.AddComponent(T("AnimationManager"));
            manager.GetType().GetMethod("AssignAnimator").Invoke(manager, new object[] { animator });
            string slash = (string)T("AbsolMoveRules").GetField("SlashAnimation").GetRawConstantValue();
            Assert.That(animator.runtimeAnimatorController.animationClips.Any(c => c.name == slash), Is.True);
            manager.GetType().GetMethod("PlayLocalAnimation").Invoke(manager, new object[] { slash, 0.35f, 0f });
            Assert.That(animator.speed, Is.EqualTo(animator.runtimeAnimatorController.animationClips.First(c => c.name == slash).length / 0.35f).Within(0.001f));
            manager.GetType().GetMethod("PlayLocalAnimation").Invoke(manager, new object[] { "ani_idle_bat_0359_Copy", 0f, 0f });
            Assert.That(animator.speed, Is.EqualTo(1));
        }
        finally { UnityEngine.Object.DestroyImmediate(model); }
    }

    [TestCase(0.35f)]
    [TestCase(0.12f)]
    public void TimedCrossfadesKeepPlaybackSpeedAndDoNotLetOldRecoveryReplaceANewerMove(float duration)
    {
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Absol/Absol.prefab"));
        try
        {
            var animator = model.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            string idle = (string)T("AbsolMoveRules").GetField("Idle").GetRawConstantValue();
            string slash = (string)T("AbsolMoveRules").GetField("SlashAnimation").GetRawConstantValue();
            string psycho = (string)T("AbsolMoveRules").GetField("PsychoAnimation").GetRawConstantValue();
            animator.Play(idle, 0, 0); animator.Update(0); animator.speed = 1.25f;
            var manager = model.AddComponent(T("AnimationManager"));
            manager.GetType().GetMethod("AssignAnimator").Invoke(manager, new object[] { animator });
            var play = manager.GetType().GetMethod("PlayLocalAnimation");
            var recover = manager.GetType().GetMethod("RecoverLocalAnimation");
            var lateUpdate = manager.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            const float blend = 0.06f;
            float speed = animator.runtimeAnimatorController.animationClips.First(c => c.name == slash).length / duration;
            play.Invoke(manager, new object[] { slash, duration, blend }); animator.Update(0.001f);
            Assert.That(animator.IsInTransition(0), Is.True);
            Assert.That(animator.GetNextAnimatorStateInfo(0).shortNameHash, Is.EqualTo(Animator.StringToHash(slash)));
            lateUpdate.Invoke(manager, null);
            Assert.That(animator.speed, Is.EqualTo(speed).Within(0.001f), "An incoming timed state must retain its speed during the blend.");
            animator.Update(blend * 0.4f); Assert.That(animator.IsInTransition(0), Is.True);
            animator.Update(blend * 0.8f); Assert.That(animator.IsInTransition(0), Is.False, "Blend time must not change with duration-matched playback speed.");
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(Animator.StringToHash(slash)));

            play.Invoke(manager, new object[] { psycho, duration, blend }); animator.Update(0.001f);
            Assert.That(recover.Invoke(manager, new object[] { slash, idle, 0.1f }), Is.False, "Outgoing Slash cannot overwrite incoming Psycho Cut.");
            Assert.That(recover.Invoke(manager, new object[] { psycho, idle, 0.1f }), Is.True);
            Assert.That(animator.speed, Is.EqualTo(1.25f));
            animator.Update(0.001f); Assert.That(animator.IsInTransition(0), Is.True);
            Assert.That(animator.GetNextAnimatorStateInfo(0).shortNameHash, Is.EqualTo(Animator.StringToHash(idle)));

            // Legacy callers still opt out, and external stun/death transitions
            // must release a timed clip's speed instead of carrying it forward.
            play.Invoke(manager, new object[] { slash, duration, 0f }); animator.Update(0);
            Assert.That(animator.IsInTransition(0), Is.False);
            animator.Play("ani_stun_bat_0359_Copy", 0, 0); animator.Update(0); lateUpdate.Invoke(manager, null);
            Assert.That(animator.speed, Is.EqualTo(1.25f));
        }
        finally { UnityEngine.Object.DestroyImmediate(model); }
    }

    [TestCase("AbsolFeint", "pref_Indicator_Arc_R_2m5_lv3")]
    [TestCase("AbsolSlash", "pref_Indicator_Sector_120_lv3")]
    [TestCase("AbsolMidnightSlash", "pref_Indicator_Arrow_2dir_lv3")]
    public void ImportedAimArtKeepsItsFaceFlipAndRangeThroughRuntimeScaling(string moveName, string modelName)
    {
        var owner = new GameObject("Aim test");
        try
        {
            var aim = owner.AddComponent(T("Aim"));
            foreach (string field in new[] { "indicatorHolders", "dashIndicator", "skillShotLine" })
            {
                var child = new GameObject(field); child.transform.SetParent(owner.transform, false);
                aim.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(aim, child);
            }
            var move = Activator.CreateInstance(T(moveName));
            var scale = (Vector3)move.GetType().GetProperty("IndicatorScale").GetValue(move);
            string path = (string)move.GetType().GetProperty("IndicatorPath").GetValue(move);
            foreach (bool mirror in new[] { false, true })
            {
                aim.GetType().GetMethod("ShowCustomIndicator").Invoke(aim, new object[] { path, scale, mirror });
                var indicator = (GameObject)aim.GetType().GetField("customIndicator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(aim);
                var art = indicator.transform.Find("NormalizedFootprint/" + modelName);
                Assert.That(art, Is.Not.Null);
                Assert.That(art.localScale, Is.EqualTo(new Vector3(1, -1, 1)));
                string source = "Assets/Sprites/AimIndicator/" + modelName + "/" + modelName + ".fbx";
                var authoredArt = Resources.Load<GameObject>(path).transform.Find("NormalizedFootprint/" + modelName);
                Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(authoredArt.gameObject)), Is.EqualTo(source));
                // Renderer AABBs include unused corners after the authored arc
                // rotation; measure the actual footprint vertices instead.
                var bounds = new Bounds(); bool first = true; float radius = 0, halfAngle = 0;
                foreach (var filter in art.GetComponentsInChildren<MeshFilter>())
                {
                    Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Is.EqualTo(source));
                    foreach (var vertex in filter.sharedMesh.vertices)
                    {
                        var point = filter.transform.TransformPoint(vertex);
                        radius = Mathf.Max(radius, new Vector2(point.x, point.z).magnitude);
                        halfAngle = Mathf.Max(halfAngle, Mathf.Abs(Mathf.Atan2(point.x, point.z) * Mathf.Rad2Deg));
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                    }
                }
                foreach (var renderer in art.GetComponentsInChildren<Renderer>())
                {
                    Assert.That(renderer.sharedMaterials.All(m => m != null && m.mainTexture != null), Is.True);
                }
                Assert.That(bounds.center.y, Is.EqualTo(0.06f).Within(0.001f));
                if (moveName == "AbsolSlash")
                {
                    float angle = (float)T("AbsolMoveRules").GetField("SlashAngle").GetRawConstantValue();
                    Assert.That(radius, Is.EqualTo(scale.z).Within(0.001f));
                    Assert.That(halfAngle, Is.EqualTo(angle * 0.5f).Within(1f), "Authored border may slightly exceed the hit angle.");
                    Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(0));
                    Assert.That(bounds.center.x, Is.EqualTo(0).Within(0.001f));
                    continue;
                }
                Assert.That(bounds.min.z, Is.EqualTo(0).Within(0.001f));
                Assert.That(bounds.max.z, Is.EqualTo(scale.z).Within(0.001f));
                if (moveName == "AbsolFeint") Assert.That(mirror ? -bounds.min.x : bounds.max.x, Is.EqualTo(scale.x).Within(0.001f));
                else Assert.That(bounds.size.x, Is.EqualTo(scale.x).Within(0.001f));
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [Test]
    public void CurvedAimRetainsFeintAlternationAndNightSlashStraightRecast()
    {
        var feint = Activator.CreateInstance(T("AbsolFeint"));
        var mirrored = feint.GetType().GetProperty("MirrorsIndicator");
        Assert.That(mirrored.GetValue(feint), Is.False);
        feint.GetType().GetMethod("OnCommitted", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(feint, null);
        Assert.That(mirrored.GetValue(feint), Is.True);
        var night = Activator.CreateInstance(T("AbsolNightSlash"));
        Assert.That(night.GetType().GetProperty("MirrorsIndicator").GetValue(night), Is.True);
        night.GetType().GetMethod("OnExecutionState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(night, new object[] { false });
        Assert.That(night.GetType().GetProperty("IndicatorPath").GetValue(night), Is.Null);
    }

    [Test]
    public void BasicMoveBufferKeepsPressReleaseAndLatestIntentWithoutExtendingExpiry()
    {
        var buffer = Activator.CreateInstance(T("BufferedMoveInput"));
        var queue = buffer.GetType().GetMethod("Queue");
        Assert.That(queue.Invoke(buffer, new object[] { 0, false, 1f, 0.18f }), Is.True);
        Assert.That(queue.Invoke(buffer, new object[] { 1, false, 1.02f, 0.18f }), Is.True);
        Assert.That(queue.Invoke(buffer, new object[] { 0, true, 1.03f, 0.18f }), Is.False);
        Assert.That(buffer.GetType().GetProperty("Slot").GetValue(buffer), Is.EqualTo(1));
        Assert.That(queue.Invoke(buffer, new object[] { 1, true, 1.04f, 0.18f }), Is.True);
        Assert.That(buffer.GetType().GetProperty("Released").GetValue(buffer), Is.True);
        Assert.That(buffer.GetType().GetProperty("ExpiresAt").GetValue(buffer), Is.EqualTo(1.2f).Within(0.0001f));
        Assert.That(buffer.GetType().GetMethod("IsValid").Invoke(buffer, new object[] { 1.21f }), Is.False);
        Assert.That(queue.Invoke(buffer, new object[] { 1, true, 1.22f, 0.18f }), Is.False);
        buffer.GetType().GetMethod("Clear").Invoke(buffer, null);
        Assert.That(buffer.GetType().GetProperty("Pending").GetValue(buffer), Is.False);
        Assert.That(queue.Invoke(buffer, new object[] { 0, true, 2f, 0.18f }), Is.False, "A release alone must never invent a cast.");
    }

    [Test]
    public void BasicTimingAndBufferAreOptInAndBoostedBonusKeepsItsOwnFormula()
    {
        var attack = Activator.CreateInstance(T("AbsolBasicAtk"));
        Assert.That(attack.GetType().GetProperty("MoveInputBufferDuration").GetValue(attack), Is.EqualTo(T("AbsolMoveRules").GetField("BasicInputBuffer").GetRawConstantValue()));
        var legacy = Activator.CreateInstance(T("MarshadowBasicAtk"));
        Assert.That(legacy.GetType().GetProperty("MoveInputBufferDuration").GetValue(legacy), Is.EqualTo(0));
        Assert.That(Rule("BasicTimingScale", 0.93333f), Is.EqualTo(1));
        Assert.That((float)Rule("BasicTimingScale", 0.466665f), Is.EqualTo(0.5f).Within(0.0001f));
        var bonus = Rule("BoostedBonusDamage", 12ul);
        Assert.That(Field<float>(bonus, "ratio"), Is.EqualTo(0.375f));
        Assert.That(Field<short>(bonus, "slider"), Is.EqualTo(1));
        Assert.That(Field<short>(bonus, "baseDmg"), Is.EqualTo(20));
    }
}

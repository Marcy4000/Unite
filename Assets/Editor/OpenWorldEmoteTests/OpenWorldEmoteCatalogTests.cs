using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class OpenWorldEmoteCatalogTests
{
    private const string CatalogPath = "Assets/Animations/Lobby/OpenWorldEmotes.asset";
    private const string MalePath = "Assets/Model/Clothing/pref_char_lob_9001_male/MaleTrainer.controller";
    private const string FemalePath = "Assets/Model/Clothing/pref_char_lob_9002_female/TrainerModelFemale.controller";

    private ScriptableObject catalog;
    private Type catalogType;
    private MethodInfo genderLookup;
    private MethodInfo animatorLookup;
    private IList entries;

    [SetUp]
    public void LoadCatalog()
    {
        catalogType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly =>
            assembly.GetType("OpenWorldEmoteCatalog")).FirstOrDefault(type => type != null);
        Assert.That(catalogType, Is.Not.Null);
        catalog = AssetDatabase.LoadAssetAtPath(CatalogPath, catalogType) as ScriptableObject;
        Assert.That(catalog, Is.Not.Null);
        entries = (IList)catalogType.GetProperty("Entries").GetValue(catalog);
        genderLookup = catalogType.GetMethods().Single(method => method.Name == "TryGet" &&
            method.GetParameters()[1].ParameterType == typeof(bool));
        animatorLookup = catalogType.GetMethods().Single(method => method.Name == "TryGet" &&
            method.GetParameters()[1].ParameterType == typeof(Animator));
    }

    private static object Property(object entry, string name) => entry.GetType().GetProperty(name).GetValue(entry);

    private bool TryGet(MethodInfo method, Object source, int index, object support, out object entry)
    {
        var arguments = new[] { (object)index, support, null };
        var found = (bool)method.Invoke(source, arguments);
        entry = arguments[2];
        return found;
    }

    [Test]
    public void ConfiguredCatalogEntriesResolveToRealPlayableStates()
    {
        var expected = new Dictionary<string, AnimationClip>();
        foreach (var path in new[] { MalePath, FemalePath })
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Assert.That(controller.layers, Has.Length.EqualTo(1));
            var layer = controller.layers[0];
            Assert.That(layer.stateMachine.stateMachines, Is.Empty);
            foreach (var child in layer.stateMachine.states)
            {
                if (!(child.state.motion is AnimationClip clip) || clip.length <= 0f) continue;
                expected.Add(path + ":" + layer.name + "." + child.state.name, clip);
            }
        }

        var actual = new HashSet<string>();
        var serializedCatalog = new SerializedObject(catalog);
        int maleCount = serializedCatalog.FindProperty("maleAnimations").arraySize;
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var controllerPath = index < maleCount ? MalePath : FemalePath;
            var statePath = (string)Property(entry, "StatePath");
            var key = controllerPath + ":" + statePath;
            Assert.That(actual.Add(key), Is.True, key);
            Assert.That(expected.ContainsKey(key), Is.True, key);
            Assert.That(Property(entry, "Clip"), Is.EqualTo(expected[key]), key);
            Assert.That(Property(entry, "StateHash"), Is.EqualTo(Animator.StringToHash(statePath)), key);
            Assert.That(Property(entry, "Layer"), Is.EqualTo(0));
            Assert.That(Property(entry, "Duration"), Is.EqualTo(expected[key].length / (float)Property(entry, "Speed")).Within(0.0001f));
        }
        Assert.That(actual, Is.SubsetOf(expected.Keys));
        Assert.That(entries.Count, Is.GreaterThan(0));
    }

    [Test]
    public void HeadlessLookupFiltersGenderWithoutRemappingIndices()
    {
        var maleCount = 0;
        var femaleCount = 0;
        for (var index = 0; index < entries.Count; index++)
        {
            var male = TryGet(genderLookup, catalog, index, true, out var maleEntry);
            var female = TryGet(genderLookup, catalog, index, false, out var femaleEntry);
            Assert.That(male ^ female, Is.True, "Index " + index);
            Assert.That(male ? maleEntry : femaleEntry, Is.SameAs(entries[index]));
            Assert.That(male ? femaleEntry : maleEntry, Is.Null);
            if (male) maleCount++;
            else femaleCount++;
        }
        var serialized = new SerializedObject(catalog);
        Assert.That(maleCount, Is.EqualTo(serialized.FindProperty("maleAnimations").arraySize));
        Assert.That(femaleCount, Is.EqualTo(serialized.FindProperty("femaleAnimations").arraySize));
    }

    [Test]
    public void InvalidIndicesAndMissingAnimatorFailWithoutAnEntry()
    {
        foreach (var index in new[] { -1, entries.Count, int.MaxValue })
        {
            Assert.That(catalogType.GetMethod("IsValidIndex").Invoke(catalog, new object[] { index }), Is.False);
            Assert.That(TryGet(genderLookup, catalog, index, true, out var entry), Is.False);
            Assert.That(entry, Is.Null);
            Assert.That(TryGet(animatorLookup, catalog, index, null, out entry), Is.False);
            Assert.That(entry, Is.Null);
        }
        Assert.That(TryGet(animatorLookup, catalog, 0, null, out var missingAnimatorEntry), Is.False);
        Assert.That(missingAnimatorEntry, Is.Null);
    }

    [Test]
    public void FriendlyNamesAreEditableWithoutChangingStateIdentity()
    {
        var clone = Object.Instantiate(catalog);
        try
        {
            var serialized = new SerializedObject(clone);
            var first = serialized.FindProperty("maleAnimations").GetArrayElementAtIndex(0);
            var statePath = first.FindPropertyRelative("statePath").stringValue;
            var clip = first.FindPropertyRelative("clip").objectReferenceValue;
            first.FindPropertyRelative("displayName").stringValue = "My friendly emote";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(TryGet(genderLookup, clone, 0, true, out var entry), Is.True);
            Assert.That(Property(entry, "DisplayName"), Is.EqualTo("My friendly emote"));
            Assert.That(Property(entry, "StatePath"), Is.EqualTo(statePath));
            Assert.That(Property(entry, "Clip"), Is.EqualTo(clip));
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ResetDisplayNamesOnlyRenumbersSelectedListAndSupportsUndo(bool isMale)
    {
        var clone = Object.Instantiate(catalog);
        try
        {
            var data = new SerializedObject(clone);
            foreach (string listName in new[] { "maleAnimations", "femaleAnimations" })
                data.FindProperty(listName).GetArrayElementAtIndex(0).FindPropertyRelative("displayName").stringValue = "Custom " + listName;
            data.ApplyModifiedPropertiesWithoutUndo();
            string before = EditorJsonUtility.ToJson(clone);
            var previousEntries = ((IList)catalogType.GetProperty("Entries").GetValue(clone)).Cast<object>()
                .Select(entry => new { Name = Property(entry, "AnimationName"), Speed = Property(entry, "Speed"), Clip = Property(entry, "Clip"), Path = Property(entry, "StatePath") }).ToArray();
            var editorType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("OpenWorldEmoteCatalogEditor"))
                .First(type => type != null);
            Undo.IncrementCurrentGroup();
            editorType.GetMethod("ResetDisplayNames").Invoke(null, new object[] { clone, isMale });
            data.Update();
            var selected = data.FindProperty(isMale ? "maleAnimations" : "femaleAnimations");
            var other = data.FindProperty(isMale ? "femaleAnimations" : "maleAnimations");
            for (int index = 0; index < selected.arraySize; index++)
                Assert.That(selected.GetArrayElementAtIndex(index).FindPropertyRelative("displayName").stringValue, Is.EqualTo("Emote " + (index + 1)));
            Assert.That(other.GetArrayElementAtIndex(0).FindPropertyRelative("displayName").stringValue,
                Is.EqualTo("Custom " + (isMale ? "femaleAnimations" : "maleAnimations")));
            var updatedEntries = ((IList)catalogType.GetProperty("Entries").GetValue(clone)).Cast<object>()
                .Select(entry => new { Name = Property(entry, "AnimationName"), Speed = Property(entry, "Speed"), Clip = Property(entry, "Clip"), Path = Property(entry, "StatePath") }).ToArray();
            Assert.That(updatedEntries, Is.EqualTo(previousEntries));
            Undo.PerformUndo();
            Assert.That(EditorJsonUtility.ToJson(clone), Is.EqualTo(before));
        }
        finally
        {
            Undo.ClearUndo(clone);
            Object.DestroyImmediate(clone);
        }
    }

    [TestCase("clip")]
    [TestCase("statePath")]
    [TestCase("layer")]
    [TestCase("stateSpeed")]
    [TestCase("animationName")]
    [TestCase("speed")]
    public void InvalidAuthoredEntriesAreRejected(string field)
    {
        var clone = Object.Instantiate(catalog);
        try
        {
            var serialized = new SerializedObject(clone);
            var property = serialized.FindProperty("maleAnimations").GetArrayElementAtIndex(0).FindPropertyRelative(field);
            if (field == "clip") property.objectReferenceValue = null;
            else if (field == "statePath" || field == "animationName") property.stringValue = string.Empty;
            else if (field == "layer") property.intValue = -1;
            else property.floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(TryGet(genderLookup, clone, 0, true, out var entry), Is.False);
            Assert.That(entry, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    [Test]
    public void DurationAccountsForStateSpeedAndEmptyClipsAreRejected()
    {
        var clone = Object.Instantiate(catalog);
        var emptyClip = new AnimationClip();
        try
        {
            var serialized = new SerializedObject(clone);
            var first = serialized.FindProperty("maleAnimations").GetArrayElementAtIndex(0);
            first.FindPropertyRelative("speed").floatValue = 2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(TryGet(genderLookup, clone, 0, true, out var entry), Is.True);
            Assert.That(Property(entry, "Duration"), Is.EqualTo(((AnimationClip)Property(entry, "Clip")).length / 2f));
            first.FindPropertyRelative("clip").objectReferenceValue = emptyClip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(TryGet(genderLookup, clone, 0, true, out entry), Is.False);
            Assert.That(entry, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(clone);
            Object.DestroyImmediate(emptyClip);
        }
    }

    [Test]
    public void InspectorExposesTwoListsAndOnlyNameFriendlyNameAndSpeedPerEntry()
    {
        var serialized = new SerializedObject(catalog);
        Assert.That(serialized.FindProperty("entries"), Is.Null);
        Assert.That(serialized.FindProperty("maleAnimations").isArray, Is.True);
        Assert.That(serialized.FindProperty("femaleAnimations").isArray, Is.True);
        foreach (var list in new[] { "maleAnimations", "femaleAnimations" })
        {
            var property = serialized.FindProperty(list).GetArrayElementAtIndex(0);
            var end = property.GetEndProperty();
            var names = new List<string>();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren) && !SerializedProperty.EqualContents(property, end))
            {
                names.Add(property.name);
                enterChildren = false;
            }
            Assert.That(names, Is.EqualTo(new[] { "animationName", "displayName", "speed" }));
        }
    }

    [Test]
    public void AnimationNamesResolveClipAliasesAndControllersAutomatically()
    {
        var clone = Object.Instantiate(catalog);
        try
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MalePath);
            var clip = (AnimationClip)controller.layers[0].stateMachine.states.Select(child => child.state)
                .First(state => state.motion is AnimationClip candidate && candidate.name != state.name).motion;
            var serialized = new SerializedObject(clone);
            var first = serialized.FindProperty("maleAnimations").GetArrayElementAtIndex(0);
            first.FindPropertyRelative("animationName").stringValue = clip.name;
            first.FindPropertyRelative("displayName").stringValue = "Friendly animation";
            first.FindPropertyRelative("speed").floatValue = 3f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            catalogType.GetMethod("ResolveAnimations").Invoke(clone, null);
            Assert.That(TryGet(genderLookup, clone, 0, true, out var resolved), Is.True);
            var resolvedClip = (AnimationClip)Property(resolved, "Clip");
            Assert.That(resolvedClip.name, Is.EqualTo(clip.name));
            Assert.That(Property(resolved, "DisplayName"), Is.EqualTo("Friendly animation"));
            Assert.That(Property(resolved, "Speed"), Is.EqualTo(3f));
            Assert.That(Property(resolved, "Duration"), Is.EqualTo(resolvedClip.length / 3f).Within(0.0001f));
            Assert.That(TryGet(genderLookup, clone, 0, false, out _), Is.False);
        }
        finally { Object.DestroyImmediate(clone); }
    }

    [Test]
    public void UnknownAnimationNamesCannotReuseAnOldResolvedState()
    {
        var clone = Object.Instantiate(catalog);
        try
        {
            var serialized = new SerializedObject(clone);
            serialized.FindProperty("maleAnimations").GetArrayElementAtIndex(0)
                .FindPropertyRelative("animationName").stringValue = "Missing animation";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(TryGet(genderLookup, clone, 0, true, out _), Is.False);
            catalogType.GetMethod("ResolveAnimations").Invoke(clone, null);
            Assert.That(TryGet(genderLookup, clone, 0, true, out _), Is.False);
            Assert.That(Property(((IList)catalogType.GetProperty("Entries").GetValue(clone))[0], "Clip"), Is.Null);
        }
        finally { Object.DestroyImmediate(clone); }
    }

    [UnityTest]
    public IEnumerator EditingAnimationNameResolvesWithoutManualSetup()
    {
        var clone = Object.Instantiate(catalog);
        try
        {
            var serialized = new SerializedObject(clone);
            serialized.FindProperty("maleAnimations").GetArrayElementAtIndex(0).FindPropertyRelative("animationName").stringValue =
                (string)Property(entries[1], "AnimationName");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!TryGet(genderLookup, clone, 0, true, out _) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(TryGet(genderLookup, clone, 0, true, out var resolved), Is.True);
            Assert.That(Property(resolved, "Clip"), Is.EqualTo(Property(entries[1], "Clip")));
            Assert.That(Property(resolved, "StatePath"), Is.EqualTo(Property(entries[1], "StatePath")));
        }
        finally { Object.DestroyImmediate(clone); }
    }

    [TestCase(true, MalePath)]
    [TestCase(false, FemalePath)]
    public void AnimatorLookupMatchesHeadlessLookupAndRejectsOtherControllers(bool isMale, string controllerPath)
    {
        var root = new GameObject("Emote catalog test animator", typeof(Animator));
        var overrideController = new AnimatorOverrideController(AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath));
        try
        {
            root.hideFlags = HideFlags.HideAndDontSave;
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            animator.Rebind();
            animator.Update(0f);
            var firstSupported = -1;
            for (var index = 0; index < entries.Count; index++)
            {
                var expected = TryGet(genderLookup, catalog, index, isMale, out var expectedEntry);
                Assert.That(TryGet(animatorLookup, catalog, index, animator, out var entry), Is.EqualTo(expected), "Index " + index);
                Assert.That(entry, Is.SameAs(expectedEntry));
                if (expected && firstSupported < 0) firstSupported = index;
            }
            animator.runtimeAnimatorController = overrideController;
            animator.Rebind();
            animator.Update(0f);
            Assert.That(TryGet(animatorLookup, catalog, firstSupported, animator, out var unsupported), Is.False);
            Assert.That(unsupported, Is.Null);
            animator.runtimeAnimatorController = null;
            Assert.That(TryGet(animatorLookup, catalog, firstSupported, animator, out unsupported), Is.False);
            Assert.That(unsupported, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(overrideController);
        }
    }

    [TestCase(MalePath, "ani_idle_40350_lob_male")]
    [TestCase(FemalePath, "ani_idle_lob_female")]
    public void ResetAnimIsAnImmediateSmoothAnyStateTransitionToNormalIdle(string controllerPath, string idleState)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        var parameter = controller.parameters.Single(candidate => candidate.name == "ResetAnim");
        Assert.That(parameter.type, Is.EqualTo(AnimatorControllerParameterType.Trigger));
        var stateMachine = controller.layers[0].stateMachine;
        var transition = stateMachine.anyStateTransitions.Single(candidate =>
            candidate.conditions.Any(condition => condition.parameter == "ResetAnim"));
        Assert.That(transition.destinationState.name, Is.EqualTo(idleState));
        Assert.That(transition.destinationState, Is.EqualTo(stateMachine.defaultState));
        Assert.That(transition.conditions, Has.Length.EqualTo(1));
        Assert.That(transition.conditions[0].mode, Is.EqualTo(AnimatorConditionMode.If));
        Assert.That(transition.hasExitTime, Is.False);
        Assert.That(transition.hasFixedDuration, Is.True);
        Assert.That(transition.duration, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(transition.offset, Is.Zero);
        Assert.That(transition.canTransitionToSelf, Is.False);
        Assert.That(transition.mute, Is.False);
        Assert.That(transition.isExit, Is.False);
    }
}

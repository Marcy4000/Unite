using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

public class TrainerClothingPlayModeTests
{
    private GameObject root;
    private Component model;
    private object info;
    private object previousClothesList;
    private Type clothesType;
    private readonly List<Object> assets = new List<Object>();

    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Get(object target, string field) => target.GetType()
        .GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private void Initialize() => model.GetType().GetMethod("InitializeClothes").Invoke(model, new[] { info });

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if !UNITY_EDITOR
        Assert.Ignore("These integration tests use authored clothing assets through Editor Asset Database play mode.");
        yield break;
#else
        var settingsType = Type.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor", true);
        var settings = settingsType.GetProperty("Settings").GetValue(null);
        var builder = Property(settings, "ActivePlayModeDataBuilder");
        if (builder.GetType().Name != "BuildScriptFastMode")
            Assert.Ignore("Run these tests with Addressables Play Mode Script set to Use Asset Database.");

        clothesType = GameType("ClothesList");
        previousClothesList = clothesType.GetField("Instance").GetValue(null);
        root = new GameObject("Clothing integration tests");
        var clothes = root.AddComponent(clothesType);
        clothesType.GetField("Instance").SetValue(null, clothes);

        var hairGuids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Model/Clothing/Male/Hair" })
            .Where(guid => AssetDatabase.GUIDToAssetPath(guid).EndsWith(".fbx"))
            .OrderBy(guid => guid).Take(2).ToArray();
        var shirtGuid = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Model/Clothing/Male/Top" })
            .First(guid => AssetDatabase.GUIDToAssetPath(guid).EndsWith(".fbx"));
        Assert.That(hairGuids.Length, Is.EqualTo(2));

        var listType = GameType("ClothingTypeList");
        var itemType = GameType("ClothingItem");
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(listType));
        for (int slot = 0; slot < 11; slot++)
        {
            var group = Activator.CreateInstance(listType);
            Set(group, "clothingType", Enum.ToObject(GameType("ClothingType"), slot));
            var entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
            string[] keys = slot == 1 ? hairGuids : slot == 4 ? new[] { "", shirtGuid } : new[] { "" };
            foreach (string key in keys)
            {
                var item = ScriptableObject.CreateInstance(itemType);
                assets.Add(item);
                Set(item, "clothingType", Enum.ToObject(GameType("ClothingType"), slot));
                if (key != "") ((IList)Get(item, "prefabs")).Add(new AssetReferenceGameObject(key));
                entries.Add(item);
            }
            Set(group, "clothingItems", entries);
            list.Add(group);
        }
        Set(clothes, "maleClothes", list);
        Set(clothes, "femaleClothes", list);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TrainerModels/TrainerModel.prefab");
        Assert.That(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(renderer => renderer.enabled), Is.True,
            "The editor placement preview must remain intact in the authored prefab.");
        var trainer = Object.Instantiate(prefab, root.transform);
        model = trainer.GetComponent(GameType("TrainerModel"));
        Assert.That(trainer.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(renderer => renderer.enabled), Is.False,
            "Awake must hide debug clothes immediately, before the first frame or appearance request.");
        info = Activator.CreateInstance(GameType("PlayerClothesInfo"));
        info.GetType().GetProperty("IsMale").SetValue(info, true);
        yield return null;
#endif
    }

    private IEnumerator WaitForInitialization()
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while ((bool)Property(model, "IsLoading") && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That((bool)Property(model, "IsLoading"), Is.False, "Trainer load timed out.");
        Assert.That((bool)Property(model, "IsInitialized"), Is.True);
    }

    private Transform Holder(int slot, bool male = true)
        => ((Transform[])Get(model, male ? "clothingHoldersMale" : "clothingHoldersFemale"))[slot];

    [UnityTest]
    public IEnumerator AddingOneSlotKeepsExistingGarmentAndItsLease()
    {
        Initialize();
        yield return WaitForInitialization();
        var hair = Holder(1).GetChild(0);
        var owned = hair.GetComponent(GameType("TrainerClothingInstance"));
        var handle = (AsyncOperationHandle<GameObject>)Get(owned, "prefabHandle");
        Set(info, "Shirt", (ushort)1);
        Initialize();
        Assert.That(Holder(1).GetChild(0), Is.SameAs(hair), "Existing outfit must stay visible during loading.");
        Assert.That(((GameObject)Get(model, "loadingText")).activeSelf, Is.False);
        yield return null;
        Assert.That(((GameObject)Get(model, "loadingText")).activeSelf, Is.False,
            "A background outfit change must not show the loading text.");
        yield return WaitForInitialization();
        Assert.That(Holder(1).GetChild(0), Is.SameAs(hair));
        Assert.That(handle.IsValid(), Is.True);
        Assert.That(Holder(4).childCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator FirstAppearanceShowsLoadingTextButNotDebugClothes()
    {
        var text = (GameObject)Get(model, "loadingText");
        Assert.That(text.activeSelf, Is.False, "No loading request has started yet.");
        Initialize();
        Assert.That(text.activeSelf, Is.True);
        Assert.That(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(renderer => renderer.enabled), Is.False);
        yield return WaitForInitialization();
        Assert.That(text.activeSelf, Is.False);
        Assert.That(Holder(1).GetChild(0).GetComponentInChildren<SkinnedMeshRenderer>().enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator ReenablingAnInitializedTrainerKeepsItsOutfitWithoutLoadingText()
    {
        Initialize();
        yield return WaitForInitialization();
        var hair = Holder(1).GetChild(0);
        model.gameObject.SetActive(false);
        model.gameObject.SetActive(true);
        Assert.That((bool)Property(model, "IsLoading"), Is.False);
        Assert.That(((GameObject)Get(model, "loadingText")).activeSelf, Is.False);
        Assert.That(hair.GetComponentInChildren<SkinnedMeshRenderer>().enabled, Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator NewRequestDuringLoadingWinsWithoutPublishingStaleOutfit()
    {
        int completions = 0;
        model.GetType().GetField("onClothesInitialized").SetValue(model, (Action)(() => completions++));
        Initialize();
        yield return null;
        info.GetType().GetProperty("Hair").SetValue(info, (byte)1);
        Initialize();
        yield return WaitForInitialization();
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(Property(Property(model, "PlayerClothesInfo"), "Hair"), Is.EqualTo((byte)1));
        Assert.That(Holder(1).childCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator DisableDuringLoadingReleasesPendingHandlesAndResumes()
    {
        Initialize();
        yield return null;
        var pending = (IList)Get(model, "pendingLoads");
        model.gameObject.SetActive(false);
        Assert.That(pending.Count, Is.Zero);
        model.gameObject.SetActive(true);
        yield return WaitForInitialization();
        Assert.That(Holder(1).childCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator TrainerCardMetadataChangesDoNotGetOverwrittenByAnOlderRequest()
    {
        Initialize();
        yield return null;
        var card = Get(info, "TrainerCardInfo");
        card.GetType().GetProperty("TrainerAnimation").SetValue(card, (byte)3);
        Set(info, "TrainerCardInfo", card);
        Initialize();
        yield return WaitForInitialization();
        var applied = Property(model, "PlayerClothesInfo");
        Assert.That(Property(Get(applied, "TrainerCardInfo"), "TrainerAnimation"), Is.EqualTo((byte)3));
    }

    [UnityTest]
    public IEnumerator OrdinaryTrainerCloneRetainsPrefabAfterSourceIsDestroyed()
    {
        Initialize();
        yield return WaitForInitialization();
        var sourceOwned = Holder(1).GetChild(0).GetComponent(GameType("TrainerClothingInstance"));
        var clone = Object.Instantiate(model.gameObject, root.transform);
        var owned = clone.GetComponentInChildren(GameType("TrainerClothingInstance"), true);
        var cloneModel = clone.GetComponent(GameType("TrainerModel"));
        Assert.That(((GameObject)Get(cloneModel, "loadingText")).activeSelf, Is.False);
        Assert.That(owned.GetComponentInChildren<SkinnedMeshRenderer>().enabled, Is.True,
            "A loaded trainer clone must not be mistaken for an editor preview.");
        var sourceMaterials = (IList)Get(sourceOwned, "ownedMaterials");
        var cloneMaterials = (IList)Get(owned, "ownedMaterials");
        Assert.That(sourceMaterials.Count, Is.GreaterThan(0));
        Assert.That(cloneMaterials.Count, Is.EqualTo(sourceMaterials.Count));
        var sourceMaterial = (Material)sourceMaterials[0];
        var cloneMaterial = (Material)cloneMaterials[0];
        Assert.That(cloneMaterial, Is.Not.SameAs(sourceMaterial));
        Object.Destroy(model.gameObject);
        yield return null;
        yield return null;
        var handle = (AsyncOperationHandle<GameObject>)Get(owned, "prefabHandle");
        Assert.That(handle.IsValid(), Is.True);
        Assert.That(handle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
        Assert.That(handle.Result != null, Is.True);
        Assert.That(cloneMaterial != null, Is.True);
    }

    [UnityTest]
    public IEnumerator GenderSwitchRetiresPreviousGenderOnlyAfterReplacementIsReady()
    {
        Initialize();
        yield return WaitForInitialization();
        info.GetType().GetProperty("IsMale").SetValue(info, false);
        Initialize();
        Assert.That(Holder(1).childCount, Is.EqualTo(1));
        yield return WaitForInitialization();
        Assert.That(Holder(1).childCount, Is.Zero);
        Assert.That(Holder(1, false).childCount, Is.EqualTo(1));
        Assert.That((bool)Property(model, "IsMale"), Is.False);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (clothesType != null) clothesType.GetField("Instance").SetValue(null, previousClothesList);
        if (root != null) Object.Destroy(root);
        foreach (var asset in assets) Object.Destroy(asset);
        assets.Clear();
        yield return null;
        yield return null;
    }
}

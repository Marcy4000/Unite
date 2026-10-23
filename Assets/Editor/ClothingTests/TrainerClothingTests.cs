using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class TrainerClothingTests
{
    private readonly List<Object> created = new List<Object>();
    private Type itemType;
    private Type infoType;
    private Type resolverType;
    private Array items;
    private object info;
    private int nextKey;

    // Runtime scripts live in Assembly-CSharp, which an asmdef cannot reference.
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static object Get(object target, string field) => target.GetType()
        .GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

    [SetUp]
    public void SetUp()
    {
        itemType = GameType("ClothingItem");
        infoType = GameType("PlayerClothesInfo");
        resolverType = GameType("TrainerOutfitResolver");
        items = Array.CreateInstance(itemType, 11);
        info = Activator.CreateInstance(infoType);
    }

    private ScriptableObject Item(int slot, int variants = 1, params int[] disables)
    {
        var item = ScriptableObject.CreateInstance(itemType);
        created.Add(item);
        Set(item, "clothingType", Enum.ToObject(GameType("ClothingType"), slot));
        var prefabs = (IList)Get(item, "prefabs");
        for (int i = 0; i < variants; i++)
            prefabs.Add(new AssetReferenceGameObject((++nextKey).ToString("x32")));
        var exclusions = (IList)Get(item, "disablesClothingType");
        foreach (int type in disables) exclusions.Add(Enum.ToObject(GameType("ClothingType"), type));
        items.SetValue(item, slot);
        return item;
    }

    private Array Resolve() => (Array)resolverType.GetMethod("ResolveItems").Invoke(null, new[] { items, info });
    private static object SelectedItem(Array result, int slot) => Get(result.GetValue(slot), "Item");
    private static int Variant(Array result, int slot) => (int)Get(result.GetValue(slot), "ModelIndex");

    private GameObject Root(string name)
    {
        var root = new GameObject(name);
        root.SetActive(false);
        created.Add(root);
        return root;
    }

    [TestCase(0, 1)]
    [TestCase(1, 0)]
    public void ShirtVariantDependsOnOverwear(int overwear, int expected)
    {
        Item(4, 2);
        Set(info, "Overwear", (ushort)overwear);
        Assert.That(Variant(Resolve(), 4), Is.EqualTo(expected));
    }

    [Test]
    public void CoveredPantsAreNotLoadedAndSelectFullSockVariant()
    {
        Item(4, 1, 7);
        var pants = Item(7);
        Set(pants, "sockTypeToUse", 0);
        Item(8, 3);
        var result = Resolve();
        Assert.That(SelectedItem(result, 7), Is.Null);
        Assert.That(Variant(result, 8), Is.EqualTo(2));
    }

    [Test]
    public void SocksFollowPantsAndClampToAvailableVariants()
    {
        var pants = Item(7);
        Item(8, 2);
        Set(pants, "sockTypeToUse", 0);
        Assert.That(Variant(Resolve(), 8), Is.Zero);
        Set(pants, "sockTypeToUse", 20);
        Assert.That(Variant(Resolve(), 8), Is.EqualTo(1));
        Set(pants, "sockTypeToUse", -1);
        Assert.That(Variant(Resolve(), 8), Is.Zero);
    }

    [Test]
    public void ExclusionsRetainOriginalUnionSemantics()
    {
        Item(0, 1, 1); // Hat hides hair.
        Item(1, 1, 6); // Selected hair also hides gloves, even when hidden.
        Item(6);
        var result = Resolve();
        Assert.That(SelectedItem(result, 0), Is.Not.Null);
        Assert.That(SelectedItem(result, 1), Is.Null);
        Assert.That(SelectedItem(result, 6), Is.Null);
    }

    [Test]
    public void EmptyOrInvalidItemsDoNotLoadOrHideOtherSlots()
    {
        Item(0, 0, 1);
        Item(1);
        var shirt = Item(4);
        ((IList)Get(shirt, "prefabs"))[0] = new AssetReferenceGameObject("");
        var result = Resolve();
        Assert.That(SelectedItem(result, 0), Is.Null);
        Assert.That(SelectedItem(result, 1), Is.Not.Null);
        Assert.That(SelectedItem(result, 4), Is.Null);
    }

    [Test]
    public void EyesUseFaceIndexAndInvalidIndicesReturnNoItem()
    {
        var clothes = Root("Index validation").AddComponent(GameType("ClothesList"));
        var listType = GameType("ClothingTypeList");
        var group = Activator.CreateInstance(listType);
        Set(group, "clothingType", Enum.ToObject(GameType("ClothingType"), 3));
        var entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
        var eye0 = Item(3);
        var eye1 = Item(3);
        entries.Add(eye0);
        entries.Add(eye1);
        Set(group, "clothingItems", entries);
        var groups = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(listType));
        groups.Add(group);
        Set(clothes, "femaleClothes", groups);
        infoType.GetProperty("Face").SetValue(info, (byte)1);
        Set(info, "Eyes", (ushort)0);
        var result = (Array)resolverType.GetMethod("Resolve").Invoke(null, new[] { clothes, info, (object)11 });
        Assert.That(SelectedItem(result, 3), Is.SameAs(eye1));
        Assert.That(Call(clothes, "GetClothingItem", Enum.ToObject(GameType("ClothingType"), 3), -1, false), Is.Null);
        Assert.That(Call(clothes, "GetClothingItem", Enum.ToObject(GameType("ClothingType"), 3), 20, false), Is.Null);
    }

    [Test]
    public void LatestRequestIsNotDroppedDuringInitialization()
    {
        var model = Root("Queued trainer").AddComponent(GameType("TrainerModel"));
        Set(model, "initializing", true);
        Set(info, "Shirt", (ushort)7);
        Call(model, "InitializeClothes", info);
        Assert.That(Get(model, "hasPendingRequest"), Is.True);
        Assert.That(Get(Get(model, "requestedInfo"), "Shirt"), Is.EqualTo((ushort)7));
        Set(info, "Shirt", (ushort)9);
        Call(model, "InitializeClothes", info);
        Assert.That(Get(Get(model, "requestedInfo"), "Shirt"), Is.EqualTo((ushort)9));
    }

    [Test]
    public void UnchangedGarmentIsReusedWithoutAnAssetRequest()
    {
        var root = Root("Reusable trainer");
        var model = root.AddComponent(GameType("TrainerModel"));
        var holders = new Transform[11];
        for (int i = 0; i < holders.Length; i++)
        {
            var holder = new GameObject("Slot " + i);
            holder.transform.SetParent(root.transform);
            holders[i] = holder.transform;
        }
        Set(model, "clothingHoldersFemale", holders);
        Set(model, "clothingHoldersMale", new Transform[0]);
        Item(1);
        var outfit = Resolve();
        string key = (string)Get(outfit.GetValue(1), "RuntimeKey");
        var garment = new GameObject("Existing hair");
        garment.transform.SetParent(holders[1]);
        var owned = garment.AddComponent(GameType("TrainerClothingInstance"));
        Call(owned, "Initialize", key, default(AsyncOperationHandle<GameObject>));

        Assert.That(Call(model, "StartLoads", holders, outfit), Is.True);
        Assert.That(((IList)Get(model, "pendingLoads")).Count, Is.Zero);
        Assert.That(Call(model, "TryInstantiateAndCommit", holders, outfit, info), Is.True);
        Assert.That(holders[1].GetChild(0).gameObject, Is.SameAs(garment));

        // A failed replacement must not retire an already visible garment.
        Item(4);
        outfit = Resolve();
        var pendingType = model.GetType().GetNestedType("PendingClothing", BindingFlags.NonPublic);
        var pending = Activator.CreateInstance(pendingType);
        var handle = Addressables.ResourceManager.CreateCompletedOperation<GameObject>(null, null);
        Set(pending, "Selection", outfit.GetValue(4));
        Set(pending, "Handle", handle);
        ((IList)Get(model, "pendingLoads")).Add(pending);
        LogAssert.Expect(LogType.Error, new Regex("Failed to load clothing"));
        Assert.That(Call(model, "TryInstantiateAndCommit", holders, outfit, info), Is.False);
        Assert.That(holders[1].GetChild(0).gameObject, Is.SameAs(garment));
        Call(model, "ReleasePendingLoads");
        Assert.That(handle.IsValid(), Is.False);
    }

    [Test]
    public void AbandonedPendingLoadIsReleasedExactlyOnce()
    {
        var model = Root("Cancelled trainer").AddComponent(GameType("TrainerModel"));
        var handle = Addressables.ResourceManager.CreateCompletedOperation<GameObject>(null, null);
        var pendingType = model.GetType().GetNestedType("PendingClothing", BindingFlags.NonPublic);
        var pending = Activator.CreateInstance(pendingType);
        Set(pending, "Handle", handle);
        ((IList)Get(model, "pendingLoads")).Add(pending);
        Call(model, "ReleasePendingLoads");
        Assert.That(handle.IsValid(), Is.False);
        Assert.That(((IList)Get(model, "pendingLoads")).Count, Is.Zero);
        Assert.DoesNotThrow(() => Call(model, "ReleasePendingLoads"));
    }

    [Test]
    public void CustomMaterialsAreCachedAndNonCustomMaterialsRemainShared()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Trainer_Hair.shader");
        var hair = new Material(shader) { name = "hair" };
        var shoes = new Material(shader) { name = "shoes" };
        created.Add(hair);
        created.Add(shoes);
        var root = Root("Material ownership");
        var renderer = root.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMaterials = new[] { hair, shoes };
        var owned = root.AddComponent(GameType("TrainerClothingInstance"));
        Call(owned, "ApplyColors", info, Color.white, Color.black);
        var customized = renderer.sharedMaterials[0];
        Assert.That(customized, Is.Not.SameAs(hair));
        Assert.That(renderer.sharedMaterials[1], Is.SameAs(shoes));
        Call(owned, "ApplyColors", info, Color.white, Color.black);
        Assert.That(renderer.sharedMaterials[0], Is.SameAs(customized));
        Call(owned, "ReleaseResources");
        Assert.That(customized == null, Is.True);
        Assert.That(hair != null, Is.True);
    }

    [Test]
    public void SharedPartitionsAreStableAndDependOnModelSetNotOrdering()
    {
        var method = GameType("ClothingAddressablesOptimizer").GetMethod("GetSharedLabel");
        object Label(params string[] keys) => method.Invoke(null, new object[] { keys });
        Assert.That(Label("a", "b"), Is.EqualTo(Label("b", "a", "a")));
        Assert.That(Label("a", "b"), Is.Not.EqualTo(Label("a", "c")));
    }

    [Test]
    public void MigratedModelGroupContainsNoFoldersOrSourceJson()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var models = settings.FindGroup("ClothingItems");
        Assert.That(models, Is.Not.Null);
        Assert.That(models.entries.Any(entry => AssetDatabase.IsValidFolder(entry.AssetPath)), Is.False);
        Assert.That(models.entries.Any(entry => entry.AssetPath.EndsWith(".json")), Is.False);
        Assert.That(models.entries.Any(entry => entry.AssetPath.EndsWith(".png")), Is.False);
        Assert.That(models.GetSchema<BundledAssetGroupSchema>().BundleMode,
            Is.EqualTo(BundledAssetGroupSchema.BundlePackingMode.PackSeparately));
        Assert.That(settings.FindGroup("ClothingIcons").GetSchema<BundledAssetGroupSchema>().BundleMode,
            Is.EqualTo(BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var gameObject in created.OfType<GameObject>())
        {
            if (gameObject == null) continue;
            foreach (var owned in gameObject.GetComponentsInChildren(GameType("TrainerClothingInstance"), true))
                Call(owned, "ReleaseResources");
        }
        foreach (var asset in created)
        {
            if (asset != null) Object.DestroyImmediate(asset);
        }
        created.Clear();
    }
}

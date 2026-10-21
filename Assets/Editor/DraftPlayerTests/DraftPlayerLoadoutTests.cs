using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unite.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Multiplayer;
using Object = UnityEngine.Object;

public class DraftPlayerLoadoutTests
{
    private Scene previewScene;
    private GameObject root;
    private Component icon;
    private GameSessionPlayer player;
    private PropertyInfo charactersInstance;
    private object previousCharacters;
    private Type tweenType;
    private bool tweensWereInitialized;
    private readonly List<Object> createdAssets = new List<Object>();
    private Sprite[] itemSprites;
    private Sprite battleItemSprite;

    // Gameplay scripts are in Assembly-CSharp, which cannot be referenced by an asmdef.
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Field(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void SetField(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private void Call(string method, params object[] args) => icon.GetType().GetMethod(method).Invoke(icon, args);

    private Sprite NewSprite()
    {
        var texture = new Texture2D(1, 1);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        createdAssets.Add(sprite);
        createdAssets.Add(texture);
        return sprite;
    }

    private Array Items(string typeName, Sprite[] sprites)
    {
        var type = GameType(typeName);
        var items = Array.CreateInstance(type, sprites.Length);
        for (var index = 0; index < sprites.Length; index++)
        {
            var item = ScriptableObject.CreateInstance(type);
            type.GetField("icon").SetValue(item, sprites[index]);
            createdAssets.Add(item);
            items.SetValue(item, index);
        }
        return items;
    }

    [SetUp]
    public void SetUp()
    {
        tweenType = GameType("DG.Tweening.DOTween");
        tweensWereInitialized = (Object)tweenType.GetField("instance").GetValue(null) != null;
        previewScene = EditorSceneManager.NewPreviewScene();
        root = new GameObject("Draft loadout test");
        root.SetActive(false); // No authentication, network startup, or Start callbacks.
        SceneManager.MoveGameObjectToScene(root, previewScene);

        var charactersType = GameType("CharactersList");
        charactersInstance = charactersType.GetProperty("Instance");
        previousCharacters = charactersInstance.GetValue(null);
        var characters = root.AddComponent(charactersType);
        charactersInstance.SetValue(null, characters);
        itemSprites = Enumerable.Range(0, 4).Select(index => NewSprite()).ToArray();
        battleItemSprite = NewSprite();
        SetField(characters, "heldItems", Items("HeldItemInfo", itemSprites));
        SetField(characters, "battleItems", Items("BattleItemAsset", new[] { NewSprite(), battleItemSprite }));

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CharacterSelect/DraftMode/DraftPlayerBlue.prefab");
        var instance = Object.Instantiate(prefab, root.transform);
        icon = instance.GetComponent(GameType("DraftPlayerIcon"));
        player = new GameSessionPlayer("draft-player");
        player.Data["PlayerName"] = new PlayerProperty("Draft player");
        player.Data["ClothingInfo"] = new PlayerProperty("AAAAAAAAAAAASjMJcj8FAAAAAAJqASkBZwA=");
        player.Data["SelectedCharacter"] = new PlayerProperty("//8="); // short -1
        player.Data["BattleItem"] = new PlayerProperty("1");
        player.Data["HeldItems"] = new PlayerProperty(Convert.ToBase64String(new byte[] { 1, 2, 3 }));
        Call("Initialize", player, false);
    }

    private void AssertHeldItems(params int[] expected)
    {
        var holder = Field(icon, "heldItemsHolder");
        var images = (IList)Field(holder, "icons");
        for (var index = 0; index < expected.Length; index++)
        {
            var sprite = images[index].GetType().GetProperty("sprite").GetValue(images[index]);
            Assert.That(sprite, Is.SameAs(itemSprites[expected[index]]), $"Held-item slot {index}");
        }
    }

    [Test]
    public void InitialSnapshotPopulatesLoadoutWithoutWaitingForSessionEvents()
    {
        AssertHeldItems(1, 2, 3);
        var image = Field(icon, "battleItemSprite");
        Assert.That(image.GetType().GetProperty("sprite").GetValue(image), Is.SameAs(battleItemSprite));
        Assert.That(((Component)Field(icon, "heldItemsHolder")).gameObject.activeSelf, Is.False,
            "Initializing the loadout must not reveal it before the pick is confirmed.");
    }

    [Test]
    public void ConfirmingPickDisplaysTheAlreadyPopulatedHeldItems()
    {
        Call("UpdateIconState", Enum.Parse(GameType("DraftPlayerState"), "Confirmed"));
        Assert.That(((Component)Field(icon, "heldItemsHolder")).gameObject.activeSelf, Is.True);
        AssertHeldItems(1, 2, 3);
    }

    [Test]
    public void SessionUpdatesRefreshItemsWithoutOverridingDraftCharacter()
    {
        var character = ScriptableObject.CreateInstance(GameType("CharacterInfo"));
        createdAssets.Add(character);
        character.GetType().GetField("icon").SetValue(character, NewSprite());
        Call("UpdateSelectedCharacter", character);
        player.Data["HeldItems"] = new PlayerProperty(Convert.ToBase64String(new byte[] { 3, 1, 2 }));
        var lobby = new GameSessionView();
        lobby.Players.Add(player);
        Call("UpdatePlayerData", lobby);
        AssertHeldItems(3, 1, 2);
        Assert.That(icon.GetType().GetProperty("SelectedCharacter").GetValue(icon), Is.SameAs(character));
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null && tweenType != null)
        {
            var kill = tweenType.GetMethod("Kill", new[] { typeof(object), typeof(bool) });
            foreach (var transform in root.GetComponentsInChildren<RectTransform>(true))
                kill.Invoke(null, new object[] { transform, false });
        }
        if (charactersInstance != null) charactersInstance.SetValue(null, previousCharacters);
        if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
        if (tweenType != null && !tweensWereInitialized)
            tweenType.GetMethod("Clear", new[] { typeof(bool) }).Invoke(null, new object[] { true });
        foreach (var asset in createdAssets)
            if (asset != null) Object.DestroyImmediate(asset);
        createdAssets.Clear();
    }
}

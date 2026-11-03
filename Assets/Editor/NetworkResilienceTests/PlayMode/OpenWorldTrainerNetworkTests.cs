using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

public class OpenWorldTrainerNetworkTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private NetworkManager host;
    private NetworkManager client;
    private GameObject prefab;
    private Type avatarType;
    private Type clothesType;
    private object previousClothesList;
    private Camera sceneCamera;
    private NetworkObject trainer;
    private ScriptableObject emoteCatalogOverride;
    private Gamepad testGamepad;
    private Type companionRosterType;
    private object previousCompanionRoster;
    private ScriptableObject hiddenCompanion;
    private bool hadCompanionPreference;
    private string companionPreference;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void CallPrivate(object target, string name, params object[] arguments) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    private static TMP_Text Nameplate(Component avatar) => avatar.transform.Find("PlayerName").GetComponent<TMP_Text>();
    private static object Call(object target, string method, params object[] arguments) => target.GetType()
        .GetMethods(BindingFlags.Instance | BindingFlags.Public).First(candidate => candidate.Name == method &&
            candidate.GetParameters().Length == arguments.Length && candidate.GetParameters()
                .Select((parameter, index) => parameter.ParameterType.IsInstanceOfType(arguments[index])).All(matches => matches))
        .Invoke(target, arguments);

    private GameObject Node(string name)
    {
        var node = new GameObject(name);
        objects.Add(node);
        return node;
    }

    private NetworkManager Manager(string name, ushort port)
    {
        var node = Node(name);
        var manager = node.AddComponent<NetworkManager>();
        var transport = node.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
        manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
        manager.AddNetworkPrefab(prefab);
        return manager;
    }

    private static IEnumerator Until(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "The trainer did not reach the expected network state.");
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (NetworkManager.Singleton != null) Assert.Ignore("An external network session is active.");
#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldTrainer.prefab");
#else
        Assert.Ignore("Requires the Editor-authored trainer prefab.");
#endif
        Assert.That(prefab, Is.Not.Null);
        var authoredName = Nameplate(prefab.transform);
        Assert.That(authoredName, Is.Not.Null, "The trainer nameplate must be authored in the prefab.");
        Assert.That(authoredName.font, Is.Not.Null);
        Assert.That(authoredName.richText, Is.False);
        Assert.That(authoredName.raycastTarget, Is.False);
        avatarType = GameType("OpenWorldTrainerAvatar");
        clothesType = GameType("ClothesList");
        previousClothesList = clothesType.GetField("Instance").GetValue(null);
        Node("Empty outfit fixture").AddComponent(clothesType);
        sceneCamera = Node("City scene camera").AddComponent<Camera>();
        sceneCamera.tag = "MainCamera";
        host = Manager("City host", 0);
        Assert.That(host.StartHost(), Is.True);
        ushort port = host.GetComponent<UnityTransport>().GetLocalEndpoint().Port;
        client = Manager("City client", port);
        Assert.That(client.StartClient(), Is.True);
        yield return Until(() => client.IsConnectedClient);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (trainer != null && trainer.IsSpawned) trainer.Despawn();
        if (client != null) client.Shutdown();
        if (host != null) host.Shutdown();
        yield return null;
        foreach (var node in objects) if (node != null) Object.Destroy(node);
        objects.Clear();
        yield return null;
        if (clothesType != null) clothesType.GetField("Instance").SetValue(null, previousClothesList);
        if (emoteCatalogOverride != null) Object.Destroy(emoteCatalogOverride);
        if (testGamepad != null) InputSystem.RemoveDevice(testGamepad);
        if (companionRosterType != null)
        {
            companionRosterType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, previousCompanionRoster);
            if (hadCompanionPreference) PlayerPrefs.SetString("OpenWorldLobby.Companion", companionPreference);
            else PlayerPrefs.DeleteKey("OpenWorldLobby.Companion");
            PlayerPrefs.Save();
        }
        if (hiddenCompanion != null) Object.Destroy(hiddenCompanion);
    }

    [UnityTest]
    public IEnumerator HostTrainerReplicatesIdentityAndRestoresSceneCamera() => CheckTrainer(false);

    [UnityTest]
    public IEnumerator VisitingTrainerOwnsMovementAndReplicatesPose() => CheckTrainer(true);

    [UnityTest]
    public IEnumerator HostEmoteMenuPlaysAndReplicatesThenReturnsToIdle() => CheckEmote(false);

    [UnityTest]
    public IEnumerator VisitingFemaleTrainerEmoteReplicatesAndReturnsToIdle() => CheckEmote(true);

    [UnityTest]
    public IEnumerator EmoteSpeedChangesPlaybackAndRestoresNormalIdleSpeed() => CheckEmote(true, 2f);

    [UnityTest]
    public IEnumerator VisitingOwnerCanOrbitCancelAndReplaceLoopingEmotes() => CheckEmote(true, 2f, true);

    [UnityTest]
    public IEnumerator HostCompanionSelectionReplicatesAndPickerBlocksMovement() => CheckCompanion(false);

    [UnityTest]
    public IEnumerator VisitingCompanionSelectionReplicatesAndReplacesOldModels() => CheckCompanion(true);

    private IEnumerator CheckCompanion(bool visitingOwner)
    {
#if UNITY_EDITOR
        companionRosterType = GameType("CharactersList");
        var singleton = companionRosterType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        previousCompanionRoster = singleton.GetValue(null);
        singleton.SetValue(null, null);
        hadCompanionPreference = PlayerPrefs.HasKey("OpenWorldLobby.Companion");
        companionPreference = PlayerPrefs.GetString("OpenWorldLobby.Companion", "");
        PlayerPrefs.SetString("OpenWorldLobby.Companion", "Absol");
        var first = AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/PokemonSelectInfo/Absol.asset");
        var second = AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/PokemonSelectInfo/Cinderace.asset");
        hiddenCompanion = (ScriptableObject)Object.Instantiate(first);
        hiddenCompanion.GetType().GetField("Hidden").SetValue(hiddenCompanion, true);
        var characters = Array.CreateInstance(GameType("CharacterInfo"), 3);
        characters.SetValue(first, 0);
        characters.SetValue(second, 1);
        characters.SetValue(hiddenCompanion, 2);
        var rosterObject = Node("Companion roster fixture");
        rosterObject.SetActive(false);
        var roster = rosterObject.AddComponent(companionRosterType);
        companionRosterType.GetField("characters", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(roster, characters);
        LogAssert.Expect(LogType.Warning, "MoveAssetRegistry is not assigned in CharactersList!");
        rosterObject.SetActive(true);

        var instance = Object.Instantiate(prefab, new Vector3(1f, 0.3f, 2f), Quaternion.identity);
        objects.Add(instance);
        Call(instance.GetComponent(avatarType), "Initialize", "companion-trainer", "Companion Trainer", "");
        trainer = instance.GetComponent<NetworkObject>();
        trainer.SpawnWithOwnership(visitingOwner ? client.LocalClientId : host.LocalClientId);
        yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(trainer.NetworkObjectId));
        var remote = client.SpawnManager.SpawnedObjects[trainer.NetworkObjectId];
        var companionType = GameType("OpenWorldPokemonCompanion");
        var owner = (visitingOwner ? remote.gameObject : instance).GetComponent(companionType);
        var observer = (visitingOwner ? instance : remote.gameObject).GetComponent(companionType);
        yield return Until(() => (short)Get(owner, "SelectedPokemonId") == 0 && (short)Get(observer, "SelectedPokemonId") == 0 &&
            (Object)Field(owner, "follower") != null && (Object)Field(observer, "follower") != null);
        var oldOwnerFollower = (Component)Field(owner, "follower");
        var oldObserverFollower = (Component)Field(observer, "follower");
        Assert.That(Call(observer, "TrySelect", (short)1), Is.False);
        Assert.That(Call(owner, "TrySelect", (short)-1), Is.False);
        Assert.That(Call(owner, "TrySelect", short.MaxValue), Is.False);
        Assert.That(Call(owner, "TrySelect", (short)2), Is.False);
        CallPrivate(owner, "SelectPokemonRpc", (short)2);
        CallPrivate(owner, "SelectPokemonRpc", short.MaxValue);
        yield return new WaitForSeconds(0.1f);
        Assert.That(Get(owner, "SelectedPokemonId"), Is.EqualTo((short)0));
        Assert.That(Get(observer, "SelectedPokemonId"), Is.EqualTo((short)0));

        Component picker = null;
        if (!visitingOwner)
        {
            CallPrivate(owner.GetComponent(avatarType), "RefreshOwnership");
            var hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldLobbyHUD.prefab"));
            objects.Add(hud);
            LogAssert.Expect(LogType.Warning, "Start from BootScene to connect to the open world lobby.");
            yield return null;
            var menu = hud.GetComponent(GameType("OpenWorldPokemonMenu"));
            picker = menu;
            Call(menu, "Toggle");
            Assert.That((bool)GameType("OpenWorldPokemonMenu").GetProperty("IsMenuOpen").GetValue(null), Is.True);
            Assert.That(Get(owner.GetComponent(avatarType), "CanStartEmote"), Is.False);
            var data = new SerializedObject(menu);
            var rows = data.FindProperty("pokemonButtons");
            var selection = Enumerable.Range(0, rows.arraySize).Select(index => rows.GetArrayElementAtIndex(index))
                .Single(row => row.FindPropertyRelative("character").objectReferenceValue == second);
            ((Button)selection.FindPropertyRelative("button").objectReferenceValue).onClick.Invoke();
            Assert.That(((GameObject)data.FindProperty("panel").objectReferenceValue).activeSelf, Is.False);
            Assert.That(Get(owner.GetComponent(avatarType), "CanStartEmote"), Is.True);
        }
        else Assert.That(Call(owner, "TrySelect", (short)1), Is.True);

        yield return Until(() => (short)Get(owner, "SelectedPokemonId") == 1 && (short)Get(observer, "SelectedPokemonId") == 1 &&
            (Object)Field(owner, "follower") != null && (Object)Field(observer, "follower") != null);
        Assert.That(PlayerPrefs.GetString("OpenWorldLobby.Companion"), Is.EqualTo("Cinderace"));
        Assert.That(oldOwnerFollower == null, Is.True);
        Assert.That(oldObserverFollower == null, Is.True);
        var currentOwnerFollower = (Component)Field(owner, "follower");
        var currentObserverFollower = (Component)Field(observer, "follower");
        Assert.That(Get(currentOwnerFollower, "Target"), Is.EqualTo(owner.transform));
        Assert.That(Get(currentObserverFollower, "Target"), Is.EqualTo(observer.transform));
        if (picker != null)
        {
            var icon = (Image)new SerializedObject(picker).FindProperty("currentIcon").objectReferenceValue;
            Assert.That(icon.sprite, Is.EqualTo(Get(second, "CircleIcon")));
            Call(picker, "Toggle");
        }
        trainer.Despawn();
        yield return Until(() => currentOwnerFollower == null && currentObserverFollower == null);
        if (picker != null)
            Assert.That((bool)GameType("OpenWorldPokemonMenu").GetProperty("IsMenuOpen").GetValue(null), Is.False);

        var returning = Object.Instantiate(prefab);
        objects.Add(returning);
        Call(returning.GetComponent(avatarType), "Initialize", "companion-trainer", "Companion Trainer", "");
        trainer = returning.GetComponent<NetworkObject>();
        trainer.SpawnWithOwnership(visitingOwner ? client.LocalClientId : host.LocalClientId);
        yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(trainer.NetworkObjectId));
        var returningOwner = (visitingOwner ? client.SpawnManager.SpawnedObjects[trainer.NetworkObjectId].gameObject : returning)
            .GetComponent(companionType);
        yield return Until(() => (short)Get(returningOwner, "SelectedPokemonId") == 1 && (Object)Field(returningOwner, "follower") != null);
        Assert.That(Call(returningOwner, "TrySelect", (short)0), Is.True);
        Assert.That(Call(returningOwner, "TrySelect", (short)1), Is.True);
        yield return new WaitForSeconds(0.2f);
        yield return Until(() => (short)Get(returningOwner, "SelectedPokemonId") == 1 && (Object)Field(returningOwner, "follower") != null);
        LogAssert.NoUnexpectedReceived();
#else
        Assert.Ignore("Requires Editor-authored companion assets.");
        yield break;
#endif
    }

    private IEnumerator CheckEmote(bool visitingOwner, float speed = 1f, bool looping = false)
    {
        var instance = Object.Instantiate(prefab, new Vector3(1f, 0.3f, 2f), Quaternion.identity);
        objects.Add(instance);
        var avatar = instance.GetComponent(avatarType);
        string clothing = "";
        if (visitingOwner && !looping)
        {
            var info = Activator.CreateInstance(GameType("PlayerClothesInfo"));
            clothing = (string)Call(info, "Serialize");
        }
        Call(avatar, "Initialize", "emote-trainer", "Emote Trainer", clothing);
        trainer = instance.GetComponent<NetworkObject>();
        trainer.SpawnWithOwnership(visitingOwner ? client.LocalClientId : host.LocalClientId);
        yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(trainer.NetworkObjectId));
        var remote = client.SpawnManager.SpawnedObjects[trainer.NetworkObjectId];
        var emotesType = GameType("OpenWorldTrainerEmotes");
        var owner = (visitingOwner ? remote.gameObject : instance).GetComponent(emotesType);
        var observer = (visitingOwner ? instance : remote.gameObject).GetComponent(emotesType);
        yield return Until(() => Get(owner, "ActiveAnimator") != null && Get(observer, "ActiveAnimator") != null);
        var ownerAnimator = (Animator)Get(owner, "ActiveAnimator");
        var observerAnimator = (Animator)Get(observer, "ActiveAnimator");
        ownerAnimator.cullingMode = observerAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var entries = ((IEnumerable)Get(Get(owner, "Catalog"), "Entries")).Cast<object>().ToArray();
        int idleHash = ownerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
        int index = Array.FindIndex(entries, entry => (float)Get(entry, "Duration") >= 0.6f &&
            (float)Get(entry, "Duration") < 2.5f && (int)Get(entry, "StateHash") != idleHash &&
            ((AnimationClip)Get(entry, "Clip")).isLooping == looping &&
            ownerAnimator.HasState(0, (int)Get(entry, "StateHash")));
        Assert.That(index, Is.GreaterThanOrEqualTo(0), "The authored catalog needs a short playable animation for this trainer.");
#if UNITY_EDITOR
        if (speed != 1f)
        {
            emoteCatalogOverride = Object.Instantiate((ScriptableObject)Get(owner, "Catalog"));
            var data = new SerializedObject(emoteCatalogOverride);
            var male = data.FindProperty("maleAnimations");
            var entry = index < male.arraySize ? male.GetArrayElementAtIndex(index) :
                data.FindProperty("femaleAnimations").GetArrayElementAtIndex(index - male.arraySize);
            entry.FindPropertyRelative("speed").floatValue = speed;
            data.ApplyModifiedPropertiesWithoutUndo();
            var field = emotesType.GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(owner, emoteCatalogOverride);
            field.SetValue(observer, emoteCatalogOverride);
        }
#endif
        int stateHash = (int)Get(entries[index], "StateHash");
        Assert.That(Call(owner, "TryPlay", -1), Is.False);
        Assert.That(Call(owner, "TryPlay", entries.Length), Is.False);
        Assert.That(Call(observer, "TryPlay", index), Is.False);
        var ownerAvatar = owner.GetComponent(avatarType);
        Call(ownerAvatar, "SetInputBlocked", true);
        Assert.That(Call(owner, "TryPlay", index), Is.False);
        Call(ownerAvatar, "SetInputBlocked", false);
        if (!visitingOwner)
        {
#if UNITY_EDITOR
            var hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldLobbyHUD.prefab"));
            objects.Add(hud);
            LogAssert.Expect(LogType.Warning, "Start from BootScene to connect to the open world lobby.");
            yield return null;
            var menu = hud.GetComponent(GameType("OpenWorldEmoteMenu"));
            int objectCount = hud.GetComponentsInChildren<Transform>(true).Length;
            Call(menu, "Toggle");
            var menuData = new SerializedObject(menu);
            var panel = (GameObject)menuData.FindProperty("panel").objectReferenceValue;
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(Get(ownerAvatar, "CanStartEmote"), Is.False);
            var rows = menuData.FindProperty("animationButtons");
            int visible = 0;
            for (int rowIndex = 0; rowIndex < rows.arraySize; rowIndex++)
            {
                var button = (Button)rows.GetArrayElementAtIndex(rowIndex).FindPropertyRelative("button").objectReferenceValue;
                bool valid = rowIndex < entries.Length && ownerAnimator.HasState(0, (int)Get(entries[rowIndex], "StateHash"));
                Assert.That(button.gameObject.activeSelf, Is.EqualTo(valid));
                if (valid)
                {
                    visible++;
                    var label = (TMP_Text)rows.GetArrayElementAtIndex(rowIndex).FindPropertyRelative("label").objectReferenceValue;
                    Assert.That(label.text, Is.EqualTo(Get(entries[rowIndex], "DisplayName")));
                }
            }
            Assert.That(visible, Is.GreaterThan(0));
            var selected = (Button)rows.GetArrayElementAtIndex(index).FindPropertyRelative("button").objectReferenceValue;
            selected.onClick.Invoke();
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(hud.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount));
#endif
        }
        else Assert.That(Call(owner, "TryPlay", index), Is.True);
        yield return Until(() => (int)Get(owner, "CurrentEmoteIndex") == index && (int)Get(observer, "CurrentEmoteIndex") == index);
        yield return Until(() => AnimatorShows(ownerAnimator, stateHash) && AnimatorShows(observerAnimator, stateHash));
        Assert.That(ownerAnimator.speed, Is.EqualTo(speed));
        Assert.That(observerAnimator.speed, Is.EqualTo(speed));
        uint sequence = (uint)Field(owner, "appliedSequence");
        Assert.That(Get(owner, "CanPlay"), Is.True, "The picker must remain available during playback.");
        Assert.That(Call(owner, "TryPlay", index), Is.True, "Selecting the same emote restarts it.");
        yield return Until(() => (uint)Field(owner, "appliedSequence") != sequence &&
            (uint)Field(observer, "appliedSequence") != sequence);
        if (looping)
        {
            yield return new WaitForSeconds((float)Get(entries[index], "Duration") / speed * 2f + 0.3f);
            Assert.That(Get(owner, "IsPlaying"), Is.True);
            Assert.That(Get(observer, "IsPlaying"), Is.True);
            Assert.That(ownerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(stateHash));
            Assert.That(ownerAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(1f));
            testGamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.EnableDevice(testGamepad);
            InputState.Change(testGamepad.rightStick, Vector2.right, InputState.currentUpdateType);
            testGamepad.MakeCurrent();
            Assert.That(testGamepad.rightStick.ReadValue().x, Is.GreaterThan(0f), "Synthetic look input was not applied.");
            float yaw = (float)Field(ownerAvatar, "cameraYaw");
            CallPrivate(ownerAvatar, "UpdateLookInput");
            Assert.That((float)Field(ownerAvatar, "cameraYaw"), Is.GreaterThan(yaw), "Orbit did not advance with deltaTime=" + Time.deltaTime);
            Assert.That(Get(owner, "IsPlaying"), Is.True, "Orbiting must not cancel an emote.");
            Vector3 position = ownerAvatar.transform.position;
            CallPrivate(ownerAvatar, "Move", Vector2.up);
            Assert.That(Get(owner, "IsPlaying"), Is.False, "Movement cancellation must not wait for the server.");
            Assert.That(Vector3.Distance(position, ownerAvatar.transform.position), Is.GreaterThan(0f), "Movement did not advance with deltaTime=" + Time.deltaTime);
            yield return Until(() => !(bool)Get(observer, "IsPlaying"));
#if UNITY_EDITOR
            var controller = (UnityEditor.Animations.AnimatorController)ownerAnimator.runtimeAnimatorController;
            var run = controller.layers[0].stateMachine.anyStateTransitions.Single(transition =>
                transition.conditions.Any(condition => condition.parameter == "startRun")).destinationState;
            int runHash = Animator.StringToHash(controller.layers[0].name + "." + run.name);
            float deadline = Time.realtimeSinceStartup + 3f;
            while ((!AnimatorShows(ownerAnimator, runHash) || !AnimatorShows(observerAnimator, runHash)) && Time.realtimeSinceStartup < deadline)
            {
                CallPrivate(ownerAvatar, "Move", Vector2.up);
                yield return null;
            }
            Assert.That(AnimatorShows(ownerAnimator, runHash), Is.True, "Movement must animate the owner running after cancellation.");
            Assert.That(AnimatorShows(observerAnimator, runHash), Is.True, "Movement must animate observers running after cancellation.");
#endif
            Assert.That(Call(observer, "TryPlay", index), Is.False);
            Assert.That(Call(owner, "TryPlay", index), Is.True);
            yield return Until(() => (bool)Get(observer, "IsPlaying") && (bool)Get(owner, "IsPlaying"));
            int replacement = Array.FindIndex(entries, entry => !((AnimationClip)Get(entry, "Clip")).isLooping &&
                (float)Get(entry, "Duration") >= 0.6f && (float)Get(entry, "Duration") < 2.5f &&
                (int)Get(entry, "StateHash") != idleHash && ownerAnimator.HasState(0, (int)Get(entry, "StateHash")));
            Assert.That(replacement, Is.GreaterThanOrEqualTo(0));
            Assert.That(Call(owner, "TryPlay", replacement), Is.True);
            yield return Until(() => (int)Get(owner, "CurrentEmoteIndex") == replacement && (int)Get(observer, "CurrentEmoteIndex") == replacement);
        }
        yield return Until(() => !(bool)Get(owner, "IsPlaying") && !(bool)Get(observer, "IsPlaying"));
        yield return Until(() => ownerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash == idleHash &&
            observerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash == idleHash &&
            !ownerAnimator.IsInTransition(0) && !observerAnimator.IsInTransition(0));
        Assert.That(Get(owner, "CanPlay"), Is.True, "Emote readiness after idle: ready=" + Get(owner, "IsReady") +
            ", focus=" + Field(ownerAvatar, "hasFocus") + ", paused=" + Field(ownerAvatar, "isPaused") +
            ", canStart=" + Get(ownerAvatar, "CanStartEmote") + ", active chat inputs=" + string.Join(", ",
                Object.FindObjectsByType(GameType("ChatManager"), FindObjectsSortMode.None).Cast<Component>()
                    .SelectMany(chat => chat.GetComponentsInChildren<TMP_InputField>(true))
                    .Where(input => input.isActiveAndEnabled).Select(input => input.name)));
        Assert.That(ownerAnimator.speed, Is.EqualTo(1f));
        Assert.That(observerAnimator.speed, Is.EqualTo(1f));
        Assert.That(Call(owner, "TryPlay", index), Is.True, "The same emote can be played again after completion.");
        yield return Until(() => (bool)Get(owner, "IsPlaying") && (bool)Get(observer, "IsPlaying"));
        if (visitingOwner)
        {
            Assert.That(Call(owner, "TryPlay", index), Is.True);
            CallPrivate(ownerAvatar, "Move", Vector2.up);
            Assert.That(Get(owner, "IsPlaying"), Is.False);
            yield return Until(() => !(bool)Get(observer, "IsPlaying"));
            yield return new WaitForSeconds(0.2f);
            Assert.That(Get(owner, "IsPlaying"), Is.False, "An in-flight play request must not undo movement cancellation.");
        }
        LogAssert.NoUnexpectedReceived();
    }

    private static bool AnimatorShows(Animator animator, int hash) => animator.GetCurrentAnimatorStateInfo(0).fullPathHash == hash ||
        (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == hash);

    [UnityTest]
    public IEnumerator OtherTrainersDoNotDisableLocalEmotePicker()
    {
        var emotesType = GameType("OpenWorldTrainerEmotes");
        var instance = Object.Instantiate(prefab);
        objects.Add(instance);
        var localAvatar = instance.GetComponent(avatarType);
        Call(localAvatar, "Initialize", "local-trainer", "Local Trainer", "");
        trainer = instance.GetComponent<NetworkObject>();
        trainer.SpawnWithOwnership(host.LocalClientId);
        var visitor = Object.Instantiate(prefab);
        objects.Add(visitor);
        Call(visitor.GetComponent(avatarType), "Initialize", "other-trainer", "Other Trainer", "");
        var visitorNetwork = visitor.GetComponent<NetworkObject>();
        visitorNetwork.SpawnWithOwnership(client.LocalClientId);
        yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(visitorNetwork.NetworkObjectId));
        var visitingOwner = client.SpawnManager.SpawnedObjects[visitorNetwork.NetworkObjectId].GetComponent(emotesType);
        var localEmotes = instance.GetComponent(emotesType);
        yield return Until(() => Get(visitingOwner, "ActiveAnimator") != null && Get(localEmotes, "ActiveAnimator") != null);
        CallPrivate(localAvatar, "RefreshOwnership");
#if UNITY_EDITOR
        var hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldLobbyHUD.prefab"));
        objects.Add(hud);
        LogAssert.Expect(LogType.Warning, "Start from BootScene to connect to the open world lobby.");
        yield return null;
        var menu = hud.GetComponent(GameType("OpenWorldEmoteMenu"));
        var data = new SerializedObject(menu);
        var open = (Button)data.FindProperty("openButton").objectReferenceValue;
        var panel = (GameObject)data.FindProperty("panel").objectReferenceValue;
        var chat = hud.GetComponent(GameType("ChatManager"));
        Assert.That(Get(localAvatar, "CanStartEmote"), Is.True);
        Call(chat, "OpenChat");
        Assert.That(Get(localAvatar, "CanStartEmote"), Is.False, "Open city chat must reuse the trainer's movement/emote input block.");
        Call(chat, "CloseChat");
        Assert.That(Get(localAvatar, "CanStartEmote"), Is.True);
        var entries = ((IEnumerable)Get(Get(visitingOwner, "Catalog"), "Entries")).Cast<object>().ToArray();
        var animator = (Animator)Get(visitingOwner, "ActiveAnimator");
        int index = Array.FindIndex(entries, entry => ((AnimationClip)Get(entry, "Clip")).isLooping &&
            animator.HasState(0, (int)Get(entry, "StateHash")));
        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        Assert.That(Call(visitingOwner, "TryPlay", index), Is.True);
        yield return Until(() => (bool)Get(visitor.GetComponent(emotesType), "IsPlaying"));
        yield return null;
        Assert.That(avatarType.GetProperty("LocalAvatar").GetValue(null), Is.SameAs(localAvatar));
        Assert.That(open.interactable, Is.True, "Remote playback must not disable the local picker.");
        Call(menu, "Toggle");
        yield return null;
        Assert.That(panel.activeSelf, Is.True);
        Call(menu, "PlayEmote", index);
        yield return Until(() => (bool)Get(localEmotes, "IsPlaying"));
        yield return null;
        Assert.That(open.interactable, Is.True, "Local playback must also allow replacement emotes.");
        Call(menu, "Toggle");
        yield return null;
        Assert.That(panel.activeSelf, Is.True, "Playback must not close an open picker.");
        Call(menu, "PlayEmote", index);
        Assert.That(panel.activeSelf, Is.False);
#endif
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator NameplateTracksReplacementMainCamera()
    {
        var label = Object.Instantiate(prefab.transform.Find("PlayerName").gameObject);
        objects.Add(label);
        sceneCamera.transform.rotation = Quaternion.Euler(15f, 30f, 0f);
        yield return Until(() => Quaternion.Angle(label.transform.rotation, sceneCamera.transform.rotation) < 0.1f);
        sceneCamera.enabled = false;
        var replacement = Node("Replacement city camera").AddComponent<Camera>();
        replacement.tag = "MainCamera";
        replacement.transform.rotation = Quaternion.Euler(35f, 120f, 0f);
        yield return Until(() => Quaternion.Angle(label.transform.rotation, replacement.transform.rotation) < 0.1f);
        Object.Destroy(replacement.gameObject);
        sceneCamera.enabled = true;
        yield return Until(() => Quaternion.Angle(label.transform.rotation, sceneCamera.transform.rotation) < 0.1f);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator ChatBubbleTracksReplacementMainCamera()
    {
        var panel = Object.Instantiate(prefab.transform.Find("ChatBubble").gameObject);
        objects.Add(panel);
        panel.SetActive(true);
        sceneCamera.transform.rotation = Quaternion.Euler(15f, 30f, 0f);
        yield return Until(() => Quaternion.Angle(panel.transform.rotation, sceneCamera.transform.rotation) < 0.1f);
        sceneCamera.enabled = false;
        var replacement = Node("Replacement bubble camera").AddComponent<Camera>();
        replacement.tag = "MainCamera";
        replacement.transform.rotation = Quaternion.Euler(35f, 120f, 0f);
        yield return Until(() => Quaternion.Angle(panel.transform.rotation, replacement.transform.rotation) < 0.1f);
        Object.Destroy(replacement.gameObject);
        sceneCamera.enabled = true;
        yield return Until(() => Quaternion.Angle(panel.transform.rotation, sceneCamera.transform.rotation) < 0.1f);
        LogAssert.NoUnexpectedReceived();
    }

    private IEnumerator CheckTrainer(bool visitingOwner)
    {
        var instance = Object.Instantiate(prefab, new Vector3(1f, 0.3f, 2f), Quaternion.identity);
        objects.Add(instance);
        var avatar = instance.GetComponent(avatarType);
        Call(avatar, "Initialize", "stable-trainer", "City Trainer", "");
        Assert.That((string)Get(avatar, "PlayerId"), Is.EqualTo("stable-trainer"));
        trainer = instance.GetComponent<NetworkObject>();
        trainer.SpawnWithOwnership(visitingOwner ? client.LocalClientId : host.LocalClientId);
        yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(trainer.NetworkObjectId));
        var remote = client.SpawnManager.SpawnedObjects[trainer.NetworkObjectId].GetComponent(avatarType);
        var owner = visitingOwner ? remote : avatar;
        var observer = visitingOwner ? avatar : remote;
        Assert.That((string)Get(remote, "PlayerId"), Is.EqualTo("stable-trainer"));
        Assert.That((string)Get(remote, "PlayerName"), Is.EqualTo("City Trainer"));
        Assert.That((string)Get(remote, "Clothing"), Is.EqualTo(Get(avatar, "Clothing")));
        Assert.That(Nameplate(avatar).text, Is.EqualTo("City Trainer"));
        Assert.That(Nameplate(remote).text, Is.EqualTo("City Trainer"));
        Assert.That(Nameplate(avatar).gameObject.activeSelf, Is.True);
        Assert.That(Nameplate(remote).gameObject.activeSelf, Is.True);
        Assert.That(owner.GetComponent<CharacterController>().enabled, Is.True);
        Assert.That(observer.GetComponent<CharacterController>().enabled, Is.False);
        Assert.That(Get(owner, "LocalCamera"), Is.Not.Null);
        Assert.That(Get(observer, "LocalCamera"), Is.Null);
        Assert.That(sceneCamera.enabled, Is.False);
        var ownerCamera = (Camera)Get(owner, "LocalCamera");
        yield return Until(() => Quaternion.Angle(Nameplate(owner).transform.rotation, ownerCamera.transform.rotation) < 0.1f &&
            Quaternion.Angle(Nameplate(observer).transform.rotation, ownerCamera.transform.rotation) < 0.1f);
        Call(owner, "SetSpawnPosition", new Vector3(5f, 0.3f, 6f), 75f);
        yield return Until(() => Vector3.Distance(observer.transform.position, owner.transform.position) < 0.05f);
        Call(avatar, "Initialize", "stable-trainer", "Updated Trainer", (string)Get(avatar, "Clothing"));
        yield return Until(() => (string)Get(remote, "PlayerName") == "Updated Trainer");
        Assert.That(Nameplate(avatar).text, Is.EqualTo("Updated Trainer"));
        Assert.That(Nameplate(remote).text, Is.EqualTo("Updated Trainer"));
        trainer.Despawn();
        yield return Until(() => !client.SpawnManager.SpawnedObjects.Values.Any(value => value.GetComponent(avatarType) != null));
        Assert.That(avatarType.GetProperty("LocalAvatar").GetValue(null), Is.Null);
        Assert.That(sceneCamera.enabled, Is.True);
        var pose = new object[] { "stable-trainer", Vector3.zero, 0f };
        Assert.That(avatarType.GetMethod("TryGetLocalPose").Invoke(null, pose), Is.True);
        Assert.That(Vector3.Distance((Vector3)pose[1], new Vector3(5f, 0.3f, 6f)), Is.LessThan(0.05f));
        LogAssert.NoUnexpectedReceived();
    }
}

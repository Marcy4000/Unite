using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class OpenWorldLobbyChatAuthoring
{
    private const string HudPath = "Assets/Prefabs/Lobby/OpenWorldLobbyHUD.prefab";

    [MenuItem("Tools/Open World Lobby/Wire Existing Chat UI")]
    public static void Wire()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/LobbyScene.unity");
        bool openedScene = !scene.isLoaded;
        if (openedScene) scene = EditorSceneManager.OpenScene("Assets/Scenes/LobbyScene.unity", OpenSceneMode.Additive);
        var root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            var source = scene.GetRootGameObjects().SelectMany(node => node.GetComponentsInChildren<ChatManager>(true)).Single();
            var sourceData = new SerializedObject(source);
            var holder = root.GetComponentsInChildren<Transform>(true).Single(node => node.name == "ChatHolder");
            var canvas = holder.parent;
            var manager = root.GetComponent<ChatManager>();
            if (manager == null) manager = root.AddComponent<ChatManager>();
            var data = new SerializedObject(manager);
            foreach (string name in new[] { "messageRecieverPrefab", "chatMessagePrefabOther", "chatMessagePrefabSelf" })
                data.FindProperty(name).objectReferenceValue = sourceData.FindProperty(name).objectReferenceValue;
            data.FindProperty("notificationDuration").floatValue = sourceData.FindProperty("notificationDuration").floatValue;

            var input = holder.GetComponentInChildren<TMP_InputField>(true);
            var global = holder.GetComponentsInChildren<Toggle>(true).Single(toggle => toggle.name == "GlobalChat");
            var privateToggle = holder.GetComponentsInChildren<Toggle>(true).Single(toggle => toggle.name == "PrivateChat");
            var dropdown = holder.GetComponentInChildren<TMP_Dropdown>(true);
            data.FindProperty("chatContainer").objectReferenceValue = holder.gameObject;
            data.FindProperty("chatMessageContainer").objectReferenceValue = holder.GetComponentsInChildren<ScrollRect>(true)
                .Single(scroll => scroll.name == "Scroll View").content;
            data.FindProperty("chatInputField").objectReferenceValue = input;
            data.FindProperty("globalChatToggle").objectReferenceValue = global;
            data.FindProperty("privateChatToggle").objectReferenceValue = privateToggle;
            data.FindProperty("privatePlayerDropdown").objectReferenceValue = dropdown;
            global.onValueChanged = new Toggle.ToggleEvent();
            privateToggle.onValueChanged = new Toggle.ToggleEvent();
            dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
            input.onSubmit = new TMP_InputField.SubmitEvent();
            UnityEventTools.AddVoidPersistentListener(input.onSubmit, manager.SendChatMessage);
            foreach (var button in holder.GetComponentsInChildren<Button>(true))
            {
                if (button.name != "Arrow" && button.name != "SendButton") continue;
                button.onClick = new Button.ButtonClickedEvent();
                if (button.name == "Arrow") UnityEventTools.AddPersistentListener(button.onClick, manager.CloseChat);
                else UnityEventTools.AddPersistentListener(button.onClick, manager.SendChatMessage);
            }

            var sourceOpen = source.GetComponentsInChildren<Button>(true).Single(button => button.name == "OpenChatButton");
            var open = canvas.Find("OpenChatButton");
            if (open == null) open = Object.Instantiate(sourceOpen.gameObject, canvas, false).transform;
            open.name = "OpenChatButton";
            var openButton = open.GetComponent<Button>();
            openButton.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(openButton.onClick, manager.OpenChat);
            data.FindProperty("unreadBadge").objectReferenceValue = open.Find("UnreadMessagesBadge").gameObject;
            data.FindProperty("unreadCountText").objectReferenceValue = open.GetComponentsInChildren<TMP_Text>(true)
                .Single(text => text.name == "UnreadCount");

            var sourceNotification = (GameObject)sourceData.FindProperty("messageNotification").objectReferenceValue;
            var notification = canvas.Find("CityChatNotification");
            if (notification == null) notification = Object.Instantiate(sourceNotification, canvas, false).transform;
            notification.name = "CityChatNotification";
            var notificationButton = notification.GetComponent<Button>();
            notificationButton.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(notificationButton.onClick, manager.OpenChat);
            data.FindProperty("messageNotification").objectReferenceValue = notification.gameObject;
            data.FindProperty("notificationSenderText").objectReferenceValue = notification.Find("Sender").GetComponent<TMP_Text>();
            data.FindProperty("notificationMessageText").objectReferenceValue = notification.Find("MessagePreview").GetComponent<TMP_Text>();
            notification.Find("Title").GetComponent<TMP_Text>().text = "City chat";
            data.ApplyModifiedPropertiesWithoutUndo();
            open.Find("UnreadMessagesBadge").gameObject.SetActive(false);
            notification.gameObject.SetActive(false);
            holder.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            if (openedScene) EditorSceneManager.CloseScene(scene, true);
        }
    }
}

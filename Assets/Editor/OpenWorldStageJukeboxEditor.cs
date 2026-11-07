using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(OpenWorldStageJukebox))]
public sealed class OpenWorldStageJukeboxEditor : Editor
{
    private ReorderableList playlist;
    private SerializedProperty tracks;
    private SerializedProperty defaultTrack;

    private void OnEnable()
    {
        tracks = serializedObject.FindProperty("tracks");
        defaultTrack = serializedObject.FindProperty("defaultTrack");
        playlist = new ReorderableList(serializedObject, tracks, true, true, true, true);
        playlist.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Playable Tracks / Menu Order");
        playlist.elementHeight = EditorGUIUtility.singleLineHeight * 2f + 10f;
        playlist.drawElementCallback = (rect, index, active, focused) =>
        {
            var track = tracks.GetArrayElementAtIndex(index);
            rect.y += 3f;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(rect, track.FindPropertyRelative("title"), new GUIContent("Display Name"));
            rect.y += EditorGUIUtility.singleLineHeight + 3f;
            EditorGUI.PropertyField(rect, track.FindPropertyRelative("music"), new GUIContent("Music Track"));
        };
        playlist.onAddCallback = list =>
        {
            int index = tracks.arraySize++;
            var track = tracks.GetArrayElementAtIndex(index);
            track.FindPropertyRelative("title").stringValue = "";
            track.FindPropertyRelative("music").objectReferenceValue = null;
            list.index = index;
        };
        playlist.onRemoveCallback = list =>
        {
            if (defaultTrack.intValue == list.index) defaultTrack.intValue = -1;
            else if (defaultTrack.intValue > list.index) defaultTrack.intValue--;
            ReorderableList.defaultBehaviours.DoRemoveButton(list);
        };
        playlist.onReorderCallbackWithDetails = (list, previous, current) =>
        {
            int selected = defaultTrack.intValue;
            if (selected == previous) defaultTrack.intValue = current;
            else if (previous < selected && selected <= current) defaultTrack.intValue--;
            else if (current <= selected && selected < previous) defaultTrack.intValue++;
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "tracks", "defaultTrack");
        playlist.DoLayoutList();
        var labels = new List<string> { "First playable track" };
        var indices = new List<int> { -1 };
        int invalid = 0;
        for (int index = 0; index < tracks.arraySize; index++)
        {
            var item = tracks.GetArrayElementAtIndex(index);
            var music = item.FindPropertyRelative("music").objectReferenceValue as JSAM.MusicFileObject;
            if (music == null || music.Files.Count != 1 || music.Files[0] == null) { invalid++; continue; }
            string title = item.FindPropertyRelative("title").stringValue;
            labels.Add($"{index + 1}. {(string.IsNullOrWhiteSpace(title) ? music.name : title)}");
            indices.Add(index);
        }
        if (!indices.Contains(defaultTrack.intValue)) defaultTrack.intValue = -1;
        defaultTrack.intValue = EditorGUILayout.IntPopup("Default Track", defaultTrack.intValue, labels.ToArray(), indices.ToArray());
        EditorGUILayout.HelpBox("This jukebox owns the playlist. The HUD generates its rows from this list. Blank display names use the music asset name. Changes apply to the prefab or scene you are editing.", MessageType.Info);
        if (invalid > 0) EditorGUILayout.HelpBox($"{invalid} track(s) will be skipped. Assign a JSAM Music File containing exactly one AudioClip so all players hear the same recording.", MessageType.Warning);
        if (indices.Count == 1) EditorGUILayout.HelpBox("No playable tracks: the area will keep the normal city music.", MessageType.Warning);
        serializedObject.ApplyModifiedProperties();
    }
}

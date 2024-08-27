/*
 * This class is intended to locate and remove any UIObject3D scenes which are no longer in use.
 * Sometimes UIObject3D scenes are not deleted if their cleanup process is interrupted, so this class will try and account for that.
 */
#region Namespace Imports
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
#endregion

namespace UI.ThreeDimensional
{
    [ExecuteInEditMode]
    public class UIObject3DSceneManager : MonoBehaviour
    {

        private void OnEnable()
        {
#if UNITY_EDITOR
#if UNITY_2017_2_OR_NEWER
            EditorApplication.playModeStateChanged += InEditorCleanup;
#else
            EditorApplication.playmodeStateChanged += InEditorCleanup;
#endif
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
#if UNITY_2017_2_OR_NEWER
            EditorApplication.playModeStateChanged -= InEditorCleanup;
#else
            EditorApplication.playmodeStateChanged -= InEditorCleanup;
#endif
#endif
        }

#if UNITY_EDITOR
#if UNITY_2017_2_OR_NEWER
        private void InEditorCleanup(PlayModeStateChange stateChange)
        {
            InEditorCleanup();
        }
#endif

        void InEditorCleanup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                var components = FindObjectsOfType<UIObject3DScene>();
                var objectsToRemove = new List<GameObject>();

                foreach (var component in components)
                {
                    if (component.UIObject3D == null // if we have a scene with no UIObject3D instance associated (e.g. it has been destroyed)
                     || component.UIObject3D.container != component.transform) // or if we have a UIObject3D instance, but that instance is no longer using this scene
                    {
                        objectsToRemove.Add(component.gameObject);
                        continue;
                    }
                }

                for (var x = objectsToRemove.Count - 1; x >= 0; x--)
                {
                    if (Application.isPlaying) Destroy(objectsToRemove[x]);
                    else DestroyImmediate(objectsToRemove[x]);
                }
            }
        }
#endif
    }
}
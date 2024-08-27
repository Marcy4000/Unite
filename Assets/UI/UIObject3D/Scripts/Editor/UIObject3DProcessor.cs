#region Namespace Imports
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
#endregion

namespace UI.ThreeDimensional
{    
    [InitializeOnLoad]
    public class UIObject3DProcessor: UnityEditor.AssetModificationProcessor
    {
        public static string[] OnWillSaveAssets(string[] paths)
        {
            CleanupAllObjects();

            if (!Application.isPlaying) DestroySceneContainers();
            
            return paths;
        }

        /*[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnAfterSceneLoadRuntimeMethod()
        {            
            var containers = GameObject.FindObjectsOfType<UIObject3DContainer>().ToList();

            // Error check - if, somehow, we have multiple scene containers,
            // cleanup and then continue
            if (containers.Count > 1)
            {
                DestroySceneContainers();
                CleanupAllObjects();
            }
        }*/

        private enum ePlayMode
        {
            Stopped,
            Playing,
            Paused            
        }
        private static ePlayMode PlayMode = ePlayMode.Stopped;

        [InitializeOnLoadMethod()]
        static void HandlePlayModeSwitches()
        {
#if UNITY_2017_3_OR_NEWER
            EditorApplication.playModeStateChanged += (a) =>
#else
            EditorApplication.playmodeStateChanged += () =>
#endif
            {
                var previousMode = PlayMode;

                if (EditorApplication.isPaused) PlayMode = ePlayMode.Paused;
                else
                {
                    if (EditorApplication.isPlaying)
                    {
                        PlayMode = ePlayMode.Playing;
                    }
                    else
                    {
                        PlayMode = ePlayMode.Stopped;
                    }
                }

                if (PlayMode == previousMode) return;
                if (PlayMode == ePlayMode.Paused) return;

                if (previousMode != ePlayMode.Paused)
                {
                    var containers = GameObject.FindObjectsOfType<UIObject3DContainer>().ToList();

                    // Error check - if, somehow, we have multiple scene containers,
                    // cleanup and then continue
                    if (containers.Count > 1)
                    {                        
                        DestroySceneContainers();
                        CleanupAllObjects();
                    }                                        
                }                
            };
        }        

        private static void CleanupAllObjects()
        {
            var objects = GameObject.FindObjectsOfType<UIObject3D>().ToList();

            foreach (var o in objects)
            {
                o.Cleanup();
            }
        }

        private static void DestroySceneContainers()
        {
            var containers = GameObject.FindObjectsOfType<UIObject3DContainer>().ToList();
            containers.ForEach(c => GameObject.DestroyImmediate(c.gameObject));            
        }
    }
}

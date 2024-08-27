#if UNITY_EDITOR
#region Namespace Imports
using UnityEngine;
using UnityEditor;
using System.Collections;
#endregion

namespace UI.ThreeDimensional
{    
    public static class UIObject3DLayerManager
    {        
        [InitializeOnLoadMethod]
        public static void ManageLayer()
        {            
            var layer = LayerMask.NameToLayer("UIObject3D");
            // layer already exists; nothing to do here
            if (layer != -1) return;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset"));
            var layers = tagManager.FindProperty("layers");

            if (layers == null || !layers.isArray)
            {
                Debug.LogWarning("[UIObject3D][Warning] Unable to set up layers. You can resolve this issue by manually adding a layer named 'UIObject3D'.");
                return;
            }

            bool set = false;

            // start off at 8 - layers 1 - 7 cannot be set here
            for (var i = 8; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);

                if (element.stringValue == "")
                {
                    element.stringValue = "UIObject3D";
                    set = true;
                    
                    // we're done
                    break;
                }
            }

            if (set)
            {
                Debug.Log("[UIObject3D] Layer 'UIObject3D' created.");
                tagManager.ApplyModifiedProperties();                
            }
            else
            {
                Debug.LogWarning("[UIObject3d][Warning] Unable to create Layer 'UIObject3D' - no blank layers found to replace! Please create the layer 'UIObject3d' manually in order to continue.");
            }
        }
    }
}
#endif

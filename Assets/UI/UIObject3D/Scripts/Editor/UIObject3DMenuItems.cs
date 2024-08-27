using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;


namespace UI.ThreeDimensional
{
    public class UIObject3DMenuItems
    {        
        [MenuItem("GameObject/UI/UIObject3D")]
        private static void NewUIObject3D()
        {                        
            Transform parent = UnityEditor.Selection.activeTransform;     

            if (parent == null || !(parent is RectTransform))
            {
                parent = GetCanvasTransform();
            }

            var prefabGUID = AssetDatabase.FindAssets("UIObject3D t:GameObject").FirstOrDefault();
            if (prefabGUID != null)
            {                
                var prefab = (GameObject)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(prefabGUID), typeof(GameObject));                

                var newUIObject3D = GameObject.Instantiate(prefab);

                newUIObject3D.name = "UIObject3D";
                newUIObject3D.transform.SetParent(parent);

                var transform = newUIObject3D.transform as RectTransform;

                transform.localPosition = Vector3.zero;
                transform.anchoredPosition3D = Vector3.zero;                
                transform.localScale = Vector3.one;
                transform.localRotation = Quaternion.identity;
                
                transform.SetParent(parent);

                transform.anchorMin = Vector2.zero;
                transform.anchorMax = Vector2.one;
                transform.offsetMin = Vector2.zero;
                transform.offsetMax = Vector2.zero;

                var uiObject3D = newUIObject3D.GetComponent<UIObject3D>();
              
                UIObject3DTimer.DelayedCall(0.01f, () => uiObject3D.HardUpdateDisplay(), uiObject3D);
            }
        }

        public static Transform GetCanvasTransform()
        {
            Canvas canvas = null;
#if UNITY_EDITOR
            // Attempt to locate a canvas object parented to the currently selected object
            if (!Application.isPlaying && UnityEditor.Selection.activeGameObject != null)
            {
                canvas = FindParentOfType<Canvas>(UnityEditor.Selection.activeGameObject);                
            }
#endif

            if (canvas == null)
            {
                // Attempt to find a canvas anywhere
                canvas = UnityEngine.Object.FindObjectOfType<Canvas>();

                if (canvas != null) return canvas.transform;
            }

            // if we reach this point, we haven't been able to locate a canvas
            // ...So I guess we'd better create one

            GameObject canvasGameObject = new GameObject("Canvas");
            canvasGameObject.layer = LayerMask.NameToLayer("UI");
            canvas = canvasGameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGameObject.AddComponent<CanvasScaler>();
            canvasGameObject.AddComponent<GraphicRaycaster>();

#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(canvasGameObject, "Create Canvas");
#endif

            var eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>();

            if (eventSystem == null)
            {
                GameObject eventSystemGameObject = new GameObject("EventSystem");
                eventSystem = eventSystemGameObject.AddComponent<EventSystem>();
                eventSystemGameObject.AddComponent<StandaloneInputModule>();

#if UNITY_4_6 || UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2
                eventSystemGameObject.AddComponent<TouchInputModule>();
#endif

#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(eventSystemGameObject, "Create EventSystem");
#endif
            }

            return canvas.transform;
        }

        public static T FindParentOfType<T>(GameObject childObject)
            where T : UnityEngine.Object
        {
            Transform t = childObject.transform;
            while (t.parent != null)
            {
                var component = t.parent.GetComponent<T>();

                if (component != null) return component;

                t = t.parent.transform;
            }

            // We didn't find anything
            return null;
        }
    }
}

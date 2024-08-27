/*
 * TODO:
 * 1) Change to 'targets' and allow multi object editing
 */
#region Namespace Imports
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#endregion

namespace UI.ThreeDimensional
{
    [CustomEditor(typeof(UIObject3DLight)), CanEditMultipleObjects]
    public class UIObject3DLightEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            base.OnInspectorGUI();

            if (!EditorGUI.EndChangeCheck()) return;

            targets.Cast<UIObject3DLight>()
                   .ToList()
                   .ForEach((l) =>
                   {
                       l.UpdateLight(true);
                   });
        }
    }
}

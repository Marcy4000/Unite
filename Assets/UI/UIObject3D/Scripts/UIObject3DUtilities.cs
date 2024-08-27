#region Namespace Imports
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#endregion

namespace UI.ThreeDimensional
{
    public static class UIObject3DUtilities
    {
        public static Vector3 NormalizeRotation(Vector3 rotation)
        {            
            return new Vector3(NormalizeAngle(rotation.x), NormalizeAngle(rotation.y), NormalizeAngle(rotation.z));
        }

        public static float NormalizeAngle(float value)
        {
            value = value % 360;

            if (value < 0)
            {
                value += 360;
            }            

            return value;
        }

        
        private static Dictionary<UIObject3D, Vector3> targetContainers = new Dictionary<UIObject3D, Vector3>();        

        internal static void RegisterTargetContainerPosition(UIObject3D uiObject3D, Vector3 position)
        {
            if (targetContainers.ContainsKey(uiObject3D))
            {                
                return;
            }

            targetContainers.Add(uiObject3D, position);
        }

        internal static Vector3 GetTargetContainerPosition(UIObject3D uiObject3d)
        {
            if (targetContainers.ContainsKey(uiObject3d)) return targetContainers[uiObject3d];

            return GetNextFreeTargetContainerPosition();
        }

        internal static Vector3 GetNextFreeTargetContainerPosition()
        {
            if (!targetContainers.Any()) return Vector3.zero;

            var lastXInUse = targetContainers.Max(v => v.Value.x);

            return new Vector3(lastXInUse + 250f, 0f, 0f);
        }

        internal static void UnRegisterTargetContainer(UIObject3D uiObject3D)
        {
            targetContainers.Remove(uiObject3D);
        }
    }
}

#region Namespace Imports
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif
#endregion

namespace UI.ThreeDimensional
{
    /// <summary>
    /// Subclass of the Unity 'Image' component which avoids the default layout sizing behaviour
    /// </summary>
    [RequireComponent(typeof(RectTransform)), DisallowMultipleComponent, ExecuteInEditMode]    
    public class UIObject3DImage : Image, ILayoutElement
    {        
        void ILayoutElement.CalculateLayoutInputHorizontal()
        {            
        }

        void ILayoutElement.CalculateLayoutInputVertical()
        {            
        }

        float ILayoutElement.flexibleHeight
        {
            get { return 1; }
        }

        float ILayoutElement.flexibleWidth
        {
            get { return 1; }
        }

        int ILayoutElement.layoutPriority
        {
            get { return -1; }
        }

        float ILayoutElement.minHeight
        {
            get { return 0; }
        }

        float ILayoutElement.minWidth
        {
            get { return 0; }
        }

        float ILayoutElement.preferredHeight
        {
            get { return 0; }
        }

        float ILayoutElement.preferredWidth
        {
            get { return 0; }
        }        
    }
}

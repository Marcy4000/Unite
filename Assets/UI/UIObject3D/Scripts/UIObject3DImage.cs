#region Namespace Imports
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
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
        private static Material captureMaterial;

        // Image.material still takes precedence when the user explicitly assigns a
        // custom UI material. Resources keeps this shader available in player builds.
        public override Material defaultMaterial
        {
            get
            {
                if (captureMaterial == null)
                {
                    var shader = Resources.Load<Shader>("UIObject3DPremultiplied");
                    if (shader == null) return base.defaultMaterial;
                    captureMaterial = new Material(shader)
                    {
                        name = "UIObject3D Capture (Premultiplied)",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
                return captureMaterial;
            }
        }

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

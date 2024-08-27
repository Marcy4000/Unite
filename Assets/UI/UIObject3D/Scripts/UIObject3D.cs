#region Namespace Imports
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering.Universal;


#if UNITY_EDITOR
using UnityEditor;
#endif
#endregion

namespace UI.ThreeDimensional
{
    [RequireComponent(typeof(RectTransform)), DisallowMultipleComponent, ExecuteInEditMode]
    [AddComponentMenu("UI/UIObject3D/UIObject3D")]
    public class UIObject3D : MonoBehaviour
    {
        [Header("Target"), SerializeField]
        private Transform _ObjectPrefab = null;
        /// <summary>
        /// Reference to the prefab / model / etc. that this instance of UIObject3D will render
        /// Note: setting this property will call HardUpdateDisplay(), even if the value has not changed.
        /// </summary>
        public Transform ObjectPrefab
        {
            get { return _ObjectPrefab; }
            set
            {
                _ObjectPrefab = value;
                HardUpdateDisplay();
            }
        }

        public bool UseTargetRotation = true;

        [SerializeField]
        private Vector3 _TargetRotation = Vector3.zero;
        /// <summary>
        /// A rotation value to apply to the model rendered by UIObject3D
        /// </summary>
        public Vector3 TargetRotation
        {
            get { return _TargetRotation; }
            set
            {
                _TargetRotation = UIObject3DUtilities.NormalizeRotation(value);

                UpdateDisplay();
            }
        }

        [SerializeField, Range(-10, 10)]
        private float _TargetOffsetX = 0f;
        [SerializeField, Range(-10, 10)]
        private float _TargetOffsetY = 0f;

        /// <summary>
        /// An offset (X/Y) to apply to the target (relative to its default location of 0,0)
        /// </summary>
        [SerializeField]
        public Vector2 TargetOffset
        {
            get { return new Vector2(_TargetOffsetX, _TargetOffsetY); }
            set
            {
                _TargetOffsetX = value.x;
                _TargetOffsetY = value.y;
                UpdateDisplay();
            }
        }

        [SerializeField, Tooltip("By default, the target object will be scaled automatically by UIObject3D to fit within the viewable area. This option allows you to override that behaviour and set the scaling value manually.")]
        private bool _OverrideCalculatedTargetScale = false;
        /// <summary>
        /// By default, the target object will be scaled automatically by UIObject3D to fit within the viewable area.
        /// This option allows you to override that behaviour and set the scaling value manually.
        /// </summary>
        public bool OverrideCalculatedTargetScale
        {
            get { return _OverrideCalculatedTargetScale; }
            set
            {
                _OverrideCalculatedTargetScale = value;
                UpdateDisplay();
            }
        }

        [SerializeField]
        private float _CalculatedTargetScaleOverride = 1f;
        /// <summary>
        /// By default, this value is calculated automatically by UIObject3D.
        /// However, if 'OverrideCalculatedTargetScale' is set to true, then UIObject3D will no longer change this value and will use whatever value you have provided.
        /// </summary>
        public float CalculatedTargetScaleOverride
        {
            get { return _CalculatedTargetScaleOverride; }
            set
            {
                _CalculatedTargetScaleOverride = value;
                UpdateDisplay();
            }
        }

        [Header("Camera Settings"), SerializeField, Range(20, 100)]
        private float _CameraFOV = 35f;
        /// <summary>
        /// This property allows you to increase or decrease the FOV (Field of View) of the camera used to render the target.
        /// </summary>
        public float CameraFOV
        {
            get { return _CameraFOV; }
            set
            {
                _CameraFOV = value;
                UpdateDisplay();
            }
        }

        [SerializeField, Range(-10, -1)]
        private float _CameraDistance = -3.5f;
        /// <summary>
        /// This property allows you to move the camera closer or further away from the target.
        /// </summary>
        public float CameraDistance
        {
            get { return _CameraDistance; }
            set
            {
                _CameraDistance = value;
                UpdateDisplay();
            }
        }

        [SerializeField, Tooltip("If this property is set, and the target has an offset, then the camera will turn to face it.")]
        private bool _AlwaysLookAtTarget = false;
        /// <summary>
        /// If this property is set to true, and the target has an offset, then the camera will turn to face it.
        /// </summary>
        public bool AlwaysLookAtTarget
        {
            get { return _AlwaysLookAtTarget; }
            set
            {
                _AlwaysLookAtTarget = value;

                UpdateDisplay();
            }
        }

        [SerializeField, HideInInspector]
        private Vector2 _textureSize = default(Vector2);
        /// <summary>
        /// Readonly property that is used to determine the size of the texture rendered by UIObject3D
        /// (Affected by 'RenderScale')
        /// </summary>
        public Vector2 TextureSize
        {
            get
            {
                if (_textureSize != default(Vector2)) return _textureSize;

                if (target != null)
                {
                    Vector2 size = new Vector2(Mathf.Abs(Mathf.Floor(rectTransform.rect.width)), Mathf.Abs(Mathf.Floor(rectTransform.rect.height))) * RenderScale;

                    if (size.x == 0 || size.y == 0) size = new Vector2(256, 256);

                    _textureSize = size;

                    return size;
                }

                return Vector2.one;
            }
        }

        [SerializeField]
        private Color _BackgroundColor = Color.clear;
        /// <summary>
        /// A background color to render behind the model.
        /// By default, this is fully transparent.
        /// </summary>
        public Color BackgroundColor
        {
            get { return _BackgroundColor; }
            set
            {
                _BackgroundColor = value;

                UpdateDisplay();
            }
        }

        [SerializeField, Tooltip("Enabling this option may help prevent ghosting issues - although it may cause flickering on some rendering devices, such as Metal on iOS.")]
        public bool ClearGLBufferBeforeRendering = false;

        /// <summary>
        /// If set to true, then UIObject3D will only render at up to 'FrameRateLimit' times per second.
        /// </summary>
        [Header("Performance"), SerializeField, Tooltip("Should this UIObject3D limit itself to a particular framerate?")]
        public bool LimitFrameRate = false;

        /// <summary>
        /// The maximum number of frames per second to render at, if 'LimitFrameRate' is true.
        /// </summary>
        [SerializeField, Tooltip("Maximum number of frames to render per second.")]
        public float FrameRateLimit = 30f;

        /// <summary>
        /// If this is enabled, then this UIObject3D will render every frame (optionally limited by FrameRateLimit) even if none of the UIObject3D properties change.
        /// This should only be enabled if your target has animations of its own which are not controlled by UIObject3D.
        /// </summary>
        [Tooltip("If this is enabled, then this UIObject3D will render every frame (optionally limited by FrameRateLimit) even if none of the UIObject3D properties change. This should only be enabled if your target has animations of its own which are not controlled by UIObject3D.")]
        public bool RenderConstantly = false;

        [SerializeField, Tooltip("Set this to a lower value to have UIObject3D render at a lower resolution, or higher to have UIObject3D render at a higher resolution.")]
        private float _RenderScale = 1.0f;
        /// <summary>
        /// Set this to a lower value to have UIObject3D render at a lower resolution, or higher to have UIObject3D render at a higher resolution.
        /// (Default value == 1)
        /// </summary>
        public float RenderScale
        {
            get { return _RenderScale; }
            set
            {
                _RenderScale = value;
                HardUpdateDisplay();
            }
        }

        internal float timeBetweenFrames
        {
            get { return 1f / FrameRateLimit; }
        }

        private float timeSinceLastRender = 0f;



        [Header("Lighting"), SerializeField]
        private bool _EnableCameraLight = false;
        /// <summary>
        /// If this is set to true, then a light will be added on the camera object.
        /// </summary>
        public bool EnableCameraLight
        {
            get { return _EnableCameraLight; }
            set
            {
                _EnableCameraLight = value;

                UpdateDisplay();
            }
        }

        [SerializeField]
        private Color _LightColor = Color.white;
        /// <summary>
        /// Specifies the color to use for the camera light.
        /// </summary>
        public Color LightColor
        {
            get { return _LightColor; }
            set
            {
                _LightColor = value;
                UpdateDisplay();
            }
        }

        [SerializeField, Range(0, 8)]
        private float _LightIntensity = 1f;
        /// <summary>
        /// Specifies the intensity for the camera light.
        /// </summary>
        public float LightIntensity
        {
            get { return _LightIntensity; }
            set
            {
                _LightIntensity = value;
                UpdateDisplay();
            }
        }

        /// <summary>
        /// Event to be called whenever the target has been updated.
        /// (Used internally, but you can add your own listeners if necessary)
        /// </summary>
        [SerializeField]
        public UnityEngine.Events.UnityEvent OnUpdateTarget = new UnityEngine.Events.UnityEvent();

        [NonSerialized]
        private bool started = false;
        [NonSerialized]
        private bool hardUpdateQueued = false;
        [NonSerialized]
        private bool renderQueued = false;
        [NonSerialized]
        private Bounds targetBounds;

        private static bool copyTextureSupportedPopulated = false;
        private static bool _copyTextureSupported = false;
        private static bool copyTextureSupported
        {
            get
            {
                if (!copyTextureSupportedPopulated)
                {
                    _copyTextureSupported = (SystemInfo.copyTextureSupport & UnityEngine.Rendering.CopyTextureSupport.RTToTexture) == UnityEngine.Rendering.CopyTextureSupport.RTToTexture;
                    copyTextureSupportedPopulated = true;
                }

                return _copyTextureSupported;
            }
        }

        //private bool _enabled = false;

        void DestroyResources()
        {
            if (_targetCamera != null) _targetCamera.targetTexture = null;
            if (_texture2D != null) _Destroy(_texture2D);
            if (_sprite != null) _Destroy(_sprite);
            if (_renderTexture != null) _Destroy(_renderTexture);
        }

        /// <summary>
        /// Clear all textures/etc. destroy the current target objects, and then start from scratch.
        /// Necessary, if, for example, the RectTransform size has changed.
        /// Fairly performance-intensive - only call this if strictly necessary.
        /// </summary>
        public void HardUpdateDisplay()
        {
            var color = imageComponent.color;
            if (Application.isPlaying)
            {
                imageComponent.color = new Color(0, 0, 0, 0);
                //imageComponent.sprite = null;
            }

            DestroyResources();

            Cleanup();

            //UpdateDisplay();
            UIObject3DTimer.AtEndOfFrame(() => UpdateDisplay(), this);
            UIObject3DTimer.DelayedCall(0.05f, () => { imageComponent.color = color; }, this, true);
        }

        private void _Destroy(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        /// <summary>
        /// Unity's Start() method. Used for initialization.
        /// </summary>
        void Start()
        {
            var color = imageComponent.color;
            if (Application.isPlaying)
            {
                imageComponent.color = new Color(0, 0, 0, 0);
                //imageComponent.sprite = null;
            }

            UIObject3DTimer.AtEndOfFrame(() => SetStarted(), this, true);
            UIObject3DTimer.AtEndOfFrame(() => OnEnable(), this);

            // Some models (particularly, models with rigs) can cause Unity to crash if they are instantiated this early (for some reason)
            // as such, we must delay very briefly to avoid this before rendering
            UIObject3DTimer.DelayedCall(0.01f, () =>
            {
                Cleanup();
                UpdateDisplay();

                UIObject3DTimer.DelayedCall(0.05f, () => { imageComponent.color = color; }, this, true);
            }, this, true);
        }

        /// <summary>
        /// For internal use only. Public so as to be accessible within Editor-only code.
        /// </summary>
        public void SetStarted()
        {
            started = true;
        }

        /// <summary>
        /// Update the target / camera / etc. to match  the configuration values,
        /// then queue a render at the end of the frame (or, optionally, render instantly)
        /// </summary>
        /// <param name="instantRender"></param>
        public void UpdateDisplay(bool instantRender = false)
        {
            if (!Application.isPlaying && !started)
            {
                Start();
                SetStarted();
            }

            if (!started) return;

            Prepare();

            UpdateTargetPositioningAndScale();
            UpdateTargetCameraPositioningEtc();

            if (OnUpdateTarget != null)
            {
                OnUpdateTarget.Invoke();
            }

            Render(instantRender);
        }

        /// <summary>
        /// Unity's 'OnEnable' method. Handles some initialization.
        /// </summary>
        void OnEnable()
        {
            // If Start hasn't been called yet, then this has been called too early.
            // Start() will call this when it is time
            if (!started) return;

            //_enabled = true;

            if (objectLayer != -1)
            {
                ClearObjectLayerFromCameras();
                ClearObjectLayerFromLights();
            }

            UIObject3DTimer.AtEndOfFrame(() => UpdateDisplay(true), this);

#if UNITY_EDITOR
#if UNITY_2017_2_OR_NEWER
            EditorApplication.playModeStateChanged += InEditorCleanup;
#else
            EditorApplication.playmodeStateChanged += InEditorCleanup;
#endif
#endif
        }

#if UNITY_EDITOR && UNITY_2017_2_OR_NEWER
        private void InEditorCleanup(PlayModeStateChange stateChange)
        {
            InEditorCleanup();
        }
#endif

        private void ClearObjectLayerFromCameras()
        {
            // remove our object layer from any other cameras we find
            var otherCameras = GameObject.FindObjectsOfType<Camera>();
            foreach (var c in otherCameras)
            {
                // don't modify the culling mask for other UIObject3DCameras
                if (c.GetComponent<UIObject3DCamera>() != null) continue;


                c.cullingMask &= ~(1 << objectLayer);
            }
        }

        private void ClearObjectLayerFromLights()
        {
            // remove object layer from any lights
            var otherLights = GameObject.FindObjectsOfType<Light>();
            foreach (var l in otherLights)
            {
                // ignore directional lights; the user may wish the object to be affected by directional lights. If they don't, then they can manually adjust the culling mask
                if (l.type == LightType.Directional) continue;
                // don't modify the culling mask for UIObject3D lights
                if (l.name == "UIObject3DLight") continue;
                // don't modify the culling mask for lights attached to other UIObject3DCameras
                if (l.GetComponent<UIObject3DCamera>() != null) continue;

                l.cullingMask &= ~(1 << objectLayer);
            }
        }

        /// <summary>
        /// Unity's 'OnDisable' method. Used to clean up scene objects that are not needed when UIObject3D is disabled.
        /// </summary>
        void OnDisable()
        {
            //_enabled = false;

            Cleanup();


#if UNITY_EDITOR
#if UNITY_2017_2_OR_NEWER
            EditorApplication.playModeStateChanged -= InEditorCleanup;
#else
            EditorApplication.playmodeStateChanged -= InEditorCleanup;
#endif
#endif
        }

#if UNITY_EDITOR
        void InEditorCleanup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
            {
                Cleanup();

                UIObject3DTimer.AtEndOfFrame(() =>
                {
                    Cleanup();
                }, this, true);
            }
        }
#endif

        void OnDestroy()
        {
            UIObject3DUtilities.UnRegisterTargetContainer(this);
        }

        void Prepare()
        {
            if (imageComponent.sprite != sprite) imageComponent.sprite = sprite;

            SetupTargetCamera();
        }

        /// <summary>
        /// Clear references to textures and delete the target objects.
        /// </summary>
        public void Cleanup()
        {
            _texture2D = null;
            _sprite = null;
            _renderTexture = null;

            _target = null;
            _targetContainer = null;

            targetBounds = default(Bounds);
            _textureSize = default(Vector2);

            if (_container != null)
            {
                UIObject3DUtilities.UnRegisterTargetContainer(this);

                if (Application.isPlaying)
                {
                    Destroy(_container.gameObject);
                }
                else
                {
                    DestroyImmediate(_container.gameObject);
                }

                _container = null;
            }
        }

        /// <summary>
        /// Get a reference to the current target used by this UIObject3D
        /// You can use this, for example, to access components on your prefab instance
        /// and call methods to trigger animations/etc.
        /// Please note, if you do use animations on the target object, you will need to set 'RenderConstantly' to true (at least, for the duration of the animation)
        /// </summary>
        /// <returns></returns>
        public Transform GetTargetInstance()
        {
            return target;
        }

        internal void Render(bool instant = false)
        {
            if (Application.isPlaying && !instant)
            {
                renderQueued = true;
                return;
            }

            if (targetCamera == null) return;

            RenderTexture previousRenderTexture = RenderTexture.active;
            if (!copyTextureSupported) RenderTexture.active = this.renderTexture;

            // If we don't manually clear the buffer, we end up with a copy of the target in the background
            if (ClearGLBufferBeforeRendering)
            {
                GL.Clear(true, true, BackgroundColor);
            }

            if (targetCamera.targetTexture != this.renderTexture) targetCamera.targetTexture = this.renderTexture;
            targetCamera.Render();


            if (copyTextureSupported)
            {
                Graphics.CopyTexture(renderTexture, texture2D);
            }
            else
            {
                var rect = new Rect(0, 0, (int)TextureSize.x, (int)TextureSize.y);
                this.texture2D.ReadPixels(rect, 0, 0);
                this.texture2D.Apply();
            }

            if (!copyTextureSupported) RenderTexture.active = previousRenderTexture;

            renderQueued = false;
        }

        /*
         * As of Unity 2017.2, there seems to be a bug which calls this method
         * repeatedly when UIObject3D is nested within a layout group, which causes all sorts of problems.
         * As such, I have decided to remove this method for now; the primary downside is that
         * resizing a UIObject3D instance will no longer resize the texture. In most scenarios,
         * this will not be noticeable. Leaving the method out increases performance markedly,
         * as resizing the texture/etc. is very expensive.
        void OnRectTransformDimensionsChange()
        {
        }
        */

        /// <summary>
        /// Unity's Update() method. Called every frame.
        /// </summary>
        void Update()
        {
            if (!Application.isPlaying) return;
            if (!started) return;

            timeSinceLastRender += Time.unscaledDeltaTime;

            if (hardUpdateQueued)
            {
                hardUpdateQueued = false;

                HardUpdateDisplay();
                return;
            }

            if (LimitFrameRate)
            {
                if (timeSinceLastRender < timeBetweenFrames) return;
            }

            if (renderQueued || RenderConstantly)
            {
                Render(true);
                timeSinceLastRender = 0f;
            }
        }

        #region Internal Components
        private RectTransform _rectTransform;
        protected RectTransform rectTransform
        {
            get
            {
                if (_rectTransform == null) _rectTransform = this.GetComponent<RectTransform>();
                return _rectTransform;
            }
        }

        [SerializeField, HideInInspector]
        private UIObject3DImage _imageComponent;
        /// <summary>
        /// An image component which renders the sprite created by UIObject3D
        /// </summary>
        public UIObject3DImage imageComponent
        {
            get
            {
                bool setProperties = false;
                if (_imageComponent == null)
                {
                    _imageComponent = this.GetComponent<UIObject3DImage>();
                    setProperties = true;
                }

                if (_imageComponent == null)
                {
                    _imageComponent = this.gameObject.AddComponent<UIObject3DImage>();
                    setProperties = true;
                }

                if (setProperties)
                {
                    _imageComponent.type = Image.Type.Simple;
                    _imageComponent.preserveAspect = true;
                }

                return _imageComponent;
            }
        }

        private Texture2D _texture2D;
        protected Texture2D texture2D
        {
            get
            {
                if (_texture2D == null) _texture2D = new Texture2D((int)TextureSize.x, (int)TextureSize.y, TextureFormat.ARGB32, false, false);

                return _texture2D;
            }
        }

        private Sprite _sprite;
        protected Sprite sprite
        {
            get
            {
                if (_sprite == null)
                {
                    _sprite = Sprite.Create(texture2D, new Rect(0, 0, (int)TextureSize.x, (int)TextureSize.y), new Vector2(0.5f, 0.5f));
                }

                return _sprite;
            }
        }

        private RenderTexture _renderTexture;
        protected RenderTexture renderTexture
        {
            get
            {
                if (_renderTexture == null)
                {
#if UNITY_2019_OR_NEWER
                    _renderTexture = new RenderTexture((int)TextureSize.x, (int)TextureSize.y, 16, RenderTextureFormat.Default);
#else
                    _renderTexture = new RenderTexture((int)TextureSize.x, (int)TextureSize.y, 16, RenderTextureFormat.ARGB32);
#endif

#if !UNITY_2020_2_OR_NEWER // there appears to be a bug in Unity 2020.2 which breaks things when using anti-aliasing
                    // Use anti-aliasing as per quality settings
                    if (QualitySettings.antiAliasing > 0) _renderTexture.antiAliasing = QualitySettings.antiAliasing;
#endif
                    if (QualitySettings.anisotropicFiltering > 0) _renderTexture.anisoLevel = (int)QualitySettings.anisotropicFiltering;

                    _renderTexture.filterMode = FilterMode.Trilinear;
                    _renderTexture.useMipMap = false;
                }

                return _renderTexture;
            }
        }

        private static Transform _parentContainer;
        private static Transform parentContainer
        {
            get
            {
                if (_parentContainer == null)
                {
                    var go = GameObject.Find("UIObject3D Scenes");

                    if (go != null)
                    {
                        _parentContainer = go.transform;
                    }
                    else
                    {
                        _parentContainer = new GameObject().transform;
                        _parentContainer.name = "UIObject3D Scenes";
                    }

                    if (_parentContainer.GetComponent<UIObject3DSceneManager>() == null)
                    {
                        _parentContainer.gameObject.AddComponent<UIObject3DSceneManager>();
                    }
                }

                return _parentContainer;
            }
        }

        private Transform _container;
        internal Transform container
        {
            get
            {
                if (_container == null)
                {
                    if (ObjectPrefab == null) return null;

                    _container = new GameObject().transform;
                    _container.SetParent(parentContainer);
                    _container.position = Vector3.zero;
                    _container.localScale = Vector3.one;
                    _container.localRotation = Quaternion.identity;
                    _container.gameObject.layer = objectLayer;
                    _container.name = "__UIObject3D_" + ObjectPrefab.name;

                    _container.localPosition = UIObject3DUtilities.GetTargetContainerPosition(this);
                    var scene = _container.gameObject.AddComponent<UIObject3DScene>();
                    scene.UIObject3D = this;

                    UIObject3DUtilities.RegisterTargetContainerPosition(this, _container.localPosition);
                }

                return _container;
            }
        }

        private Transform _targetContainer;
        internal Transform targetContainer
        {
            get
            {
                if (_targetContainer == null)
                {
                    if (container == null) return null;

                    _targetContainer = new GameObject().transform;
                    _targetContainer.SetParent(container);

                    _targetContainer.localPosition = Vector3.zero;
                    _targetContainer.localScale = Vector3.one;
                    _targetContainer.localRotation = Quaternion.identity;
                    _targetContainer.name = "Target Container";
                    _targetContainer.gameObject.layer = objectLayer;
                }

                return _targetContainer;
            }
        }

        private Transform _target;
        protected Transform target
        {
            get
            {
                if (_target == null && started) SetupTarget();

                return _target;
            }
        }

        public GameObject TargetGameObject
        {
            get
            {
                if (target == null) return null;

                return target.gameObject;
            }
        }

        private void SetupTarget()
        {
            if (_target == null)
            {
                if (ObjectPrefab == null)
                {
                    if (Application.isPlaying) Debug.LogWarning("[UIObject3D] No prefab set.");
                    return;
                }

                _target = GameObject.Instantiate(ObjectPrefab);
            }

            UpdateTargetPositioningAndScale();
        }

        /// <summary>
        /// Call this method if you've updated your target object (e.g. a model in your scene)
        /// and you want UIObject3D's copy to be updated to match.
        /// Performs a small cleanup and then updates and schedules a render.
        /// (Has less of a performance hit than HardUpdateDisplay())
        /// </summary>
        public void RefreshTarget()
        {
            if (_target != null) Cleanup();

            UIObject3DTimer.AtEndOfFrame(() => UpdateDisplay(), this);
        }

        private void UpdateTargetPositioningAndScale()
        {
            if (_target == null) return;
            var renderer = _target.GetComponentInChildren<Renderer>();

            _target.name = "Target";

            bool initial = targetBounds == default(Bounds);

            if (initial)
            {
                // if our "Prefab" has no children, it is almost certainly a model
                var prefabIsModel = false; // ObjectPrefab.childCount == 0;
                if (ObjectPrefab.gameObject.scene.name != "Null") prefabIsModel = true;

                _target.transform.SetParent(targetContainer);

                if (prefabIsModel)
                {
                    // Models can have strange default positions/etc.
                    // better to just correct that here
                    _target.transform.localPosition = Vector3.zero;
                    _target.transform.localScale = Vector3.one;
                    _target.localRotation = Quaternion.identity;
                }
                else
                {
                    // if we're dealing with a prefab, then preserve the position/scale/rotation
                    // as defined by that prefab
                    _target.transform.localPosition = ObjectPrefab.localPosition;
                    _target.transform.localScale = ObjectPrefab.localScale;
                    _target.transform.localRotation = ObjectPrefab.localRotation;
                }

                SetLayerRecursively(_target.transform, objectLayer);
            }


            if (renderer != null)
            {
                if (initial)
                {
                    var storedPosition = _target.transform.localPosition;
                    _target.transform.position = Vector3.zero;
                    targetBounds = new Bounds(renderer.bounds.center, renderer.bounds.size);
                    _target.transform.localPosition = storedPosition;

                    // debug code to visualize the target container
                    // var debugCollider = targetContainer.gameObject.AddComponent<BoxCollider>();
                    // debugCollider.size = targetBounds.size;
                    // end debug code

                    // this helps correct models with off-center pivots
                    _target.transform.localPosition -= targetBounds.center;
                }

                if (!OverrideCalculatedTargetScale)
                {
                    var frustumHeight = 2 * 2 * Math.Tan(targetCamera.fieldOfView * 0.5 * Mathf.Deg2Rad);
                    var frustumWidth = frustumHeight * targetCamera.aspect;

                    double scale = 1f / Math.Max(targetBounds.size.x, targetBounds.size.y);

                    var wideObject = targetBounds.size.x > targetBounds.size.y;
                    var tallObject = targetBounds.size.y > targetBounds.size.x;

                    if (wideObject)
                    {
                        scale = frustumWidth / targetBounds.size.x;
                    }
                    else if (tallObject)
                    {
                        scale = frustumHeight / targetBounds.size.y;
                    }

                    // now check to see if the new size exceeds the camera frustrum
                    var newHeight = targetBounds.size.y * scale;
                    var newWidth = targetBounds.size.x * scale;

                    var newWidthIsHigher = newWidth > frustumWidth;
                    var newHeightIsHigher = newHeight > frustumHeight;

                    if (newWidthIsHigher)
                    {
                        scale = frustumWidth / targetBounds.size.x;
                    }

                    if (newHeightIsHigher)
                    {
                        scale = frustumHeight / targetBounds.size.y;
                    }


                    targetContainer.transform.localScale = Vector3.one * (float)scale;

                    //
                    _CalculatedTargetScaleOverride = (float)scale;
                }
                else
                {
                    targetContainer.transform.localScale = Vector3.one * CalculatedTargetScaleOverride;
                }
            }

            targetContainer.transform.localPosition = new Vector3(TargetOffset.x, TargetOffset.y, 0);
            if (UseTargetRotation) targetContainer.transform.localEulerAngles = TargetRotation;
        }

        private void SetLayerRecursively(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;

            foreach (Transform t in transform)
            {
                SetLayerRecursively(t, layer);
            }
        }

        private Camera _targetCamera;
        protected Camera targetCamera
        {
            get
            {
                if (_targetCamera == null) SetupTargetCamera();

                return _targetCamera;
            }
        }

        private void SetupTargetCamera()
        {
            if (_targetCamera == null)
            {
                if (ObjectPrefab == null) return;

                var cameraGO = new GameObject();
                cameraGO.transform.SetParent(container);
                _targetCamera = cameraGO.AddComponent<Camera>();
                _targetCamera.enabled = false;
                _targetCamera.allowHDR = false;
                _targetCamera.GetUniversalAdditionalCameraData().renderShadows = false;

                cameraGO.AddComponent<UIObject3DCamera>();
            }

            UpdateTargetCameraPositioningEtc();
        }

        private Light _cameraLight;
        protected Light cameraLight
        {
            get
            {
                if (_cameraLight == null) SetupCameraLight();

                return _cameraLight;
            }
        }

        private void SetupCameraLight()
        {
            if (targetCamera == null) return;

            if (_cameraLight == null) _cameraLight = targetCamera.gameObject.AddComponent<Light>();


            _cameraLight.enabled = EnableCameraLight;

            if (EnableCameraLight)
            {
                _cameraLight.gameObject.layer = objectLayer;
                _cameraLight.cullingMask = LayerMask.GetMask(LayerMask.LayerToName(objectLayer));
                _cameraLight.type = LightType.Point;
                _cameraLight.intensity = LightIntensity;
                _cameraLight.range = 200;
                _cameraLight.color = LightColor;
                _cameraLight.bounceIntensity = 0;
            }
        }

        private void UpdateTargetCameraPositioningEtc()
        {
            if (_targetCamera == null) return;

            _targetCamera.transform.localPosition = Vector3.zero + new Vector3(0, 0, CameraDistance);

            if (AlwaysLookAtTarget)
            {
                _targetCamera.transform.LookAt(_target);
            }
            else
            {
                _targetCamera.transform.rotation = Quaternion.identity;
            }

            _targetCamera.name = "Camera";

            _targetCamera.targetTexture = this.renderTexture;
            _targetCamera.clearFlags = CameraClearFlags.SolidColor;
            _targetCamera.backgroundColor = Color.clear;
            _targetCamera.nearClipPlane = 0.1f;
            _targetCamera.farClipPlane = 50f;

            _targetCamera.fieldOfView = CameraFOV;

            _targetCamera.gameObject.layer = objectLayer;
            _targetCamera.cullingMask = LayerMask.GetMask(LayerMask.LayerToName(objectLayer));

            _targetCamera.backgroundColor = BackgroundColor;

            SetupCameraLight();
        }

        private static int _objectLayer = -1;
        internal static int objectLayer
        {
            get
            {
                if (_objectLayer == -1)
                {
                    _objectLayer = LayerMask.NameToLayer("UIObject3D");
#if UNITY_EDITOR
                    if (_objectLayer == -1)
                    {
                        UIObject3DLayerManager.ManageLayer();
                        _objectLayer = LayerMask.NameToLayer("UIObject3D");
                    }
#endif
                }

                return _objectLayer;
            }
        }
#endregion
    }
}
#region Namespace Imports
using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#endregion

namespace UI.ThreeDimensional
{
    [RequireComponent(typeof(UIObject3D)), ExecuteInEditMode]
    [AddComponentMenu("UI/UIObject3D/UIObject3D Light")]
    public class UIObject3DLight : MonoBehaviour
    {
        [SerializeField]
        private Vector3 _LightPosition = new Vector3(0, 0, -2.5f);
        public Vector3 LightPosition
        {
            get { return _LightPosition; }
            set
            {
                _LightPosition = value;
                SetLightPosition(true);
            }
        }

        [SerializeField]
        private Color _LightColor = Color.white;
        public Color LightColor
        {
            get { return _LightColor; }
            set
            {
                _LightColor = value;
                SetLightProperties(true);
            }
        }

        [SerializeField, Range(0, 8)]
        private float _LightIntensity = 1f;
        public float LightIntensity
        {
            get { return _LightIntensity; }
            set
            {
                _LightIntensity = value;
                SetLightProperties(true);
            }
        }

        [NonSerialized]
        private UIObject3D UIObject3D;

        [NonSerialized]
        private Light _lightObject = null;
        private Light lightObject
        {
            get
            {
                if (_lightObject == null) SpawnLight();

                return _lightObject;
            }
            set { _lightObject = value; }
        }

        private void OnEnable()
        {
            if (UIObject3D == null) UIObject3D = GetComponent<UIObject3D>();

            UIObject3D.OnUpdateTarget.AddListener(UpdateLightEvent);

            lightObject.enabled = true;
            UpdateLight(true);
        }

        private void OnDisable()
        {
            UIObject3D.OnUpdateTarget.RemoveListener(UpdateLightEvent);

            if (_lightObject != null)
            {
                lightObject.enabled = false;
                ScheduleRender();
            }
        }

        private void UpdateLightEvent()
        {
            UpdateLight(true);
        }

        public void UpdateLight(bool scheduleRender = false)
        {
            if (!enabled) return;

            if (lightObject == null)
            {
                SpawnLight();
            }

            SetLightPosition(false);
            SetLightProperties(false);

            if (scheduleRender) ScheduleRender();
        }

        void SpawnLight()
        {
            var lightGO = new GameObject("UIObject3DLight", typeof(Light));
            _lightObject = lightGO.GetComponent<Light>();

            _lightObject.transform.localScale = Vector3.one;
            _lightObject.transform.SetParent(UIObject3D.container.gameObject.transform);

            _lightObject.range = 200;
            _lightObject.cullingMask = LayerMask.GetMask(LayerMask.LayerToName(UIObject3D.objectLayer));
            _lightObject.type = LightType.Point;
            _lightObject.bounceIntensity = 0;
        }

        void SetLightPosition(bool scheduleRender = true)
        {
            lightObject.transform.localPosition = LightPosition;

            if (scheduleRender) ScheduleRender();
        }

        void SetLightProperties(bool scheduleRender = true)
        {
            lightObject.intensity = LightIntensity;
            lightObject.color = LightColor;

            if (scheduleRender) ScheduleRender();
        }

        void ScheduleRender()
        {
            if (UIObject3D == null || !enabled) return;

            if (!Application.isPlaying)
            {
                UIObject3DTimer.AtEndOfFrame(() =>
                {
                    UIObject3D.OnUpdateTarget.RemoveListener(UpdateLightEvent);
                    UIObject3D.UpdateDisplay();
                    UIObject3D.OnUpdateTarget.AddListener(UpdateLightEvent);
                }, this);
            }
            else
            {
                UIObject3D.Render();
            }
        }
    }
}

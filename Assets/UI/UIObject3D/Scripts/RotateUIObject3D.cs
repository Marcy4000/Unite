#region Namespace Imports
using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#endregion

namespace UI.ThreeDimensional
{
    [RequireComponent(typeof(UIObject3D))]
    [AddComponentMenu("UI/UIObject3D/Rotate UIObject3D")]
    public class RotateUIObject3D : MonoBehaviour
    {
        public enum eRotationMode
        {
            Constant,
            WhenMouseIsOver,
            WhenMouseIsOverThenSnapBack
        }

        public eRotationMode RotationMode = eRotationMode.Constant;

        public bool RotateX = false;
        public float RotateXSpeed = 45f;

        public bool RotateY = true;
        public float RotateYSpeed = 45f;

        public bool RotateZ = false;
        public float RotateZSpeed = 45f;

        public float snapbackTime = 0.25f;

        private UIObject3D UIObject3D;

        private bool mouseIsOver = false;

        private Vector3 initialRotation = Vector3.zero;

        private EventTrigger _eventTrigger = null;
        private EventTrigger eventTrigger
        {
            get
            {
                if (_eventTrigger == null) _eventTrigger = this.GetComponent<EventTrigger>() ?? this.gameObject.AddComponent<EventTrigger>();
                return _eventTrigger;
            }
        }

        private float timeSinceLastUpdate = 0f;

        void Awake()
        {
            UIObject3D = this.GetComponent<UIObject3D>();
            initialRotation = UIObject3DUtilities.NormalizeRotation(UIObject3D.TargetRotation);

            SetupEvents();
        }

        void Update()
        {
            timeSinceLastUpdate += Time.deltaTime;

            if (UIObject3D.LimitFrameRate)
            {
                if (timeSinceLastUpdate < UIObject3D.timeBetweenFrames) return;
            }

            switch (RotationMode)
            {
                case eRotationMode.Constant:
                    {
                        UpdateRotation();
                    }
                    break;
                case eRotationMode.WhenMouseIsOver:
                case eRotationMode.WhenMouseIsOverThenSnapBack:
                    {
                        if (mouseIsOver) UpdateRotation();
                    }
                    break;
            }
        }

        void UpdateRotation()
        {
            UIObject3D.TargetRotation += new Vector3(
                    RotateX ? RotateXSpeed * timeSinceLastUpdate : 0,
                    RotateY ? RotateYSpeed * timeSinceLastUpdate : 0,
                    RotateZ ? RotateZSpeed * timeSinceLastUpdate : 0
                );

            timeSinceLastUpdate = 0f;
        }

        void SetupEvents()
        {
            // get or add the event trigger
            var onPointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            var onPointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };

            onPointerEnter.callback.AddListener((e) => OnPointerEnter());
            onPointerExit.callback.AddListener((e) => OnPointerExit());

            eventTrigger.triggers.Add(onPointerEnter);
            eventTrigger.triggers.Add(onPointerExit);
        }

        void OnPointerEnter()
        {
            mouseIsOver = true;
        }

        void OnPointerExit()
        {
            mouseIsOver = false;

            if (RotationMode == eRotationMode.WhenMouseIsOverThenSnapBack)
            {
                StartCoroutine(SnapBack(snapbackTime));
            }
        }

        IEnumerator SnapBack(float time)
        {
            var timeStarted = Time.time;

            float percentageComplete = 0f;
            Vector3 snapStartRotation = UIObject3DUtilities.NormalizeRotation(UIObject3D.TargetRotation);


            // This sort of works, but perhaps it would be best to simply go back the way we came?
            float desiredX = (Mathf.Abs(snapStartRotation.x - initialRotation.x) >= 180f) ? (initialRotation.x - 180f) : initialRotation.x;
            float desiredY = (Mathf.Abs(snapStartRotation.y - initialRotation.y) >= 180f) ? (initialRotation.y - 180f) : initialRotation.y;
            float desiredZ = (Mathf.Abs(snapStartRotation.z - initialRotation.z) >= 180f) ? (initialRotation.z - 180f) : initialRotation.z;

            while (percentageComplete < 1f)
            {
                //UIObject3D.TargetRotation = Vector3.Lerp(snapStartRotation, initialRotation, percentageComplete);
                UIObject3D.TargetRotation = new Vector3(
                    (RotateX ? Mathf.Lerp(snapStartRotation.x, desiredX, percentageComplete) : desiredX),
                    (RotateY ? Mathf.Lerp(snapStartRotation.y, desiredY, percentageComplete) : desiredY),
                    (RotateZ ? Mathf.Lerp(snapStartRotation.z, desiredZ, percentageComplete) : desiredZ)
                    );

                percentageComplete = (Time.time - timeStarted) / time;

                yield return null;
            }

            UIObject3D.TargetRotation = initialRotation;
        }

        void OnValidate()
        {
            eventTrigger.enabled = RotationMode != eRotationMode.Constant;
        }
    }
}

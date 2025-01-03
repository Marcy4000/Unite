#region Namespace Imports
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;  // New Input System
#endregion

namespace UI.ThreeDimensional
{
    [RequireComponent(typeof(UIObject3D))]
    [AddComponentMenu("UI/UIObject3D/Drag Rotate UIObject3D")]
    public class DragRotateUIObject3D : MonoBehaviour
    {
        [Header("Speed")]
        public float RotationSpeed = 10f;

        [Header("X")]
        public bool RotateX = true;
        public bool InvertX = false;
        private int _xMultiplier
        {
            get { return InvertX ? -1 : 1; }
        }

        [Header("Y")]
        public bool RotateY = true;
        public bool InvertY = false;
        private int _yMultiplier
        {
            get { return InvertY ? -1 : 1; }
        }

        [Header("Inertia")]
        public bool UseInertia = false;
        public float SlowSpeed = 1f;

        private UIObject3D UIObject3D;
        private bool beingDragged = false;

        private Vector3 speed = Vector3.zero;
        private Vector3 averageSpeed = Vector3.zero;

        private Vector2 lastMousePosition = Vector2.zero;

        // Input actions
        [SerializeField] private InputActionReference pointerClickAction;
        [SerializeField] private InputActionReference pointerPositionAction;

        void Awake()
        {
            UIObject3D = this.GetComponent<UIObject3D>();

            SetupEvents();

            // Enable the input actions
            pointerClickAction.action.Enable();
            pointerPositionAction.action.Enable();
        }

        void Update()
        {
            if (UIObject3D == null || UIObject3D.targetContainer == null) return;

            if (lastMousePosition == Vector2.zero) lastMousePosition = pointerPositionAction.action.ReadValue<Vector2>();

            if (beingDragged)
            {
                // Calculate speed based on pointer delta
                var mouseDelta = (pointerPositionAction.action.ReadValue<Vector2>() - lastMousePosition) * 100;
                mouseDelta.Set(mouseDelta.x / Screen.width, mouseDelta.y / Screen.height);

                speed = new Vector3(-mouseDelta.x * _xMultiplier, mouseDelta.y * _yMultiplier, 0);
                averageSpeed = Vector3.Lerp(averageSpeed, speed, Time.deltaTime * 5);
            }
            else
            {
                if (beingDragged)
                {
                    speed = averageSpeed;
                    beingDragged = false;
                }

                if (UseInertia)
                {
                    float i = Time.deltaTime * SlowSpeed;
                    speed = Vector3.Lerp(speed, Vector3.zero, i);
                }
                else
                {
                    speed = Vector3.zero;
                }
            }

            if (speed != Vector3.zero)
            {
                if (RotateX) UIObject3D.targetContainer.Rotate(Camera.main.transform.up * speed.x * RotationSpeed, Space.World);
                if (RotateY) UIObject3D.targetContainer.Rotate(Camera.main.transform.right * speed.y * RotationSpeed, Space.World);
                UIObject3D.TargetRotation = UIObject3D.targetContainer.localRotation.eulerAngles;
            }
        }

        void SetupEvents()
        {
            // Get or add the event trigger
            EventTrigger trigger = this.GetComponent<EventTrigger>() ?? this.gameObject.AddComponent<EventTrigger>();

            var onPointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            onPointerDown.callback.AddListener((e) => OnPointerDown((PointerEventData)e));
            trigger.triggers.Add(onPointerDown);

            var onPointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            onPointerUp.callback.AddListener((e) => OnPointerUp((PointerEventData)e));
            trigger.triggers.Add(onPointerUp);
        }

        private void OnPointerDown(PointerEventData eventData)
        {
            if (pointerClickAction.action.ReadValue<float>() > 0)
            {
                beingDragged = true;
            }
        }

        private void OnPointerUp(PointerEventData eventData)
        {
            beingDragged = false;
        }

        private void OnDestroy()
        {
            // Disable the input actions when the object is destroyed
            pointerClickAction.action.Disable();
            pointerPositionAction.action.Disable();
        }
    }
}

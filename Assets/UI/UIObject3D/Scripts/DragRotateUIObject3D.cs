#region Namespace Imports
using UnityEngine;
using UnityEngine.InputSystem;
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
        private int _xMultiplier => InvertX ? -1 : 1;

        [Header("Y")]
        public bool RotateY = true;
        public bool InvertY = false;
        private int _yMultiplier => InvertY ? -1 : 1;

        [Header("Inertia")]
        public bool UseInertia = false;
        public float SlowSpeed = 1f;

        private UIObject3D UIObject3D;

        private Vector3 speed = Vector3.zero;
        private Vector3 averageSpeed = Vector3.zero;

        private PlayerControls playerInput;

        void Awake()
        {
            UIObject3D = GetComponent<UIObject3D>();
            playerInput = InputManager.Instance.Controls;
        }

        void Update()
        {
            if (UIObject3D == null || UIObject3D.targetContainer == null) return;

            Vector2 inputDelta = OnModelRotatePerformed();

            if (speed != Vector3.zero)
            {
                if (RotateX)
                    UIObject3D.targetContainer.Rotate(Camera.main.transform.up * speed.x * RotationSpeed, Space.World);
                if (RotateY)
                    UIObject3D.targetContainer.Rotate(Camera.main.transform.right * speed.y * RotationSpeed, Space.World);

                UIObject3D.TargetRotation = UIObject3D.targetContainer.localRotation.eulerAngles;

                if (UseInertia && inputDelta == Vector2.zero)
                {
                    speed = Vector3.Lerp(speed, Vector3.zero, Time.deltaTime * SlowSpeed);
                }
                else if (!UseInertia)
                {
                    speed = Vector3.zero;
                }
            }
        }

        private Vector2 OnModelRotatePerformed()
        {
            Vector2 inputDelta = playerInput.UI.ModelRotate.ReadValue<Vector2>();

            speed = new Vector3(-inputDelta.x * _xMultiplier * Time.deltaTime, inputDelta.y * _yMultiplier * Time.deltaTime, 0);
            averageSpeed = Vector3.Lerp(averageSpeed, speed, Time.deltaTime * 5);

            return inputDelta;
        }
    }
}

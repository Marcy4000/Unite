using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class MobileButtonWithStick : OnScreenControl
{
    [SerializeField] private RectTransform stick;
    [SerializeField] private RectTransform stickArea;
    [SerializeField] private float maxStickDistance = 100f;

    private Vector2 startPos;
    private bool isButtonPressed = false;
    private Vector2 inputVector;
    private int touchId = -1;

    private float lastDistance = 0f;

    [SerializeField]
    private string m_ControlPath;

    protected override string controlPathInternal
    {
        get => m_ControlPath;
        set => m_ControlPath = value;
    }

    private void Start()
    {
        stick.gameObject.SetActive(false);  // Initially hide the stick
        startPos = stick.anchoredPosition;
    }

    private void Update()
    {
        if (isButtonPressed)
        {
            Vector2 touchPos = Vector2.zero;
            bool touchFound = false;

            for (int i = 0; i < Touch.activeTouches.Count; i++)
            {
                var touch = Touch.activeTouches[i];
                if (touch.finger.index == touchId)
                {
                    touchFound = true;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(stickArea, touch.screenPosition, null, out touchPos);
                    break;
                }
            }

            if (!touchFound)
            {
                // If the touch is not found, assume it was lifted and reset
                isButtonPressed = false;
                touchId = -1;
                StartCoroutine(HideStick());
            }
            else
            {
                Vector2 direction = touchPos - startPos;
                lastDistance = direction.magnitude;
                float distance = Mathf.Clamp(direction.magnitude, 0, maxStickDistance);
                Vector2 clampedDirection = direction.normalized * distance;
                stick.anchoredPosition = startPos + clampedDirection;

                // Normalize the input vector for use as input to the game
                inputVector = clampedDirection / maxStickDistance;
                SendInputToControl(inputVector);
            }
        }
    }

    // Method to hide the stick
    private IEnumerator HideStick()
    {
        stick.gameObject.SetActive(false);
        stick.anchoredPosition = startPos;

        if (lastDistance > (maxStickDistance * 5f))
        {
            StartCoroutine(SimulateCancelButton());
        }

        lastDistance = 0f;

        yield return new WaitForSeconds(0.1f);

        SendInputToControl(Vector2.zero); // Reset the input when stick is hidden
    }

    // Unity's new input system callback for button press
    public void OnButtonPress(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            isButtonPressed = true;
            
            if (Touch.activeTouches.Count > 0)
            {
                foreach (var touch in Touch.activeTouches)
                {
                    Debug.Log(touch.screenPosition);
                    Debug.Log(stickArea);
                    if (RectTransformUtility.RectangleContainsScreenPoint(stickArea, touch.screenPosition))
                    {
                        touchId = touch.finger.index;
                        break;
                    }
                }
            }
            stick.gameObject.SetActive(true);
        }
        else if (context.phase == InputActionPhase.Canceled)
        {
            isButtonPressed = false;
            touchId = -1;
            StartCoroutine(HideStick());
        }
    }

    // Method to send input to the game
    private void SendInputToControl(Vector2 input)
    {
        var gamepad = InputSystem.GetDevice<Gamepad>();
        if (gamepad != null)
        {
            InputSystem.QueueDeltaStateEvent(gamepad.rightStick, input);
        }
    }

    private IEnumerator SimulateCancelButton()
    {
        var gamepad = InputSystem.GetDevice<Gamepad>();
        if (gamepad != null)
        {
            SendValueToControl(1f);
            yield return new WaitForSeconds(0.1f);
            SendValueToControl(0f);
        }
    }
}

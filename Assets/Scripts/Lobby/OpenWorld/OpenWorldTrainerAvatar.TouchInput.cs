using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public sealed partial class OpenWorldTrainerAvatar
{
    private readonly List<RaycastResult> touchLookRaycasts = new List<RaycastResult>();
    private Finger cameraTouchFinger;
    private bool touchLookEnabled;
    private bool mouseTouchLookActive;

    private void InitializeTouchLookInput()
    {
        if (touchLookEnabled) return;
        EnhancedTouchSupport.Enable();
        touchLookEnabled = true;
    }

    private void ReleaseTouchLookInput()
    {
        ResetTouchLookInput();
        if (!touchLookEnabled) return;
        EnhancedTouchSupport.Disable();
        touchLookEnabled = false;
    }

    private void ResetTouchLookInput()
    {
        cameraTouchFinger = null;
        mouseTouchLookActive = false;
    }

    private bool IsTouchLookOverUi(Vector2 position)
    {
        var events = EventSystem.current;
        if (events == null) return false;
        var pointer = new PointerEventData(events) { position = position };
        touchLookRaycasts.Clear();
        events.RaycastAll(pointer, touchLookRaycasts);
        return touchLookRaycasts.Count > 0;
    }

    private void ApplyTouchLookDelta(Vector2 delta)
    {
        cameraYaw += delta.x * 0.15f;
        cameraPitch = Mathf.Clamp(cameraPitch - delta.y * 0.15f, 10f, 70f);
    }

    private void UpdateTouchLookInput()
    {
        if (!touchLookEnabled) return;
        var touches = Touch.activeTouches;
        if (touches.Count > 0)
        {
            mouseTouchLookActive = false;
            foreach (var touch in touches)
            {
                if (cameraTouchFinger == null && touch.phase == UnityEngine.InputSystem.TouchPhase.Began &&
                    !IsTouchLookOverUi(touch.screenPosition))
                    cameraTouchFinger = touch.finger;

                if (touch.finger != cameraTouchFinger) continue;
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                    touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    cameraTouchFinger = null;
                else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                    ApplyTouchLookDelta(touch.delta);
                return;
            }
            cameraTouchFinger = null;
            return;
        }

        cameraTouchFinger = null;
#if UNITY_EDITOR
        var mouse = Mouse.current;
        if (OpenWorldLobbyView.UsesMobileControls && mouse != null && mouse.leftButton.isPressed)
        {
            if (mouse.leftButton.wasPressedThisFrame)
                mouseTouchLookActive = !IsTouchLookOverUi(mouse.position.ReadValue());
            else if (mouseTouchLookActive)
                ApplyTouchLookDelta(mouse.delta.ReadValue());
        }
        else
            mouseTouchLookActive = false;
#endif
    }
}

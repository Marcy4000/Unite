using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Handles mobile touch input for BigJumpPad landing position selection.
/// Allows players to tap directly on landing positions to perform jumps.
/// Only active on Android builds.
/// </summary>
public class BigJumpPadMobileInputHandler : MonoBehaviour
{
#if UNITY_ANDROID
    [Header("Settings")]
    [SerializeField] private float maxRaycastDistance = 100f;
    [SerializeField] private LayerMask raycastLayerMask = ~0; // Default to all layers

    private Camera mainCamera;
    private BigJumpPad activeJumpPad;

    private void Awake()
    {
        mainCamera = Camera.main;
        raycastLayerMask = LayerMask.GetMask("JumpLandingPosition");
    }

    private void OnEnable()
    {
        BigJumpPad.OnEnterJumpSelection += HandleEnterJumpSelection;
        BigJumpPad.OnExitJumpSelection += HandleExitJumpSelection;

        if (!EnhancedTouchSupport.enabled)
        {
            EnhancedTouchSupport.Enable();
        }
    }

    private void OnDisable()
    {
        BigJumpPad.OnEnterJumpSelection -= HandleEnterJumpSelection;
        BigJumpPad.OnExitJumpSelection -= HandleExitJumpSelection;
    }

    private void HandleEnterJumpSelection(BigJumpPad jumpPad)
    {
        activeJumpPad = jumpPad;
    }

    private void HandleExitJumpSelection()
    {
        activeJumpPad = null;
    }

    private void Update()
    {
        if (activeJumpPad == null || !activeJumpPad.InJumpSelection) return;

        HandleTouchInput();
    }

    private void HandleTouchInput()
    {
        if (Touch.activeTouches.Count == 0) return;

        foreach (var touch in Touch.activeTouches)
        {
            // Only process touch that just began (tap)
            if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;

            // Skip if touch is over UI elements
            if (IsPointerOverUI(touch.screenPosition)) continue;

            TrySelectLandingPosition(touch.screenPosition);
        }
    }

    private void TrySelectLandingPosition(Vector2 screenPosition)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, raycastLayerMask))
        {
            // Check if hit object has the landing position tag
            string landingTag = activeJumpPad.GetLandingPositionTag();

            if (hit.transform.CompareTag(landingTag))
            {
                activeJumpPad.OnMobileTapLandingPosition(hit.transform);
            }
            else
            {
                // Try to find landing position in parent hierarchy
                Transform landingPosition = FindLandingPositionInParent(hit.transform, landingTag);
                if (landingPosition != null)
                {
                    activeJumpPad.OnMobileTapLandingPosition(landingPosition);
                }
            }
        }
    }

    private Transform FindLandingPositionInParent(Transform target, string tag)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.CompareTag(tag))
            {
                return current;
            }
            current = current.parent;
        }
        return null;
    }

    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem == null) return false;

        var pointerEventData = new UnityEngine.EventSystems.PointerEventData(eventSystem)
        {
            position = screenPosition
        };

        var raycastResults = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        eventSystem.RaycastAll(pointerEventData, raycastResults);

        return raycastResults.Count > 0;
    }
#endif
}

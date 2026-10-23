using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A raycastable UI area in the mobile HUD. Owns one pointer so moving/aiming
/// with another finger cannot move the camera or end the current camera drag.
/// </summary>
public class MobileCameraPanArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IInitializePotentialDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private CameraController cameraController;

    private int? activePointerId;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || activePointerId.HasValue
            || eventData.button != PointerEventData.InputButton.Left
            || cameraController == null || !cameraController.BeginTouchPan()) return;

        activePointerId = eventData.pointerId;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (activePointerId == eventData.pointerId && cameraController != null)
        {
            cameraController.DragTouchPan(eventData.delta);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (activePointerId == eventData.pointerId) ReleasePointer();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (activePointerId == eventData.pointerId) ReleasePointer();
    }

    private void OnDisable()
    {
        ReleasePointer();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) ReleasePointer();
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused) ReleasePointer();
    }

    private void ReleasePointer()
    {
        if (!activePointerId.HasValue) return;

        activePointerId = null;
        if (cameraController != null) cameraController.EndTouchPan();
    }
}

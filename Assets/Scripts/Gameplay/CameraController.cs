using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    // Keep the base type so the Cinemachine upgrader can preserve the serialized reference.
    [SerializeField] private CinemachineVirtualCameraBase virtualCamera;
    [SerializeField] private float panSpeed = 10f;
    [Tooltip("World units moved by a touch drag spanning the screen height.")]
    [SerializeField, Min(0f)] private float touchPanSensitivity = 30f;

    private Vector2 moveInput;
    private bool isPanning;
    private bool forcePanning;
    private bool keyboardPanning;
    private bool touchPanning;
    private Transform cameraTransform;
    private Transform playerTransform;

    public CinemachineCamera VirtualCamera => virtualCamera as CinemachineCamera;
    public bool IsPanning => isPanning;

    private Coroutine superJumpCameraCoroutine;
    private readonly Dictionary<object, ZoomOverride> zoomOverrides = new();
    private readonly object legacySuperJumpOwner = new();
    private Vector3 baseZoomOffset = new Vector3(0, 18.85f, -18.6f);
    private float baseZoomFOV = 25f;
    private Vector3 effectiveZoomOffset;
    private float effectiveZoomFOV;
    private bool hasEffectiveZoom;
    private long zoomOrder;
    private readonly ZoomRenderScalePolicy renderScalePolicy = new();

    private void LateUpdate()
    {
        var camera = VirtualCamera;
        var follow = camera != null ? camera.GetComponent<CinemachineFollow>() : null;
        if (Instance != this || camera == null || !camera.isActiveAndEnabled || follow == null)
        {
            renderScalePolicy.Restore();
            return;
        }
        renderScalePolicy.Apply(follow.FollowOffset.magnitude, camera.Lens.FieldOfView);
    }

    private void OnDisable()
    {
        renderScalePolicy.Restore();
    }

    private void OnDestroy()
    {
        renderScalePolicy.Restore();
        if (Instance == this) Instance = null;
    }

    private struct ZoomOverride
    {
        public Vector3 Offset;
        public float FieldOfView;
        public int Priority;
        public long Order;
    }

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (virtualCamera != null)
        {
            cameraTransform = virtualCamera.transform;
        }
    }

    public void Initialize(Transform playerTransform)
    {
        this.playerTransform = playerTransform;
        if (virtualCamera != null && !isPanning)
        {
            virtualCamera.Follow = playerTransform;
            virtualCamera.LookAt = playerTransform;
        }
    }

    public void ForcePan(bool value)
    {
        forcePanning = value;
        if (!value)
        {
            keyboardPanning = false;
            touchPanning = false;
        }
        SetPanning(forcePanning || keyboardPanning || touchPanning);
    }

    public bool BeginTouchPan()
    {
        if (virtualCamera == null || playerTransform == null) return false;

        cameraTransform = virtualCamera.transform;
        touchPanning = true;
        SetPanning(true);
        return true;
    }

    public void DragTouchPan(Vector2 screenDelta)
    {
        if (!touchPanning || cameraTransform == null) return;

        // Displacement, not velocity: independent of frame rate, DPI and resolution.
        Vector2 movement = screenDelta * (touchPanSensitivity / Mathf.Max(1, Screen.height));
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        cameraTransform.position += right * movement.x + forward * movement.y;
    }

    public void EndTouchPan()
    {
        touchPanning = false;
        SetPanning(forcePanning || keyboardPanning);
    }

    private void SetPanning(bool value)
    {
        isPanning = value;
        if (virtualCamera == null) return;

        virtualCamera.Follow = value ? null : playerTransform;
        virtualCamera.LookAt = value ? null : playerTransform;
    }

    public void ZoomCamera(Vector3 targetOffset, float targetFOV = 25f, float duration = 1.0f)
    {
        baseZoomOffset = targetOffset;
        baseZoomFOV = targetFOV;
        ApplyEffectiveZoom(duration);
    }

    public void SetZoomOverride(object owner, Vector3 targetOffset, float targetFOV = 25f, int priority = 0, float duration = 1f)
    {
        if (owner == null) return;
        zoomOverrides[owner] = new ZoomOverride
        {
            Offset = targetOffset, FieldOfView = targetFOV, Priority = priority, Order = ++zoomOrder
        };
        ApplyEffectiveZoom(duration);
    }

    public void ReleaseZoomOverride(object owner, float duration = 1f)
    {
        if (owner != null && zoomOverrides.Remove(owner)) ApplyEffectiveZoom(duration);
    }

    private void ApplyEffectiveZoom(float duration)
    {
        Vector3 targetOffset = baseZoomOffset;
        float targetFOV = baseZoomFOV;
        int priority = int.MinValue;
        long order = long.MinValue;
        foreach (var request in zoomOverrides.Values)
        {
            if (request.Priority < priority || (request.Priority == priority && request.Order < order)) continue;
            priority = request.Priority;
            order = request.Order;
            targetOffset = request.Offset;
            targetFOV = request.FieldOfView;
        }
        if (hasEffectiveZoom && effectiveZoomOffset == targetOffset && effectiveZoomFOV == targetFOV) return;
        hasEffectiveZoom = true;
        effectiveZoomOffset = targetOffset;
        effectiveZoomFOV = targetFOV;
        if (superJumpCameraCoroutine != null)
        {
            StopCoroutine(superJumpCameraCoroutine);
        }
        superJumpCameraCoroutine = StartCoroutine(SmoothZoomTransition(targetFOV, targetOffset, duration));
    }

    public void ResetZoom()
    {
        ZoomCamera(new Vector3(0, 18.85f, -18.6f), 25f);
    }

    public void SetSuperJumpCamera(bool isSuperJump)
    {
        if (isSuperJump)
        {
            SetZoomOverride(legacySuperJumpOwner, new Vector3(0, 57.7f, -61), 30f, 100);
        }
        else
        {
            ReleaseZoomOverride(legacySuperJumpOwner);
        }
    }

    private IEnumerator SmoothZoomTransition(float targetFOV, Vector3 targetOffset, float duration)
    {
        var camera = VirtualCamera;
        if (camera == null) yield break;
        var follow = camera.GetComponent<CinemachineFollow>();
        if (follow == null) yield break;
        float initialFOV = camera.Lens.FieldOfView;
        Vector3 initialOffset = follow.FollowOffset;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            camera.Lens.FieldOfView = Mathf.Lerp(initialFOV, targetFOV, elapsedTime / duration);
            follow.FollowOffset = Vector3.Lerp(initialOffset, targetOffset, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        camera.Lens.FieldOfView = targetFOV;
        follow.FollowOffset = targetOffset;

        superJumpCameraCoroutine = null;
    }

    void Update()
    {
        if (InputManager.Instance == null || InputManager.Instance.Controls == null) return;

        if (!forcePanning)
        {
            if (InputManager.Instance.Controls.PanCamera.ShouldPan.WasPressedThisFrame())
            {
                keyboardPanning = true;
                SetPanning(true);
            }
            else if (InputManager.Instance.Controls.PanCamera.ShouldPan.WasReleasedThisFrame())
            {
                keyboardPanning = false;
                SetPanning(touchPanning);
            }
        }

        if (isPanning && !touchPanning)
        {
            moveInput = InputManager.Instance.Controls.PanCamera.CameraMove.ReadValue<Vector2>();
            PanCamera();
        }
    }

    private void PanCamera()
    {
        if (cameraTransform != null)
        {
            Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
            cameraTransform.Translate(moveDirection * panSpeed * Time.deltaTime, Space.World);
        }
    }
}

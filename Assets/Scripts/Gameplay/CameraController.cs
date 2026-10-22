using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    // Keep the base type so the Cinemachine upgrader can preserve the serialized reference.
    [SerializeField] private CinemachineVirtualCameraBase virtualCamera;
    [SerializeField] private float panSpeed = 10f;

    private Vector2 moveInput;
    private bool isPanning;
    private bool forcePanning;
    private Transform cameraTransform;
    private Transform playerTransform;

    public CinemachineCamera VirtualCamera => virtualCamera as CinemachineCamera;
    public bool IsPanning => isPanning;

    private Coroutine superJumpCameraCoroutine;

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
        if (virtualCamera != null)
        {
            virtualCamera.Follow = playerTransform;
            virtualCamera.LookAt = playerTransform;
        }
    }

    public void ForcePan(bool value)
    {
        forcePanning = value;

        if (forcePanning)
        {
            isPanning = true;
            virtualCamera.Follow = null;
            virtualCamera.LookAt = null;
        }
        else
        {
            isPanning = false;
            virtualCamera.Follow = playerTransform;
            virtualCamera.LookAt = playerTransform;
        }
    }

    public void ZoomCamera(Vector3 targetOffset, float targetFOV = 25f, float duration = 1.0f)
    {
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
            ZoomCamera(new Vector3(0, 57.7f, -61), 30f);
        }
        else
        {
            ResetZoom();
        }
    }

    private IEnumerator SmoothZoomTransition(float targetFOV, Vector3 targetOffset, float duration)
    {
        var camera = VirtualCamera;
        var follow = camera.GetComponent<CinemachineFollow>();
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
        if (!forcePanning)
        {
            if (InputManager.Instance.Controls.PanCamera.ShouldPan.WasPressedThisFrame())
            {
                isPanning = true;
                virtualCamera.Follow = null;
                virtualCamera.LookAt = null;
            }
            else if (InputManager.Instance.Controls.PanCamera.ShouldPan.WasReleasedThisFrame())
            {
                isPanning = false;
                virtualCamera.Follow = playerTransform;
                virtualCamera.LookAt = playerTransform;
            }
        }

        if (isPanning)
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

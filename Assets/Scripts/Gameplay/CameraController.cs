using UnityEngine;
using Cinemachine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private float panSpeed = 10f;

    private Vector2 moveInput;
    private bool isPanning;
    private bool forcePanning;
    private Transform cameraTransform;
    private Transform playerTransform;

    public CinemachineVirtualCamera VirtualCamera => virtualCamera;
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
        var trasposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
        float initialFOV = virtualCamera.m_Lens.FieldOfView;
        Vector3 initialOffset = trasposer.m_FollowOffset;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(initialFOV, targetFOV, elapsedTime / duration);
            trasposer.m_FollowOffset = Vector3.Lerp(initialOffset, targetOffset, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        virtualCamera.m_Lens.FieldOfView = targetFOV;
        trasposer.m_FollowOffset = targetOffset;

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
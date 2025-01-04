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

    public void SetSuperJumpCamera(bool isSuperJump)
    {
        virtualCamera.m_Lens.FieldOfView = isSuperJump ? 30 : 25;
        var trasposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
        Vector3 targetOffset = isSuperJump ? new Vector3(0, 57.7f, -61) : new Vector3(0, 18.85f, -18.6f);

        if (superJumpCameraCoroutine != null)
        {
            StopCoroutine(superJumpCameraCoroutine);
        }
        superJumpCameraCoroutine = StartCoroutine(SmoothTransition(trasposer, targetOffset));
    }

    private IEnumerator SmoothTransition(CinemachineTransposer trasposer, Vector3 targetOffset)
    {
        Vector3 initialOffset = trasposer.m_FollowOffset;
        float transitionDuration = 1.0f; // Duration of the transition in seconds
        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            trasposer.m_FollowOffset = Vector3.Lerp(initialOffset, targetOffset, elapsedTime / transitionDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

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
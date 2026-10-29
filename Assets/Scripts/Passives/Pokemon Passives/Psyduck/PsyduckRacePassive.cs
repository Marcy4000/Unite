using System.Collections;
using UnityEngine;

public class PsyduckRacePassive : PassiveBase
{
    protected CharacterController characterController;

    private bool canMove = true;

    protected float acceleration = 8f;
    protected float deceleration = 5f;
    protected float maxSpeed = 12f;
    protected float turnSpeed = 3.5f;
    protected float driftFactor = 0.7f;
    protected float rotationSpeed = 200f;

    protected Vector3 currentVelocity;
    protected Vector3 currentFacingDirection;

    protected bool isFrozen = false;

    private float animationUpdateRate = 0.1f;
    private float lastAnimationUpdate;
    private static readonly int RacingIdleState = Animator.StringToHash("Base Layer.ani_spell1bidle_bat_0054");
    private Coroutine movementChangeRoutine;
    private float originalStepOffset;
    private bool disposed;

    protected bool isDashing = false;
    protected float dashSpeed = 20f;
    protected float dashDuration = 0.3f;
    protected float dashCooldown = 1f;

    protected float speedModifier = 1f;

    public bool CanMove { get => canMove; set => canMove = value; }
    public Vector3 CurrentVelocity { get => currentVelocity; set => currentVelocity = value; }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        characterController = playerManager.PlayerMovement.CharacterController;
        originalStepOffset = characterController.stepOffset;
        playerManager.PlayerMovement.OnCanMoveChanged += OnPlayerMovementCanMoveChange;
        currentFacingDirection = playerManager.transform.forward;

        playerManager.PlayerMovement.CharacterController.stepOffset = 0;

        playerManager.Pokemon.OnStatusChange += OnStatusChanged;

        canMove = playerManager.PlayerMovement.CanMove;

        SnapPlayerToGround();
    }

    internal static void InitializeAnimator(Animator animator)
    {
        animator.keepAnimatorStateOnDisable = true;
        if (animator.HasState(0, RacingIdleState))
        {
            animator.Play(RacingIdleState, 0, 0f);
        }
    }

    private void OnPlayerMovementCanMoveChange(bool value)
    {
        if (!disposed)
        {
            movementChangeRoutine = playerManager.StartCoroutine(SetCanMoveRoutine(value));
        }
    }

    private IEnumerator SetCanMoveRoutine(bool value)
    {
        canMove = value;
        playerManager.PlayerMovement.OnCanMoveChanged -= OnPlayerMovementCanMoveChange;
        playerManager.PlayerMovement.CanMove = false;

        yield return null;

        movementChangeRoutine = null;
        if (!disposed && playerManager != null)
        {
            playerManager.PlayerMovement.OnCanMoveChanged += OnPlayerMovementCanMoveChange;
        }
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (playerManager == null) return;

        if (movementChangeRoutine != null)
        {
            playerManager.StopCoroutine(movementChangeRoutine);
            movementChangeRoutine = null;
        }
        playerManager.Pokemon.OnStatusChange -= OnStatusChanged;
        playerManager.PlayerMovement.OnCanMoveChanged -= OnPlayerMovementCanMoveChange;
        // The passive disables regular movement while it handles input itself.
        // Restore the requested state before a replacement passive takes over;
        // PlayerMovement still respects any external movement restrictions.
        playerManager.PlayerMovement.CanMove = canMove;
        if (characterController != null && characterController.stepOffset == 0f)
        {
            characterController.stepOffset = originalStepOffset;
        }
    }

    private void OnStatusChanged(StatusEffect status, bool added)
    {
        if (status.Type == StatusType.Scriptable && status.ID == 20)
        {
            isFrozen = added;
        }
    }

    public override void Update()
    {
        if (!canMove || playerManager.PlayerMovement.IsKnockedUp)
        {
            isDashing = false;
            dashDuration = 0.3f;
            currentVelocity = Vector3.zero;
            SetWalking(false);
            UpdateAnimations();
            return;
        }
        if (isFrozen && isDashing)
        {
            isDashing = false;
            dashDuration = 0.3f;
        }
        if (isDashing)
        {
            Vector2 dashInput = playerManager.MovementInput;
            if (dashInput.magnitude > 0.1f)
            {
                Vector3 dashInputDir = new Vector3(dashInput.x, 0, dashInput.y).normalized;
                Quaternion dashTargetRot = Quaternion.LookRotation(dashInputDir, Vector3.up);
                float dashTurnSpeed = rotationSpeed * 0.15f;
                playerManager.transform.rotation = Quaternion.RotateTowards(
                    playerManager.transform.rotation,
                    dashTargetRot,
                    dashTurnSpeed * Time.deltaTime
                );
                currentFacingDirection = playerManager.transform.forward;
            }
            HandleDash();
        }
        else
        {
            HandleMovement(isFrozen ? Vector2.zero : playerManager.MovementInput);
        }

        UpdateAnimations();
    }

    private void HandleMovement(Vector2 input)
    {
        if (isFrozen)
        {
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, deceleration * 0.5f * Time.deltaTime);
            characterController.Move(currentVelocity * Time.deltaTime);
            return;
        }

        if (!canMove || playerManager.PlayerMovement.IsKnockedUp)
        {
            currentVelocity = Vector3.zero;
            SetWalking(false);
            return;
        }

        Vector3 inputDirection = new Vector3(input.x, 0, input.y).normalized;

        float modifiedMaxSpeed = maxSpeed * speedModifier;
        if (playerManager.IsBot) modifiedMaxSpeed *= Mathf.Clamp(input.magnitude, .2f, 1);

        if (inputDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(inputDirection, Vector3.up);
            playerManager.transform.rotation = Quaternion.RotateTowards(playerManager.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            currentFacingDirection = playerManager.transform.forward;

            Vector3 desiredVelocity = currentFacingDirection * modifiedMaxSpeed;

            float driftLerp = driftFactor * Time.deltaTime * 2f;
            currentVelocity = Vector3.Lerp(currentVelocity, desiredVelocity, driftLerp);

            if (currentVelocity.magnitude > modifiedMaxSpeed)
                currentVelocity = currentVelocity.normalized * modifiedMaxSpeed;

            SetWalking(true);
        }
        else
        {
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
            SetWalking(false);
        }

        characterController.Move(currentVelocity * Time.deltaTime);
    }

    private void HandleDash()
    {
        characterController.Move(currentFacingDirection * dashSpeed * Time.deltaTime);

        currentVelocity = currentFacingDirection * dashSpeed * 0.8f;

        dashDuration -= Time.deltaTime;
        if (dashDuration <= 0)
        {
            isDashing = false;
            dashDuration = 0.3f;
        }
    }

    public void Dash()
    {
        TryDash();
    }

    public bool TryDash()
    {
        if (disposed || isDashing || isFrozen || !canMove || playerManager.PlayerMovement.IsKnockedUp) return false;
        isDashing = true;
        return true;
    }

    public void Dash(float dashDuration)
    {
        if (float.IsFinite(dashDuration) && dashDuration > 0 && TryDash())
        {
            this.dashDuration = dashDuration;
        }
    }

    private void UpdateAnimations()
    {
        if (Time.time - lastAnimationUpdate > animationUpdateRate)
        {
            Vector3 localVelocity = playerManager.transform.InverseTransformDirection(currentVelocity);
            float turnDirection = Mathf.Clamp(localVelocity.x / maxSpeed, -1f, 1f);

            if (!playerManager.AnimationManager.IsAnimatorNull())
            {
                playerManager.AnimationManager.SetFloat("TurnDirection", turnDirection);
            }

            if (canMove)
                SnapPlayerToGround();

            lastAnimationUpdate = Time.time;
        }
    }

    private void SetWalking(bool isWalking)
    {
        if (playerManager.AnimationManager.IsAnimatorNull()) return;
        if (playerManager.AnimationManager.Animator.GetBool("Walking") != isWalking)
        {
            playerManager.AnimationManager.SetBool("Walking", isWalking);
        }
    }

    private void SnapPlayerToGround()
    {
        RaycastHit hit;
        if (Physics.Raycast(playerManager.transform.position, Vector3.down, out hit))
        {
            float distanceToGround = hit.distance;
            if (distanceToGround > 0.001f)
            {
                playerManager.transform.position += Vector3.down * (distanceToGround - 0.001f);
            }
        }
    }

    public void FreezePlayer(bool freeze)
    {
        isFrozen = freeze;
    }

    public void SetSpeedModifier(float modifier)
    {
        speedModifier = modifier;
    }
}

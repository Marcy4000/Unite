using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PsyduckRacePassive : PassiveBase
{
    protected CharacterController characterController;

    private bool canMove = true;

    // Movement properties
    protected float acceleration = 7f;
    protected float deceleration = 4f;
    protected float maxSpeed = 10f;
    protected float turnSpeed = 3.5f; // Slightly increased for snappier turning
    protected float driftFactor = 0.7f; // Increased for more drifting
    protected float rotationSpeed = 200f; // Increased rotation speed for snappier turns

    protected Vector3 currentVelocity;
    protected Vector3 currentFacingDirection;

    protected bool isFrozen = false;

    private float animationUpdateRate = 0.1f; // Update rate for turnDirection
    private float lastAnimationUpdate;

    protected bool isDashing = false;
    protected float dashSpeed = 20f;
    protected float dashDuration = 0.3f;
    protected float dashCooldown = 1f;

    protected float speedModifier = 1f; // Multiplier for maxSpeed

    public bool CanMove { get => canMove; set => canMove = value; }
    public Vector3 CurrentVelocity { get => currentVelocity; set => currentVelocity = value; }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        characterController = playerManager.PlayerMovement.CharacterController;
        playerManager.PlayerMovement.OnCanMoveChanged += OnPlayerMovementCanMoveChange;
        currentFacingDirection = playerManager.transform.forward;

        playerManager.PlayerMovement.CharacterController.stepOffset = 0;

        playerManager.Pokemon.OnStatusChange += OnStatusChanged;

        canMove = playerManager.PlayerMovement.CanMove;

        playerManager.Pokemon.AddStatChange(new StatChange(4000, Stat.Speed, 0f, false, true, false, 0, false));

        playerManager.StartCoroutine(PlayAnimationDelayed());

        SnapPlayerToGround();
    }

    private IEnumerator PlayAnimationDelayed()
    {
        yield return new WaitForSeconds(0.1f);
        playerManager.AnimationManager.PlayAnimation("ani_spell1bidle_bat_0054");
    }

    private void OnPlayerMovementCanMoveChange(bool value)
    {
        playerManager.StartCoroutine(SetCanMoveRoutine(value));
    }

    private IEnumerator SetCanMoveRoutine(bool value)
    {
        canMove = value;
        playerManager.PlayerMovement.OnCanMoveChanged -= OnPlayerMovementCanMoveChange;
        playerManager.PlayerMovement.CanMove = false;

        yield return null;

        playerManager.PlayerMovement.OnCanMoveChanged += OnPlayerMovementCanMoveChange;
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
        if (isDashing)
        {
            HandleDash();
        }
        else
        {
            HandleMovement(playerManager.PlayerControls.Movement.Move.ReadValue<Vector2>());
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

        // Normalize input to determine desired direction
        Vector3 inputDirection = new Vector3(input.x, 0, input.y).normalized;

        if (inputDirection.magnitude > 0.1f)
        {
            // Gradually rotate towards input direction
            Quaternion targetRotation = Quaternion.LookRotation(inputDirection, Vector3.up);
            playerManager.transform.rotation = Quaternion.RotateTowards(playerManager.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            currentFacingDirection = playerManager.transform.forward;
        }

        // Adjust acceleration based on alignment and speed
        float alignmentFactor = Vector3.Dot(currentFacingDirection, inputDirection);
        alignmentFactor = Mathf.Clamp01((alignmentFactor + 1f) / 2f); // Scale from 0 (opposite) to 1 (aligned)

        // Accelerate in the current facing direction
        if (inputDirection.magnitude > 0)
        {
            currentVelocity += currentFacingDirection * acceleration * alignmentFactor * Time.deltaTime;
            SetWalking(true);
        }
        else
        {
            // Decelerate when no input is provided
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
            SetWalking(false);
        }

        // Clamp speed
        float modifiedMaxSpeed = maxSpeed * speedModifier;
        if (currentVelocity.magnitude > modifiedMaxSpeed)
        {
            currentVelocity = currentVelocity.normalized * modifiedMaxSpeed;
        }

        // Apply drift effect
        float speedFactor = currentVelocity.magnitude / modifiedMaxSpeed; // Higher effect at higher speeds
        Vector3 lateralVelocity = Vector3.Cross(Vector3.up, currentFacingDirection) * Vector3.Dot(currentVelocity, Vector3.Cross(Vector3.up, currentFacingDirection));
        Vector3 driftVelocity = currentVelocity - lateralVelocity * (1f - driftFactor * speedFactor);

        // Combine drift and forward motion
        Vector3 finalVelocity = Vector3.Lerp(driftVelocity, currentFacingDirection * currentVelocity.magnitude, driftFactor * speedFactor);

        // Move the character
        characterController.Move(finalVelocity * Time.deltaTime);
    }

    private void HandleDash()
    {
        // Dash movement
        characterController.Move(currentFacingDirection * dashSpeed * Time.deltaTime);

        // Maintain momentum after dash
        currentVelocity = currentFacingDirection * dashSpeed * 0.8f; // Retain a portion of dash speed

        // Decrease dash duration
        dashDuration -= Time.deltaTime;
        if (dashDuration <= 0)
        {
            isDashing = false;
            dashDuration = 0.3f; // Reset for next dash
        }
    }

    public void Dash()
    {
        if (!isDashing)
        {
            isDashing = true;
        }
    }

    public void Dash(float dashDuration)
    {
        if (!isDashing)
        {
            this.dashDuration = dashDuration;
            isDashing = true;
        }
    }

    private void UpdateAnimations()
    {
        if (Time.time - lastAnimationUpdate > animationUpdateRate)
        {
            // Update turnDirection based on current velocity and facing direction
            Vector3 localVelocity = playerManager.transform.InverseTransformDirection(currentVelocity);
            float turnDirection = Mathf.Clamp(localVelocity.x / maxSpeed, -1f, 1f);

            playerManager.AnimationManager.SetFloat("TurnDirection", turnDirection);

            if (canMove)
                SnapPlayerToGround();

            lastAnimationUpdate = Time.time;
        }
    }

    private void SetWalking(bool isWalking)
    {
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

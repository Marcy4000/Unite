using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
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
        playerManager.PlayerMovement.OnCanMoveChanged += OnPlayerMovementCanMoveChange;
        currentFacingDirection = playerManager.transform.forward;

        playerManager.PlayerMovement.CharacterController.stepOffset = 0;

        playerManager.Pokemon.OnStatusChange += OnStatusChanged;

        canMove = playerManager.PlayerMovement.CanMove;

        playerManager.StartCoroutine(PlayAnimationDelayed());

        SnapPlayerToGround();
    }

    private IEnumerator PlayAnimationDelayed()
    {
        yield return new WaitUntil(() => playerManager.Pokemon.CurrentEvolution != null);
        yield return new WaitForSeconds(0.15f);
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
            Vector2 dashInput = playerManager.PlayerControls.Movement.Move.ReadValue<Vector2>();
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

        Vector3 inputDirection = new Vector3(input.x, 0, input.y).normalized;

        float modifiedMaxSpeed = maxSpeed * speedModifier;

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

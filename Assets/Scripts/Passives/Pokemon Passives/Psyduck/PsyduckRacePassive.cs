using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PsyduckRacePassive : PassiveBase
{
    protected CharacterController characterController;

    private bool canMove = true;

    // Movement properties
    protected float acceleration = 6f;
    protected float deceleration = 4f;
    protected float maxSpeed = 8f;
    protected float turnSpeed = 3f;
    protected float driftFactor = 0.6f;
    protected float rotationSpeed = 180f;

    protected Vector3 currentVelocity;
    protected Vector3 currentFacingDirection;

    protected bool isFrozen = false;

    private float animationUpdateRate = 0.1f; // Update rate for turnDirection
    private float lastAnimationUpdate;

    protected float speedModifier = 1f; // Multiplier for maxSpeed

    public bool CanMove { get => canMove; set => canMove = value; }
    public Vector3 CurrentVelocity { get => currentVelocity; set => currentVelocity = value; }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        characterController = playerManager.PlayerMovement.CharacterController;
        playerManager.Pokemon.OnLevelChange += UpdateSpeed;
        playerManager.Pokemon.OnPokemonInitialized += UpdateSpeed;
        playerManager.Pokemon.OnStatChange += UpdateSpeed;
        playerManager.PlayerMovement.OnCanMoveChanged += OnPlayerMovementCanMoveChange;
        currentFacingDirection = playerManager.transform.forward;

        playerManager.PlayerMovement.CharacterController.stepOffset = 0;

        playerManager.Pokemon.OnStatusChange += OnStatusChanged;

        canMove = playerManager.PlayerMovement.CanMove;

        playerManager.Pokemon.AddStatChange(new StatChange(4000, Stat.Speed, 0f, false, true, false, 0, false));

        playerManager.AnimationManager.PlayAnimation("ani_spell1bidle_bat_0054");

        SnapPlayerToGround();
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
        HandleMovement(playerManager.PlayerControls.Movement.Move.ReadValue<Vector2>());
        UpdateAnimations();
    }

    private void UpdateSpeed(NetworkListEvent<StatChange> changeEvent)
    {
        UpdateSpeed();
    }

    private void UpdateSpeed()
    {
        maxSpeed = playerManager.Pokemon.GetSpeed() / 1000f;
    }

    private void HandleMovement(Vector2 input)
    {
        if (isFrozen)
        {
            // Apply sliding effect when frozen
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
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

        // Accelerate in the current facing direction
        if (inputDirection.magnitude > 0)
        {
            currentVelocity += currentFacingDirection * acceleration * Time.deltaTime;
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

        // Apply drift factor for smooth transitions
        Vector3 driftAdjustedVelocity = Vector3.Lerp(currentVelocity, currentFacingDirection * currentVelocity.magnitude, driftFactor);

        // Move the character
        characterController.Move(driftAdjustedVelocity * Time.deltaTime);
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

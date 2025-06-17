using DG.Tweening;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 4.1f;
    [SerializeField] private float dashDistance = 5f; // Set your desired dash distance
    [SerializeField] private float dashDuration = 0.2f; // Set your desired dash duration
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private LayerMask wallLayerMask;
    private Vector3 inputMovement;
    private Vector3 currentMovement; // Accumulated movement vector
    private CharacterController characterController;
    private Pokemon pokemon;
    private bool canMove = true;
    private int movementRestrictions = 0; // Reference counter for movement restrictions
    private bool isMoving = false;
    private bool isKnockedUp = false;
    private bool canBeKnockedBack = true;
    private bool snapToGround = true;

    private bool isDashing = false;
    private Vector3 dashDirection;

    public bool CanMove { get => canMove && movementRestrictions == 0; set => SetCanMove(value); }
    public bool CanBeKnockedBack { get => canBeKnockedBack; set => canBeKnockedBack = value; }
    public bool SnapToGround { get => snapToGround; set => snapToGround = value; }
    public bool IsKnockedUp => isKnockedUp;
    public bool IsDashing => isDashing;
    public bool IsMoving => isMoving;
    public CharacterController CharacterController => characterController;

    public event System.Action<bool> OnCanMoveChanged;

    private void SetCanMove(bool value)
    {
        canMove = value;
        OnCanMoveChanged?.Invoke(CanMove);
    }

    /// <summary>
    /// Adds a movement restriction. Each restriction must be paired with RemoveMovementRestriction().
    /// Movement is only allowed when there are no active restrictions.
    /// </summary>
    public void AddMovementRestriction()
    {
        movementRestrictions++;
        OnCanMoveChanged?.Invoke(CanMove);
    }

    /// <summary>
    /// Removes a movement restriction. Should be called to pair with each AddMovementRestriction() call.
    /// </summary>
    public void RemoveMovementRestriction()
    {
        if (movementRestrictions > 0)
        {
            movementRestrictions--;
            OnCanMoveChanged?.Invoke(CanMove);
        }
    }

    public override void OnNetworkSpawn()
    {
        characterController = GetComponent<CharacterController>();
        animationManager = GetComponent<AnimationManager>();
        pokemon = GetComponent<Pokemon>();
        pokemon.OnEvolution += UpdateAnimations;
        pokemon.OnLevelChange += UpdateSpeed;
        pokemon.OnPokemonInitialized += UpdateSpeed;
        pokemon.OnStatChange += UpdateSpeed;
        canMove = IsOwner;
    }

    private void UpdateSpeed(NetworkListEvent<StatChange> changeEvent)
    {
        UpdateSpeed();
    }

    private void UpdateSpeed()
    {
        moveSpeed = pokemon.GetSpeed() / 1000f;
    }

    void Update()
    {
        if (!CanMove || !IsOwner || isKnockedUp)
        {
            return;
        }

        if (!isDashing)
        {
            Move(InputManager.Instance.Controls.Movement.Move.ReadValue<Vector2>());
        }
        else
        {
            currentMovement += dashDirection * (dashDistance / dashDuration);
        }

        // Apply accumulated movement
        characterController.Move(currentMovement * Time.deltaTime);

        currentMovement = Vector3.zero; // Reset current movement at the start of each frame

        HandleAnimations();
    }

    private void FixedUpdate()
    {
        if (!CanMove || !IsOwner || isKnockedUp || !snapToGround)
        {
            return;
        }

        CheckIfPlayerIsInAWall();
        SnapPlayerToGround();
    }

    private void Move(Vector2 playerInput)
    {
        inputMovement = new Vector3(playerInput.x, 0, playerInput.y);
        currentMovement += inputMovement.normalized * moveSpeed;
        isMoving = false;
        if (inputMovement.magnitude != 0)
        {
            transform.rotation = Quaternion.LookRotation(inputMovement);
            isMoving = true;
        }
    }

    private void SnapPlayerToGround()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit))
        {
            float distanceToGround = hit.distance;
            if (distanceToGround > 0.001f) // Adjust this threshold as needed
            {
                currentMovement += (Vector3.down * (distanceToGround - 0.001f))/Time.deltaTime; // Snap to ground
            }
        }
    }

    // Doesn't quite cover all cases
    private void CheckIfPlayerIsInAWall()
    {
        // Get the player's position and the bounds of the CharacterController
        Vector3 playerPosition = characterController.transform.position;
        Vector3 controllerCenter = playerPosition + characterController.center;
        float controllerRadius = characterController.radius;
        float controllerHeight = characterController.height - 0.5f;

        // Use OverlapCapsule to detect if the player is inside any wall colliders
        Collider[] hitColliders = Physics.OverlapCapsule(controllerCenter + Vector3.up * (controllerHeight / 2),
                                                         controllerCenter - Vector3.up * (controllerHeight / 2),
                                                         controllerRadius,
                                                         wallLayerMask);

        // If there are any colliders hit, push the player out of the wall
        if (hitColliders.Length > 0)
        {
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.isTrigger)
                {
                    continue;
                }

                // Compute penetration direction and distance using Physics.ComputePenetration
                Vector3 pushOutDirection;
                float pushOutDistance;

                bool isPenetrating = Physics.ComputePenetration(
                    characterController, playerPosition, characterController.transform.rotation,
                    hitCollider, hitCollider.transform.position, hitCollider.transform.rotation,
                    out pushOutDirection, out pushOutDistance
                );

                if (isPenetrating)
                {
                    // Ensure we don't affect vertical position (gravity) when pushing out horizontally
                    pushOutDirection.y = 0;

                    // Push the player out of the wall
                    Vector3 newPlayerPosition = playerPosition + pushOutDirection.normalized * (pushOutDistance + 0.1f);

                    // Move the player to the new position
                    transform.position = newPlayerPosition;
                }
            }
        }
    }

    public void StartDash()
    {
        characterController.enabled = true;

        if (!isDashing)
        {
            isDashing = true;
            dashDirection = transform.forward;

            // Start dash cooldown coroutine
            StartCoroutine(DashCooldown());
        }
    }

    public void StartDash(Vector3 dashDirection)
    {
        characterController.enabled = true;

        if (!isDashing)
        {
            isDashing = true;
            this.dashDirection = dashDirection;

            // Start dash cooldown coroutine
            StartCoroutine(DashCooldown());
        }
    }

    private IEnumerator DashCooldown()
    {
        yield return new WaitForSeconds(dashDuration);
        isDashing = false;
    }

    private void HandleAnimations()
    {
        if (animationManager.IsAnimatorNull())
        {
            return;
        }

        bool isMoving = inputMovement.magnitude != 0;
        if (isMoving != animationManager.Animator.GetBool("Walking"))
        {
            UpdateAnimations(isMoving);
        }
    }

    private void UpdateAnimations()
    {
        if (animationManager.IsAnimatorNull())
        {
            return;
        }

        bool isMoving = inputMovement.magnitude != 0;
        animationManager.SetBool("Walking", isMoving);
    }

    private void UpdateAnimations(bool isMoving)
    {
        if (animationManager.IsAnimatorNull())
        {
            return;
        }

        animationManager.SetBool("Walking", isMoving);
    }

    public void Knockup(float force, float duration)
    {
        if (!canBeKnockedBack)
        {
            return;
        }
        
        isKnockedUp = true;
        transform.DOJump(transform.position, force, 1, duration).OnComplete(() => isKnockedUp = false);
    }

    public void Knockback(Vector3 direction, float force)
    {
        if (!canBeKnockedBack)
        {
            return;
        }
        StartCoroutine(ApplyKnockback(direction, force));
    }

    private IEnumerator ApplyKnockback(Vector3 direction, float force)
    {
        float knockbackDuration = 0.2f; // Duration of the knockback effect
        float elapsedTime = 0f;

        while (elapsedTime < knockbackDuration)
        {
            currentMovement += direction.normalized * force;
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }
}

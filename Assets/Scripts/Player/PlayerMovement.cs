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
    private PlayerControls controls;
    private Pokemon pokemon;
    private bool canMove = true;
    private bool isMoving = false;
    private bool isKnockedUp = false;
    private bool canBeKnockedBack = true;
    private bool snapToGround = true;

    private bool isDashing = false;
    private Vector3 dashDirection;

    public bool CanMove { get => canMove; set => canMove = value; }
    public bool CanBeKnockedBack { get => canBeKnockedBack; set => canBeKnockedBack = value; }
    public bool SnapToGround { get => snapToGround; set => snapToGround = value; }
    public bool IsKnockedUp => isKnockedUp;
    public bool IsDashing => isDashing;
    public bool IsMoving => isMoving;
    public CharacterController CharacterController => characterController;

    public override void OnNetworkSpawn()
    {
        characterController = GetComponent<CharacterController>();
        animationManager = GetComponent<AnimationManager>();
        pokemon = GetComponent<Pokemon>();
        pokemon.OnEvolution += UpdateAnimations;
        pokemon.OnLevelChange += UpdateSpeed;
        pokemon.OnPokemonInitialized += UpdateSpeed;
        pokemon.OnStatChange += UpdateSpeed;
        if (IsOwner)
        {
            controls = new PlayerControls();
            controls.asset.Enable();
        }
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
        if (!canMove || !IsOwner || isKnockedUp)
        {
            return;
        }

        if (!isDashing)
        {
            Move(controls.Movement.Move.ReadValue<Vector2>());
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
        if (!canMove || !IsOwner || isKnockedUp || !snapToGround)
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
        Vector3 playerPosition = characterController.transform.position;
        Vector3 controllerCenter = playerPosition + characterController.center;
        float controllerRadius = characterController.radius;
        float controllerHeight = characterController.height / 2f;

        Collider[] hitColliders = Physics.OverlapCapsule(controllerCenter + Vector3.up * (controllerHeight / 2),
                                                         controllerCenter - Vector3.up * (controllerHeight / 2),
                                                         controllerRadius,
                                                         wallLayerMask);

        if (hitColliders.Length > 0)
        {
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.isTrigger)
                {
                    continue;
                }

                if (hitCollider is MeshCollider meshCollider && !meshCollider.convex)
                {
                    RaycastHit hit;
                    Vector3 rayDirection = (controllerCenter - hitCollider.bounds.center).normalized;

                    if (Physics.Raycast(controllerCenter, rayDirection, out hit, controllerRadius * 2, wallLayerMask))
                    {
                        Vector3 pushOutDirection = hit.normal;

                        pushOutDirection.y = 0;

                        Vector3 newPlayerPosition = playerPosition + pushOutDirection * (controllerRadius + 0.1f);

                        transform.position = newPlayerPosition;
                    }
                }
                else
                {
                    Vector3 closestPoint = hitCollider.ClosestPoint(controllerCenter);

                    if (Vector3.Distance(controllerCenter, closestPoint) < controllerRadius)
                    {
                        Vector3 pushOutDirection = (controllerCenter - closestPoint).normalized;

                        pushOutDirection.y = 0;

                        Vector3 newPlayerPosition = playerPosition + pushOutDirection * (controllerRadius + 0.1f);

                        transform.position = newPlayerPosition;
                    }
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

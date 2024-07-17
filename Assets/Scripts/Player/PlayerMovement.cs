using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 4.1f;
    [SerializeField] private float dashDistance = 5f; // Set your desired dash distance
    [SerializeField] private float dashDuration = 0.2f; // Set your desired dash duration
    [SerializeField] private AnimationManager animationManager;
    private Vector3 inputMovement;
    private Vector3 currentMovement; // Accumulated movement vector
    private CharacterController characterController;
    private PlayerControls controls;
    private Pokemon pokemon;
    private bool canMove = true;
    private bool isMoving = false;
    private bool canBeKnockedBack = true;

    private bool isDashing = false;
    private Vector3 dashDirection;

    public bool CanMove { get => canMove; set => EnableMovement(value); }
    public bool CanBeKnockedBack { get => canBeKnockedBack; set => canBeKnockedBack = value; }
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
        moveSpeed = pokemon.GetSpeed() / 1000f;
    }

    private void UpdateSpeed()
    {
        moveSpeed = pokemon.GetSpeed() / 1000f;
    }

    private void EnableMovement(bool value)
    {
        canMove = value;
        //characterController.enabled = value;
    }

    void Update()
    {
        if (!canMove || !IsOwner)
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
        if (!canMove || !IsOwner)
        {
            return;
        }

        SnapToGround();
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

    void SnapToGround()
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

    [Rpc(SendTo.Owner)]
    public void KnockbackRPC(Vector3 direction, float force)
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

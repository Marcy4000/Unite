using DG.Tweening;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 4.1f;
    [SerializeField] private float dashDistance = 5f; // Set your desired dash distance
    [SerializeField] private float dashDuration = 0.2f; // Set your desired dash duration
    [SerializeField] private AnimationManager animationManager;
    // Layer mask used when snapping the player to the ground (configurable in Inspector)
    [SerializeField] private LayerMask snapToGroundLayerMask = ~0; // default: Everything
    
    private Collider walkableWithWalls;
    private Collider walkableNoWalls;
    private Collider activeWalkmesh;
    
    private Vector3 lastValidPosition;
    
    private Vector3 inputMovement;
    private Vector3 currentMovement; // Accumulated movement vector
    private CharacterController characterController;
    private Pokemon pokemon;
    private PlayerManager player;
    private bool canMove = true;
    private int movementRestrictions = 0; // Reference counter for movement restrictions
    private int movementRestrictionCount = 0; // Internal counter for tracking restrictions
    private bool isMoving = false;
    private bool isKnockedUp = false;
    private Tween knockupTween;
    private uint teleportGeneration;
    private bool canBeKnockedBack = true;
    private bool snapToGround = true;

    private bool isDashing = false;
    private bool isFlying = false;
    private Vector3 dashDirection;
    private float currentDashDistance;
    private float currentDashDuration;

    public bool CanMove { get => canMove && movementRestrictions == 0; set => SetCanMove(value); }
    public bool IsFlying { get => isFlying; set => SetFlying(value); }
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
    
    private void SetFlying(bool value)
    {
        isFlying = value;
        UpdateActiveWalkmesh();
    }
    
    private void UpdateActiveWalkmesh()
    {
        // Don't change walkmesh during dash
        if (isDashing)
        {
            return;
        }
        
        // Dive owns its own traversal mode; never borrow or clear IsFlying.
        activeWalkmesh = isFlying || vaporeonDiveTraversal || flygonDigTraversal ? walkableNoWalls : walkableWithWalls;
    }

    public void AddMovementRestriction()
    {
        movementRestrictions++;
        movementRestrictionCount++;
        if (movementRestrictionCount == 1)
        {
            CanMove = false;
        }
        OnCanMoveChanged?.Invoke(CanMove);
    }

    public void RemoveMovementRestriction()
    {
        if (movementRestrictions > 0)
        {
            movementRestrictions--;
            movementRestrictionCount = Mathf.Max(0, movementRestrictionCount - 1);
            if (movementRestrictionCount == 0)
            {
                CanMove = true;
            }
            OnCanMoveChanged?.Invoke(CanMove);
        }
    }

    public override void OnNetworkSpawn()
    {
        characterController = GetComponent<CharacterController>();
        animationManager = GetComponent<AnimationManager>();
        pokemon = GetComponent<Pokemon>();
        player = GetComponent<PlayerManager>();
        pokemon.OnEvolution += UpdateAnimations;
        pokemon.OnLevelChange += UpdateSpeed;
        pokemon.OnPokemonInitialized += UpdateSpeed;
        pokemon.OnStatChange += UpdateSpeed;
        canMove = IsOwner;
        
        // Initialize walkmesh references
        InitializeWalkmeshes();
        
        // Initialize last valid position to current position
        lastValidPosition = transform.position;
    }
    
    private void InitializeWalkmeshes()
    {
        GameObject walkableWithWallsObj = GameObject.FindGameObjectWithTag("WalkableWithWalls");
        GameObject walkableNoWallsObj = GameObject.FindGameObjectWithTag("WalkableNoWalls");
        
        if (walkableWithWallsObj != null)
        {
            walkableWithWalls = walkableWithWallsObj.GetComponent<Collider>();
        }
        
        if (walkableNoWallsObj != null)
        {
            walkableNoWalls = walkableNoWallsObj.GetComponent<Collider>();
        }
        
        // Default to walls mesh
        activeWalkmesh = walkableWithWalls;
        WalkmeshBoundary.Prepare(walkableWithWalls);
        WalkmeshBoundary.Prepare(walkableNoWalls);
        
        if (walkableWithWalls == null || walkableNoWalls == null)
        {
            Debug.LogError("PlayerMovement: Could not find walkmesh colliders. Make sure objects are tagged with 'WalkableWithWalls' and 'WalkableNoWalls'");
        }
    }
    
    private bool IsPositionValidOnWalkmesh(Vector3 position)
    {
        return WalkmeshBoundary.Contains(activeWalkmesh, position);
    }
    
    private Vector3 GetClosestValidPosition(Vector3 position)
    {
        if (activeWalkmesh == null) return position;
        if (WalkmeshBoundary.TryRecover(activeWalkmesh, position, out var recovered)) return recovered;
        
        // Try to find the closest valid position by sampling in a spiral pattern
        float searchRadius = 0.5f;
        int samples = 8;
        
        // Expanded search - 30 rings instead of 10 (15 unit radius instead of 5)
        for (int ring = 0; ring < 30; ring++)
        {
            float currentRadius = searchRadius * ring;
            
            for (int i = 0; i < samples * (ring + 1); i++)
            {
                float angle = (i / (float)(samples * (ring + 1))) * Mathf.PI * 2f;
                Vector3 testPos = new Vector3(
                    position.x + Mathf.Cos(angle) * currentRadius,
                    position.y,
                    position.z + Mathf.Sin(angle) * currentRadius
                );
                
                if (IsPositionValidOnWalkmesh(testPos))
                {
                    return testPos;
                }
            }
        }
        
        // Ultimate fallback: return last known valid position
        // This ensures we never get permanently stuck
        return lastValidPosition;
    }
    
    private Vector3 GetWallNormal(Vector3 position, Vector3 movementDir)
    {
        if (activeWalkmesh == null) return Vector3.zero;
        
        // Sample positions around the blocked point to find wall direction
        int samples = 8;
        Vector3 averageToValid = Vector3.zero;
        int validSamples = 0;
        
        for (int i = 0; i < samples; i++)
        {
            float angle = (i / (float)samples) * Mathf.PI * 2f;
            float sampleDist = 0.3f;
            
            Vector3 sampleOffset = new Vector3(
                Mathf.Cos(angle) * sampleDist,
                0f,
                Mathf.Sin(angle) * sampleDist
            );
            
            Vector3 samplePos = position + sampleOffset;
            samplePos.y = position.y;
            
            if (IsPositionValidOnWalkmesh(samplePos))
            {
                // This direction leads to valid space
                averageToValid += sampleOffset;
                validSamples++;
            }
        }
        
        if (validSamples > 0)
        {
            // Average direction to valid space
            averageToValid /= validSamples;
            averageToValid.y = 0;
            
            // Wall normal points away from the wall (toward valid space)
            if (averageToValid.magnitude > 0.01f)
            {
                return averageToValid.normalized;
            }
        }
        
        // Fallback: use opposite of movement direction
        return -movementDir;
    }
    
    private Vector3 ClampMovementToWalkmesh(Vector3 currentPos, Vector3 intendedPos)
    {
        if (activeWalkmesh == null) return intendedPos;
        
        // If intended position is valid, use it
        if (IsPositionValidOnWalkmesh(intendedPos))
        {
            return intendedPos;
        }
        if (WalkmeshBoundary.TrySlide(activeWalkmesh, currentPos, intendedPos, out var sliding)) return sliding;
        
        // Movement is blocked - use vector projection for wall sliding
        Vector3 movement = (intendedPos - currentPos);
        movement.y = 0; // Only care about XZ movement
        float movementDist = movement.magnitude;
        
        if (movementDist < 0.001f)
        {
            return currentPos; // No movement
        }
        
        Vector3 movementDir = movement.normalized;
        
        // Get the wall normal at the collision point
        Vector3 wallNormal = GetWallNormal(intendedPos, movementDir);
        
        // Project movement vector onto the wall surface (perpendicular to wall normal)
        // Formula: projectedVector = vector - (vector · normal) * normal
        Vector3 projectedMovement = movement - Vector3.Dot(movement, wallNormal) * wallNormal;
        projectedMovement.y = 0; // Keep it on XZ plane
        
        // Try moving in the projected direction
        if (projectedMovement.sqrMagnitude > 0.00000001f)
        {
            Vector3 projectedPos = currentPos + projectedMovement;
            projectedPos.y = intendedPos.y; // Keep intended Y
            
            if (IsPositionValidOnWalkmesh(projectedPos))
            {
                return projectedPos;
            }
            
            // If full projected movement doesn't work, try partial movement
            float slideDistance = projectedMovement.magnitude;
            float minDist = 0f;
            float maxDist = slideDistance;
            Vector3 bestPos = currentPos;
            
            for (int i = 0; i < 8; i++)
            {
                float testDist = (minDist + maxDist) / 2f;
                Vector3 testPos = currentPos + projectedMovement.normalized * testDist;
                testPos.y = intendedPos.y;
                
                if (IsPositionValidOnWalkmesh(testPos))
                {
                    bestPos = testPos;
                    minDist = testDist;
                }
                else
                {
                    maxDist = testDist;
                }
            }
            
            if (bestPos != currentPos)
            {
                return bestPos;
            }
        }
        
        // Fallback: stay at current position
        return currentPos;
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
        if (!CanMove || !IsOwner || isKnockedUp || (player != null && (player.PlayerState == PlayerState.Dead || pokemon.CurrentHp <= 0)))
        {
            return;
        }

        if (!isDashing)
        {
            Move(player != null ? player.MovementInput : InputManager.Instance.Controls.Movement.Move.ReadValue<Vector2>());
        }
        else
        {
            currentMovement += dashDirection * (currentDashDistance / currentDashDuration);
        }

        // Validate and apply accumulated movement
        Vector3 intendedPosition = transform.position + currentMovement * Time.deltaTime;
        Vector3 validatedPosition = ClampMovementToWalkmesh(transform.position, intendedPosition);
        
        // Calculate the actual movement after validation
        Vector3 validatedMovement = (validatedPosition - transform.position) / Time.deltaTime;
        if (flygonDigTraversal) MoveFlygonDig(validatedMovement * Time.deltaTime);
        else if (vaporeonDiveTraversal) MoveVaporeonDive(validatedMovement * Time.deltaTime);
        else characterController.Move(validatedMovement * Time.deltaTime);

        currentMovement = Vector3.zero; // Reset current movement at the start of each frame

        HandleAnimations();
    }

    private void FixedUpdate()
    {
        if (!CanMove || !IsOwner || isKnockedUp || !snapToGround || (player != null && (player.PlayerState == PlayerState.Dead || pokemon.CurrentHp <= 0)))
        {
            return;
        }

        ValidatePlayerPosition();
        // Keep the gameplay root at ground height under terrain; the model
        // supplies Dive's sinking effect. Do not snap onto subterranean meshes.
        if (!vaporeonDiveTraversal && !flygonDigTraversal) SnapPlayerToGround();
    }
    
    private void ValidatePlayerPosition()
    {
        // Track last valid position
        if (IsPositionValidOnWalkmesh(transform.position))
        {
            lastValidPosition = transform.position;
        }
        else
        {
            // Snap player to closest valid position if out of bounds
            transform.position = GetClosestValidPosition(transform.position);
        }
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
        // Use the configurable layer mask so only intended colliders are considered when snapping to ground
        if (Physics.Raycast(transform.position, Vector3.down, out hit, Mathf.Infinity, snapToGroundLayerMask))
        {
            float distanceToGround = hit.distance;
            if (distanceToGround > 0.001f) // Adjust this threshold as needed
            {
                currentMovement += (Vector3.down * (distanceToGround - 0.001f))/Time.deltaTime; // Snap to ground
            }
        }
    }


    public void Teleport(Vector3 position, Quaternion rotation, bool recordSafePosition = true)
    {
        teleportGeneration++;
        knockupTween?.Kill();
        knockupTween = null;
        isKnockedUp = isDashing = isMoving = false;
        currentMovement = inputMovement = Vector3.zero;
        bool restoreController = characterController != null && characterController.enabled;
        if (restoreController) characterController.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        if (recordSafePosition) lastValidPosition = position;
        UpdateActiveWalkmesh();
        if (restoreController) characterController.enabled = true;
    }

    public void StartDash()
    {
        StartDash(transform.forward, dashDistance, dashDuration);
    }

    // For driven movement that must respect walls (unlike StartDash's wall-crossing dash).
    // Returns actual displacement so a blocked move cannot count fictitious travel.
    public Vector3 MoveWithWalls(Vector3 displacement)
    {
        Vector3 start = transform.position;
        // Walkmesh validation checks endpoints. Substeps prevent a fast drive
        // from skipping a thin wall whose far side is also a valid endpoint.
        int steps = Mathf.Clamp(Mathf.CeilToInt(displacement.magnitude / 0.2f), 1, 128);
        Vector3 step = displacement / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector3 before = transform.position;
            Vector3 destination = ClampMovementToWalkmesh(before, before + step);
            characterController.Move(destination - before);
            if ((transform.position - before).sqrMagnitude < step.sqrMagnitude * 0.01f) break;
        }
        return transform.position - start;
    }

    public void StartDash(Vector3 direction)
    {
        StartDash(direction, dashDistance, dashDuration);
    }
    
    public void StartDash(Vector3 direction, float distance, float duration)
    {
        characterController.enabled = true;

        if (!isDashing)
        {
            isDashing = true;
            dashDirection = direction.normalized;
            currentDashDistance = distance;
            currentDashDuration = duration;
            
            // Pre-validate dash with no-walls mesh
            StartCoroutine(DashCooldown(distance, duration));
        }
    }

    private IEnumerator DashCooldown(float distance, float duration)
    {
        uint generation = teleportGeneration;
        // Switch to no-walls walkmesh for dashing
        Collider previousWalkmesh = activeWalkmesh;
        activeWalkmesh = walkableNoWalls;
        
        // Calculate dash endpoint
        Vector3 dashStart = transform.position;
        Vector3 dashEnd = dashStart + dashDirection.normalized * distance;
        
        // Pre-validate dash endpoint on no-walls mesh
        // If endpoint isn't valid, find the furthest valid point along dash path
        if (!IsPositionValidOnWalkmesh(dashEnd))
        {
            // Binary search for maximum valid dash distance
            float minDist = 0f;
            float maxDist = distance;
            float validDist = 0f;
            
            for (int i = 0; i < 10; i++)
            {
                float testDist = (minDist + maxDist) / 2f;
                Vector3 testPos = dashStart + dashDirection.normalized * testDist;
                testPos.y = dashStart.y;
                
                if (IsPositionValidOnWalkmesh(testPos))
                {
                    validDist = testDist;
                    minDist = testDist;
                }
                else
                {
                    maxDist = testDist;
                }
            }
            
            // Adjust dash to only go as far as valid
            if (validDist > 0.1f)
            {
                dashEnd = dashStart + dashDirection.normalized * validDist;
            }
            else
            {
                // Can't dash at all in this direction - cancel dash
                isDashing = false;
                activeWalkmesh = previousWalkmesh;
                yield break;
            }
        }
        
        yield return new WaitForSeconds(duration);
        if (generation != teleportGeneration) yield break;
        isDashing = false;
        
        // Switch back to appropriate mesh after dash (respect flying state)
        UpdateActiveWalkmesh();
        
        // Validate position after dash ends and snap if needed
        if (!IsPositionValidOnWalkmesh(transform.position))
        {
            Vector3 validPosition = GetClosestValidPosition(transform.position);
            transform.position = validPosition;
        }
    }

    private void HandleAnimations()
    {
        if (animationManager.IsAnimatorNull())
        {
            return;
        }

        bool isMoving = inputMovement.magnitude != 0;
        if (!animationManager.TryGetBool(Animator.StringToHash("Walking"), out bool walking) || isMoving != walking)
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
        knockupTween?.Kill();
        knockupTween = transform.DOJump(transform.position, force, 1, duration).OnComplete(() => isKnockedUp = false);
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
        uint generation = teleportGeneration;
        float knockbackDuration = 0.2f; // Duration of the knockback effect
        float elapsedTime = 0f;

        while (elapsedTime < knockbackDuration && generation == teleportGeneration)
        {
            currentMovement += direction.normalized * force;
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 4.1f;
    [SerializeField] private float dashDistance = 5f; // Set your desired dash distance
    [SerializeField] private float dashDuration = 0.2f; // Set your desired dash duration
    [SerializeField] private AnimationManager animationManager;
    private Vector3 inputMovement;
    private CharacterController characterController;
    private PlayerControls controls;
    private Pokemon pokemon;
    private bool canMove = true;
    private bool lastValue = true;

    private bool isDashing = false;
    private Vector3 dashDirection;

    public bool CanMove { get => canMove; set => canMove = value; }
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

    private void UpdateSpeed()
    {
        moveSpeed = pokemon.GetSpeed() / 1000f;
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
            characterController.Move(dashDirection * Time.deltaTime * (dashDistance / dashDuration));
        }

        SnapToGround();

        HandleAnimations();
    }

    private void Move(Vector2 playerInput)
    {
        inputMovement = new Vector3(playerInput.x, 0, playerInput.y);
        characterController.Move(inputMovement.normalized * moveSpeed * Time.deltaTime);
        if (inputMovement.magnitude != 0)
        {
            transform.rotation = Quaternion.LookRotation(inputMovement);
        }
    }

    void SnapToGround()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit))
        {
            float distanceToGround = hit.distance;
            if (distanceToGround > 0.01f) // Adjust this threshold as needed
            {
                characterController.Move(Vector3.down * (distanceToGround - 0.01f)); // Snap to ground
            }
        }
    }

    public void StartDash()
    {
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
        if (isMoving != lastValue)
        {
            UpdateAnimations(isMoving);
            lastValue = isMoving;
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
        StartCoroutine(ApplyKnockback(direction, force));
    }

    private IEnumerator ApplyKnockback(Vector3 direction, float force)
    {
        float knockbackDuration = 0.2f; // Duration of the knockback effect
        float elapsedTime = 0f;

        while (elapsedTime < knockbackDuration)
        {
            characterController.Move(direction.normalized * force * Time.deltaTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }
}

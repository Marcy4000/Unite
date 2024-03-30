using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private Animator cinderace;
    [SerializeField] private float dashDistance = 5f; // Set your desired dash distance
    [SerializeField] private float dashDuration = 0.2f; // Set your desired dash duration
    private Vector3 inputMovement;
    private CharacterController characterController;
    private PlayerControls controls;
    private Pokemon pokemon;
    private bool canMove = true;

    private bool isDashing = false;
    private Vector3 dashDirection;

    public bool CanMove { get => canMove; set => canMove = value; }

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        pokemon = GetComponent<Pokemon>();
        pokemon.OnEvolution += AssignNewAnimator;
        AssignNewAnimator();
        controls = new PlayerControls();
        controls.asset.Enable();
    }

    void Update()
    {
        if (!canMove)
        {
            return;
        }

        if (!isDashing)
        {
            inputMovement = new Vector3(controls.Movement.Move.ReadValue<Vector2>().x, 0, controls.Movement.Move.ReadValue<Vector2>().y);
            characterController.Move(inputMovement.normalized * Time.deltaTime * moveSpeed);
            if (inputMovement.magnitude != 0)
            {
                transform.rotation = Quaternion.LookRotation(inputMovement);
            }
        }
        else
        {
            characterController.Move(dashDirection * Time.deltaTime * (dashDistance / dashDuration));
        }

        HandleAnimations();
    }

    public void StartDash()
    {
        if (!isDashing)
        {
            isDashing = true;
            dashDirection = transform.forward; // Dash forward relative to player's current facing direction

            // Start dash cooldown coroutine
            StartCoroutine(DashCooldown());
        }
    }

    public void StartDash(Vector3 dashDirection)
    {
        if (!isDashing)
        {
            isDashing = true;
            this.dashDirection = dashDirection; // Dash forward relative to player's current facing direction

            // Start dash cooldown coroutine
            StartCoroutine(DashCooldown());
        }
    }

    private IEnumerator DashCooldown()
    {
        yield return new WaitForSeconds(dashDuration);
        isDashing = false;
    }

    private void AssignNewAnimator()
    {
        cinderace = pokemon.ActiveModel.GetComponentInChildren<Animator>();
    }

    private void HandleAnimations()
    {
        if (cinderace == null)
        {
            return;
        }

        cinderace.SetBool("Walking", inputMovement.magnitude == 0);
    }
}

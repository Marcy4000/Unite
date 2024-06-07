using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Aim : NetworkBehaviour
{
    public static Aim Instance { get; private set; }
    [SerializeField] private LayerMask targetMask;
    [SerializeField] private GameObject autoAimIndicator, indicatorHolders, circleIndicator, dashIndicator, skillShotLine;
    [SerializeField] private GameObject basicAtkIndicator;
    private Transform playerTransform;
    private PlayerControls controls;
    [SerializeField]private float coneAngle = 60f; // Cone angle for the sure hit aim
    private float coneDistance = 10f;
    private Collider[] collidersBuffer; // Buffer to store colliders
    private Collider playerCollider; // Collider of the player character
    public int maxEnemies = 10; // Maximum number of enemies to detect
    private bool teamToIgnore;

    public bool TeamToIgnore { get => teamToIgnore; set => teamToIgnore = value; } 

    private void Start()
    {
        if (!IsOwner)
        {
            Destroy(indicatorHolders);
            enabled = false;
            return;
        }
        Instance = this;

        // Initialize the colliders buffer
        collidersBuffer = new Collider[maxEnemies];

        // Get the collider of the player character
        playerCollider = GetComponent<Collider>();

        playerTransform = transform;
        controls = new PlayerControls();
        controls.asset.Enable();
        autoAimIndicator.SetActive(false);
        circleIndicator.SetActive(false);
        dashIndicator.SetActive(false);
        skillShotLine.SetActive(false);
        indicatorHolders.transform.SetParent(null);
    }

    private void Update()
    {
        indicatorHolders.transform.position = playerTransform.position;
    }

    public void HideAutoAim()
    {
        autoAimIndicator.SetActive(false);
        circleIndicator.SetActive(false);
    }

    public void InitializeDashAim(float distance)
    {
        dashIndicator.SetActive(true);
        circleIndicator.SetActive(true);
        dashIndicator.transform.localScale = new Vector3(distance/2, 1, distance*1.5f);
        circleIndicator.transform.localScale = new Vector3(distance / 2.5f, 1, distance / 2.5f);
    }

    public void InitializeAutoAim(float distance, float angle)
    {
        autoAimIndicator.SetActive(true);
        circleIndicator.SetActive(true);
        coneDistance = distance;
        coneAngle = angle;
        autoAimIndicator.transform.localScale = new Vector3(coneAngle / 25f, 1, coneDistance / 5f);
        circleIndicator.transform.localScale = new Vector3(coneDistance / 2.5f, 1, coneDistance / 2.5f);
    }

    public void InitializeSkillshotAimAim(float distance)
    {
        skillShotLine.SetActive(true);
        circleIndicator.SetActive(true);
        skillShotLine.transform.localScale = new Vector3(1f, 1f, distance / 2.5f);
        circleIndicator.transform.localScale = new Vector3(distance / 2.5f, 1f, distance / 2.5f);
    }

    public void HideDashAim()
    {
        dashIndicator.SetActive(false);
        circleIndicator.SetActive(false);
    }

    public void HideSkillshotAim()
    {
        skillShotLine.SetActive(false);
        circleIndicator.SetActive(false);
    }

    public void ShowBasicAtk(bool show, float radius)
    {
        basicAtkIndicator.SetActive(show);
        basicAtkIndicator.transform.localScale = new Vector3(radius / 2.5f, 1, radius / 2.5f);
    }

    public GameObject AimInCircle(float attackRadius)
    {
        // Find enemies within the attack radius using OverlapSphereNonAlloc
        int numEnemies = Physics.OverlapSphereNonAlloc(transform.position, attackRadius, collidersBuffer, targetMask);

        GameObject closestEnemy = null;
        float closestDistance = Mathf.Infinity;

        // Iterate through detected enemies
        for (int i = 0; i < numEnemies; i++)
        {
            // Skip the player character's collider
            if (collidersBuffer[i] == playerCollider || collidersBuffer[i].CompareTag("VisionController"))
                continue;

            if (collidersBuffer[i].CompareTag("Player"))
            {
                var playerManager = collidersBuffer[i].GetComponent<PlayerManager>();
                if (playerManager.OrangeTeam == teamToIgnore)
                {
                    continue;
                }
            }

            float distance = Vector3.Distance(transform.position, collidersBuffer[i].transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = collidersBuffer[i].gameObject;
            }
        }

        return closestEnemy;
    }

    public GameObject SureHitAim()
    {
        // Rotate aiming direction based on right stick input
        float horizontalInput = controls.Movement.AimMove.ReadValue<Vector2>().x;
        float verticalInput = controls.Movement.AimMove.ReadValue<Vector2>().y;

        Vector3 aimDirection = new Vector3(horizontalInput, 0f, verticalInput).normalized;

        autoAimIndicator.transform.rotation = Quaternion.LookRotation(aimDirection);
        autoAimIndicator.transform.localScale = new Vector3(coneAngle / 25f, 1, coneDistance / 5f);

        circleIndicator.transform.localScale = new Vector3(coneDistance/2.5f, 1, coneDistance/2.5f);

        // Perform cone-based targeting
        RaycastHit[] hits = Physics.SphereCastAll(playerTransform.position, 3f, aimDirection.normalized, coneDistance-3, targetMask);

        // Convert angle from degrees to radians
        float angleInRadians = coneAngle * Mathf.Deg2Rad;
        float cosOfAngle = Mathf.Cos(angleInRadians);

        foreach (RaycastHit hit in hits)
        {
            Vector3 toTarget = (hit.point - transform.position).normalized;
            float dot = Vector3.Dot(aimDirection.normalized, toTarget);
            if (dot >= cosOfAngle)
            {
                if (hit.collider.gameObject == gameObject)
                {
                    continue;
                }

                if (hit.collider.CompareTag("Player"))
                {
                    var playerManager = hit.collider.gameObject.GetComponent<PlayerManager>();
                    if (playerManager.OrangeTeam == teamToIgnore)
                    {
                        continue;
                    }
                }
                return hit.collider.gameObject;
                // Handle targeting here, such as highlighting the target
            }
        }

        return null;
    }

    public Vector3 DashAim()
    {
        // Rotate aiming direction based on right stick input
        float horizontalInput = controls.Movement.AimMove.ReadValue<Vector2>().x;
        float verticalInput = controls.Movement.AimMove.ReadValue<Vector2>().y;

        Vector3 aimDirection = new Vector3(horizontalInput, 0f, verticalInput).normalized;

        dashIndicator.transform.rotation = Quaternion.LookRotation(aimDirection);

        return aimDirection;
    }

    public Vector3 SkillshotAim()
    {
        // Rotate aiming direction based on right stick input
        float horizontalInput = controls.Movement.AimMove.ReadValue<Vector2>().x;
        float verticalInput = controls.Movement.AimMove.ReadValue<Vector2>().y;

        Vector3 aimDirection = new Vector3(horizontalInput, 0f, verticalInput).normalized;

        skillShotLine.transform.rotation = Quaternion.LookRotation(aimDirection);

        return aimDirection;
    }
}
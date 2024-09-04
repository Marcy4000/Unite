using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum AimTarget { Enemy, Ally, Wild, NonAlly, All }

public class Aim : NetworkBehaviour
{
    public static Aim Instance { get; private set; }

    public const int MAX_ENEMIES = 15; // Maximum number of enemies to detect

    private static readonly StatusType[] invulnerableStatuses = { StatusType.Invincible, StatusType.Untargetable, StatusType.Invisible };

    [SerializeField] private LayerMask targetMask;
    [SerializeField] private GameObject autoAimIndicator, indicatorHolders, circleIndicator, dashIndicator, skillShotLine, circleAreaIndicator;
    [SerializeField] private GameObject glaceonUniteIndicator, hyperVoiceIndicator;
    [SerializeField] private GameObject basicAtkIndicator;
    private Transform playerTransform;
    private PlayerControls controls;
    [SerializeField]private float coneAngle = 60f; // Cone angle for the sure hit aim
    private float coneDistance = 10f;
    private Collider[] collidersBuffer; // Buffer to store colliders
    private Collider playerCollider; // Collider of the player character

    private AimTarget autoaimTarget;

    private float circleAimSpeed = 20f;

    private Vector3 circleAimPosition;
    private float maxCircleAimRadius;

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
        collidersBuffer = new Collider[MAX_ENEMIES];

        // Get the collider of the player character
        playerCollider = GetComponent<Collider>();

        playerTransform = transform;
        controls = new PlayerControls();
        controls.asset.Enable();

        autoAimIndicator.SetActive(false);
        circleIndicator.SetActive(false);
        dashIndicator.SetActive(false);
        skillShotLine.SetActive(false);
        glaceonUniteIndicator.SetActive(false);
        basicAtkIndicator.SetActive(false);
        hyperVoiceIndicator.SetActive(false);
        circleAreaIndicator.SetActive(false);
        indicatorHolders.transform.SetParent(null);
        indicatorHolders.transform.rotation = Quaternion.identity;
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

    public void InitializeAutoAim(float distance, float angle, AimTarget target)
    {
        autoAimIndicator.SetActive(true);
        circleIndicator.SetActive(true);
        coneDistance = distance;
        coneAngle = angle;
        autoAimIndicator.transform.localScale = new Vector3(coneAngle / 25f, 1, coneDistance / 5f);
        circleIndicator.transform.localScale = new Vector3(coneDistance / 2.5f, 1, coneDistance / 2.5f);
        autoaimTarget = target;
    }

    public void InitializeSkillshotAim(float distance)
    {
        skillShotLine.SetActive(true);
        circleIndicator.SetActive(true);
        skillShotLine.transform.localScale = new Vector3(1f, 1f, distance / 2.5f);
        circleIndicator.transform.localScale = new Vector3(distance / 2.5f, 1f, distance / 2.5f);
    }

    public void InitializeCircleAreaIndicator(float maxRadius)
    {
        circleAreaIndicator.SetActive(true);
        circleIndicator.SetActive(true);
        circleIndicator.transform.localScale = new Vector3(maxRadius / 2.5f, 1, maxRadius / 2.5f);
        circleAreaIndicator.transform.position = new Vector3(playerTransform.position.x, circleAreaIndicator.transform.position.y, playerTransform.position.z);
        circleAimPosition = Vector3.zero;
        maxCircleAimRadius = maxRadius;
    }

    public void InitializeSimpleCircle(float radius)
    {
        circleIndicator.SetActive(true);
        circleIndicator.transform.localScale = new Vector3(radius / 2.5f, 1f, radius / 2.5f);
    }

    public void HideSimpleCircle()
    {
        circleIndicator.SetActive(false);
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
        basicAtkIndicator.transform.localScale = new Vector3(radius, 1, radius);
    }

    public void InitializeGlaceonUniteAim()
    {
        glaceonUniteIndicator.SetActive(true);
        circleIndicator.SetActive(true);
        circleIndicator.transform.localScale = new Vector3(6.2f, 1f, 6.2f);
    }

    public void HideGlaceonUniteAim()
    {
        glaceonUniteIndicator.SetActive(false);
        circleIndicator.SetActive(false);
    }

    public void HideCircleAreaIndicator()
    {
        circleAreaIndicator.SetActive(false);
        circleIndicator.SetActive(false);
    }

    public void InitializeHyperVoiceAim()
    {
        hyperVoiceIndicator.SetActive(true);
        circleIndicator.SetActive(true);
        circleIndicator.transform.localScale = new Vector3(2.5f, 1f, 2.5f);
    }

    public void HideHyperVoiceAim()
    {
        hyperVoiceIndicator.SetActive(false);
        circleIndicator.SetActive(false);
    }

    public GameObject AimInCircle(float attackRadius, PokemonType priority = PokemonType.Player)
    {
        // Find enemies within the attack radius using OverlapSphereNonAlloc
        int numEnemies = Physics.OverlapSphereNonAlloc(transform.position, attackRadius, collidersBuffer, targetMask);

        GameObject closestEnemy = null;
        float closestDistance = Mathf.Infinity;

        // Initialize variables to track the highest priority target
        GameObject highestPriorityTarget = null;
        float highestPriorityDistance = Mathf.Infinity;

        // Iterate through detected enemies
        for (int i = 0; i < numEnemies; i++)
        {
            // Skip the player character's collider
            if (collidersBuffer[i] == playerCollider || collidersBuffer[i].CompareTag("VisionController"))
                continue;

            if (!CanPokemonBeTargeted(collidersBuffer[i].gameObject, AimTarget.NonAlly, TeamToIgnore, false))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, collidersBuffer[i].transform.position);

            // Check if the target matches the priority
            if ((priority == PokemonType.Player && collidersBuffer[i].CompareTag("Player")) ||
                (priority == PokemonType.Wild && collidersBuffer[i].CompareTag("WildPokemon")))
            {
                // Update highest priority target if closer
                if (distance < highestPriorityDistance)
                {
                    highestPriorityDistance = distance;
                    highestPriorityTarget = collidersBuffer[i].gameObject;
                }
            }
            else
            {
                // Update closest target if no priority target found
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = collidersBuffer[i].gameObject;
                }
            }
        }

        // Return the highest priority target if found, otherwise the closest target
        return highestPriorityTarget != null ? highestPriorityTarget : closestEnemy;
    }

    public GameObject[] AimInCircleAtPosition(Vector3 position, float radius, AimTarget target)
    {
        return AimInCircleAtPosition(position, radius, target, teamToIgnore);
    }

    public GameObject[] AimInCircleAtPosition(Vector3 position, float radius, AimTarget target, bool teamToIgnore, bool canHitInvisTargets = true)
    {
        List<GameObject> foundTargets = new List<GameObject>();

        Collider[] hitColliders = Physics.OverlapSphere(position, radius);

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == gameObject)
            {
                continue;
            }

            if (!CanPokemonBeTargeted(hitCollider.gameObject, target, teamToIgnore, canHitInvisTargets))
            {
                continue;
            }

            foundTargets.Add(hitCollider.gameObject);
        }

        return foundTargets.ToArray();
    }

    public GameObject SureHitAim()
    {
        // Rotate aiming direction based on right stick input
        float horizontalInput = controls.Movement.AimMove.ReadValue<Vector2>().x;
        float verticalInput = controls.Movement.AimMove.ReadValue<Vector2>().y;

        Vector3 aimDirection = new Vector3(horizontalInput, 0f, verticalInput).normalized;

        if (aimDirection.magnitude == 0)
        {
            aimDirection = playerTransform.forward;
        }

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

                if (!CanPokemonBeTargeted(hit.collider.gameObject, autoaimTarget, TeamToIgnore, false))
                {
                    continue;
                }

                return hit.collider.gameObject;
            }
        }

        return null;
    }

    public Vector3 CircleAreaAim()
    {
#if UNITY_ANDROID
        // Read input
        Vector2 input = controls.Movement.AimMove.ReadValue<Vector2>();

        circleAimPosition = new Vector3(input.x, 0, input.y) * maxCircleAimRadius;
#else
        // Read input and normalize it
        Vector2 input = controls.Movement.AimMove.ReadValue<Vector2>().normalized;

        // Calculate the new position based on input and speed
        float xPos = circleAimPosition.x + (input.x * Time.deltaTime * circleAimSpeed);
        float zPos = circleAimPosition.z + (input.y * Time.deltaTime * circleAimSpeed);

        // Update the aim position
        circleAimPosition = new Vector3(xPos, 0, zPos);

        // Clamp the position within the allowed radius
        if (circleAimPosition.magnitude > maxCircleAimRadius)
        {
            circleAimPosition = circleAimPosition.normalized * maxCircleAimRadius;
        }
#endif

        // Update the indicator position
        circleAreaIndicator.transform.localPosition = new Vector3(circleAimPosition.x, circleAreaIndicator.transform.localPosition.y, circleAimPosition.z);

        return circleAreaIndicator.transform.position;
    }


    public Vector3 DashAim()
    {
        // Rotate aiming direction based on right stick input
        float horizontalInput = controls.Movement.AimMove.ReadValue<Vector2>().x;
        float verticalInput = controls.Movement.AimMove.ReadValue<Vector2>().y;

        Vector3 aimDirection = new Vector3(horizontalInput, 0f, verticalInput).normalized;

        if (aimDirection.magnitude == 0)
        {
            aimDirection = playerTransform.forward;
        }

        dashIndicator.transform.rotation = Quaternion.LookRotation(aimDirection);

        return aimDirection;
    }

    public Vector3 SkillshotAim()
    {
        // Rotate aiming direction based on right stick input
        float horizontalInput = controls.Movement.AimMove.ReadValue<Vector2>().x;
        float verticalInput = controls.Movement.AimMove.ReadValue<Vector2>().y;

        Vector3 aimDirection = new Vector3(horizontalInput, 0f, verticalInput).normalized;

        if (aimDirection.magnitude == 0)
        {
            aimDirection = playerTransform.forward;
        }

        skillShotLine.transform.rotation = Quaternion.LookRotation(aimDirection);
        glaceonUniteIndicator.transform.rotation = Quaternion.LookRotation(aimDirection);
        hyperVoiceIndicator.transform.rotation = Quaternion.LookRotation(aimDirection);

        return aimDirection;
    }

    public bool CanPokemonBeTargeted(GameObject pokemonObject, AimTarget targetType, bool teamToIgnore, bool canHitInvisTargets=true)
    {
        if (pokemonObject.TryGetComponent(out Pokemon pokemon))
        {
            if (pokemon.HasAnyStatusEffect(invulnerableStatuses))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        switch (targetType)
        {
            case AimTarget.Enemy:
                if (pokemonObject.GetComponent<WildPokemon>())
                {
                    return false;
                }

                if (pokemonObject.CompareTag("Player"))
                {
                    var playerManager = pokemonObject.GetComponent<PlayerManager>();
                    if (playerManager.OrangeTeam == teamToIgnore)
                    {
                        return false;
                    }
                }

                if (pokemonObject.CompareTag("SoldierPokemon"))
                {
                    var soldierPokemon = pokemonObject.GetComponent<SoldierPokemon>();
                    if (soldierPokemon.OrangeTeam == teamToIgnore)
                    {
                        return false;
                    }
                }
                break;
            case AimTarget.Ally:
                if (pokemonObject.GetComponent<WildPokemon>())
                {
                    return false;
                }

                if (pokemonObject.CompareTag("Player"))
                {
                    var playerManager = pokemonObject.GetComponent<PlayerManager>();
                    if (playerManager.OrangeTeam != teamToIgnore)
                    {
                        return false;
                    }
                }

                if (pokemonObject.CompareTag("SoldierPokemon"))
                {
                    var soldierPokemon = pokemonObject.GetComponent<SoldierPokemon>();
                    if (soldierPokemon.OrangeTeam != teamToIgnore)
                    {
                        return false;
                    }
                }
                break;
            case AimTarget.Wild:
                if (!pokemonObject.GetComponent<WildPokemon>())
                {
                    return false;
                }
                break;
            case AimTarget.NonAlly:
                if (pokemonObject.CompareTag("Player"))
                {
                    var playerManager = pokemonObject.GetComponent<PlayerManager>();
                    if (playerManager.OrangeTeam == teamToIgnore)
                    {
                        return false;
                    }
                }

                if (pokemonObject.CompareTag("SoldierPokemon"))
                {
                    var soldierPokemon = pokemonObject.GetComponent<SoldierPokemon>();
                    if (soldierPokemon.OrangeTeam == teamToIgnore)
                    {
                        return false;
                    }
                }
                break;
        }

        if (pokemonObject.TryGetComponent(out Vision vision))
        {
            if ((!vision.IsRendered || !vision.IsVisible) && !canHitInvisTargets)
            {
                return false;
            }
        }

        return true;
    }
}
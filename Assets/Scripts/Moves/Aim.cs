using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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

    private Team teamToIgnore;

    private string activeControlScheme;
    private float deviceSwitchCooldown = 0.5f; // Time to prevent rapid switching
    private float lastInputTime;

    public Team TeamToIgnore { get => teamToIgnore; set => teamToIgnore = value; } 

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
        controls = InputManager.Instance.Controls;

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
        DetectActiveDevice();
    }

    private void DetectActiveDevice()
    {
        if (Time.time - lastInputTime < deviceSwitchCooldown) return;

        if (Keyboard.current.wasUpdatedThisFrame)
        {
            SetActiveControlScheme("Keyboard");
        }
        else if (Gamepad.current?.wasUpdatedThisFrame == true)
        {
            SetActiveControlScheme("Controller");
        }
    }

    private void SetActiveControlScheme(string scheme)
    {
        if (activeControlScheme != scheme)
        {
            activeControlScheme = scheme;
            Debug.Log($"Switched to {scheme} control scheme");
        }

        lastInputTime = Time.time;
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

    public GameObject[] AimInCircleAtPosition(Vector3 position, float radius, AimTarget target, TeamMember teamToIgnore, bool canHitInvisTargets = true)
    {
        return AimInCircleAtPosition(position, radius, target, teamToIgnore.Team, canHitInvisTargets);
    }

    public GameObject[] AimInCircleAtPosition(Vector3 position, float radius, AimTarget target, Team teamToIgnore, bool canHitInvisTargets = true)
    {
        List<GameObject> foundTargets = new List<GameObject>();

        Collider[] hitColliders = Physics.OverlapSphere(position, radius);

        foreach (var hitCollider in hitColliders)
        {
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
        Vector3 aimDirection;

        if (activeControlScheme == "Keyboard")
        {
            // Mouse aiming
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Vector3 playerScreenPosition = Camera.main.WorldToScreenPoint(playerTransform.position);
            Vector2 screenDirection = mousePosition - (Vector2)playerScreenPosition;
            aimDirection = new Vector3(screenDirection.x, 0f, screenDirection.y).normalized;
        }
        else
        {
            // Controller stick aiming
            Vector2 stickInput = controls.Movement.AimMove.ReadValue<Vector2>();
            aimDirection = new Vector3(stickInput.x, 0f, stickInput.y).normalized;

            if (aimDirection.magnitude == 0)
            {
                aimDirection = playerTransform.forward; // Default to forward if no input
            }
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
        Vector3 aimDirection;

#if UNITY_ANDROID
        // Controller input (Android-specific behavior)
        Vector2 input = controls.Movement.AimMove.ReadValue<Vector2>();
        aimDirection = new Vector3(input.x, 0, input.y) * maxCircleAimRadius;
#else
        if (activeControlScheme == "Keyboard")
        {
            // Mouse aiming
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Vector3 playerScreenPosition = Camera.main.WorldToScreenPoint(playerTransform.position);
            Vector2 screenDirection = mousePosition - (Vector2)playerScreenPosition;
            aimDirection = new Vector3(screenDirection.x, 0f, screenDirection.y).normalized * maxCircleAimRadius;
        }
        else
        {
            // Controller stick input
            Vector2 input = controls.Movement.AimMove.ReadValue<Vector2>().normalized;

            // Calculate the new position based on input and speed
            float xPos = circleAimPosition.x + (input.x * Time.deltaTime * circleAimSpeed);
            float zPos = circleAimPosition.z + (input.y * Time.deltaTime * circleAimSpeed);

            aimDirection = new Vector3(xPos, 0, zPos);

            // Clamp the position within the allowed radius
            if (aimDirection.magnitude > maxCircleAimRadius)
            {
                aimDirection = aimDirection.normalized * maxCircleAimRadius;
            }
        }
#endif

        // Update the indicator position
        circleAreaIndicator.transform.localPosition = new Vector3(aimDirection.x, circleAreaIndicator.transform.localPosition.y, aimDirection.z);

        return circleAreaIndicator.transform.position;
    }


    public Vector3 DashAim()
    {
        Vector3 aimDirection;

        if (activeControlScheme == "Keyboard")
        {
            // Mouse aiming
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Vector3 playerScreenPosition = Camera.main.WorldToScreenPoint(playerTransform.position);
            Vector2 screenDirection = mousePosition - (Vector2)playerScreenPosition;
            aimDirection = new Vector3(screenDirection.x, 0f, screenDirection.y).normalized;
        }
        else
        {
            // Controller stick aiming
            Vector2 stickInput = controls.Movement.AimMove.ReadValue<Vector2>();
            aimDirection = new Vector3(stickInput.x, 0f, stickInput.y).normalized;

            if (aimDirection.magnitude == 0)
            {
                aimDirection = playerTransform.forward; // Default to forward if no input
            }
        }

        dashIndicator.transform.rotation = Quaternion.LookRotation(aimDirection);

        return aimDirection;
    }

    public Vector3 SkillshotAim()
    {
        Vector3 aimDirection;

        if (activeControlScheme == "Keyboard")
        {
            // Mouse aiming
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Vector3 playerScreenPosition = Camera.main.WorldToScreenPoint(playerTransform.position);
            Vector2 screenDirection = mousePosition - (Vector2)playerScreenPosition;
            aimDirection = new Vector3(screenDirection.x, 0f, screenDirection.y).normalized;
        }
        else
        {
            // Controller stick aiming
            Vector2 stickInput = controls.Movement.AimMove.ReadValue<Vector2>();
            aimDirection = new Vector3(stickInput.x, 0f, stickInput.y).normalized;

            if (aimDirection.magnitude == 0)
            {
                aimDirection = playerTransform.forward; // Default to forward if no input
            }
        }

        skillShotLine.transform.rotation = Quaternion.LookRotation(aimDirection);
        glaceonUniteIndicator.transform.rotation = Quaternion.LookRotation(aimDirection);
        hyperVoiceIndicator.transform.rotation = Quaternion.LookRotation(aimDirection);

        return aimDirection;
    }

    public bool CanPokemonBeTargeted(GameObject pokemonObject, AimTarget targetType, Team teamToIgnore, bool canHitInvisTargets = true)
    {
        return CanPokemonBeTargeted(pokemonObject, targetType, new TeamMember(teamToIgnore), canHitInvisTargets);
    }

    public bool CanPokemonBeTargeted(GameObject pokemonObject, AimTarget targetType, TeamMember teamToIgnore, bool canHitInvisTargets=true)
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
                if (pokemon.TeamMember.Team == Team.Neutral)
                {
                    return false;
                }

                if (pokemon.TeamMember.IsOnSameTeam(teamToIgnore))
                {
                    return false;
                }
                break;
            case AimTarget.Ally:
                if (pokemon.TeamMember.Team == Team.Neutral)
                {
                    return false;
                }

                if (!pokemon.TeamMember.IsOnSameTeam(teamToIgnore))
                {
                    return false;
                }
                break;
            case AimTarget.Wild:
                if (pokemon.TeamMember.Team != Team.Neutral)
                {
                    return false;
                }
                break;
            case AimTarget.NonAlly:
                if (pokemon.TeamMember.IsOnSameTeam(teamToIgnore))
                {
                    return false;
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
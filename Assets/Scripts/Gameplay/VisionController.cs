using System.Collections.Generic;
using UnityEngine;

public class VisionController : MonoBehaviour
{
    [SerializeField] private float visionRange = 10f;
    private SphereCollider visionCollider;
    private bool teamToIgnore;
    private bool isEnabled = false;

    public bool TeamToIgnore { get => teamToIgnore; set => teamToIgnore = value; }
    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    private List<Vision> visibleObjects = new List<Vision>();

    void Start()
    {
        visionCollider = gameObject.GetComponent<SphereCollider>();
        visionCollider.radius = visionRange;
    }

    private void Update()
    {
        if (!isEnabled)
        {
            return;
        }

        foreach (Vision vision in visibleObjects)
        {
            if (vision.IsVisible && !vision.IsRendered)
            {
                vision.SetVisibility(true);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isEnabled)
        {
            return;
        }

        Vision vision;
        if (other.TryGetComponent(out vision))
        {
            Debug.Log("Vision found");
            if (vision.HasATeam && vision.CurrentTeam == teamToIgnore)
            {
                return;
            }
            else
            {
                if (vision.IsVisible)
                {
                    vision.SetVisibility(true);
                }
                else
                {
                    vision.SetVisibility(false);
                }

                visibleObjects.Add(vision);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isEnabled)
        {
            return;
        }

        Vision vision;
        if (other.TryGetComponent(out vision))
        {
            if (vision.HasATeam && vision.CurrentTeam == teamToIgnore)
            {
                return;
            }
            visibleObjects.Remove(vision);

            vision.SetVisibility(false);
        }
    }
}

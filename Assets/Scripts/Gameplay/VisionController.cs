using System.Collections.Generic;
using UnityEngine;

public class VisionController : MonoBehaviour
{
    [SerializeField] private float visionRange = 10f;
    private SphereCollider visionCollider;
    private bool teamToIgnore;
    private bool isEnabled = false;
    private bool isBlinded = false;

    public bool TeamToIgnore { get => teamToIgnore; set => teamToIgnore = value; }
    public bool IsEnabled { get => isEnabled; set => UpdateIsEnabled(value); }
    public bool IsBlinded { get => isBlinded; set => UpdateBlindState(value); }

    private List<Vision> visibleObjects = new List<Vision>();

    void Start()
    {
        visionCollider = gameObject.GetComponent<SphereCollider>();
        visionCollider.radius = visionRange;
    }

    private void UpdateBlindState(bool state)
    {
        isBlinded = state;
        if (isBlinded)
        {
            foreach (Vision vision in visibleObjects)
            {
                vision.SetVisibility(false);
            }
        }
        else
        {
            foreach (Vision vision in visibleObjects)
            {
                if (vision.HasATeam && vision.CurrentTeam == teamToIgnore)
                {
                    vision.SetVisibility(true);
                    return;
                }

                if (vision.IsVisible)
                {
                    vision.SetVisibility(true);
                }
                else
                {
                    vision.SetVisibility(false);
                }
            }
        }
    }

    private void UpdateIsEnabled(bool state)
    {
        isEnabled = state;
        if (isEnabled) {
            foreach (Vision vision in visibleObjects)
            {
                if (vision.IsVisible && !vision.IsRendered && !isBlinded)
                {
                    vision.SetVisibility(true);
                }
            }
        }
        else
        {
            foreach (Vision vision in visibleObjects)
            {
                vision.SetVisibility(false);
            }
        }
    }

    private void Update()
    {
        if (!isEnabled)
        {
            return;
        }

        for (int i = visibleObjects.Count; i > 0; i--)
        {
            int index = i - 1;
            if (visibleObjects[index] == null)
            {
                visibleObjects.RemoveAt(index);
            }
            else if (visibleObjects[index].IsVisible && !visibleObjects[index].IsRendered && !IsBlinded)
            {
                visibleObjects[index].SetVisibility(true);
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
            if (vision.HasATeam && vision.CurrentTeam == teamToIgnore)
            {
                return;
            }
            else
            {
                if (vision.IsVisible && !isBlinded)
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

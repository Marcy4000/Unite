using System.Collections.Generic;
using UnityEngine;

// I hate how vision is implemented
public class VisionController : MonoBehaviour
{
    [SerializeField] private float visionRange = 10f;
    private SphereCollider visionCollider;
    private bool teamToIgnore;
    private bool isEnabled = false;
    private bool isBlinded = false;

    private GameObject currentBush = null;

    public bool TeamToIgnore { get => teamToIgnore; set => teamToIgnore = value; }
    public bool IsEnabled { get => isEnabled; set => UpdateIsEnabled(value); }
    public bool IsBlinded { get => isBlinded; set => UpdateBlindState(value); }
    public GameObject CurrentBush { get => currentBush; set => UpdateBush(value);}

    private List<Vision> visibleObjects = new List<Vision>();

    void Start()
    {
        visionCollider = gameObject.GetComponent<SphereCollider>();
        visionCollider.radius = visionRange;
    }

    private void UpdateBush(GameObject bush)
    {
        UpdateBushVision(bush);
        currentBush = bush;
    }

    private void UpdateBushVision(GameObject bush)
    {
        if (bush != null)
        {
            foreach (Vision vision in visibleObjects)
            {
                if (vision.CurrentBush == bush)
                {
                    vision.IsVisibleInBush.Add(gameObject.GetInstanceID());
                }
                else if (vision.IsVisibleInBush.Contains(gameObject.GetInstanceID()))
                {
                    vision.IsVisibleInBush.Remove(gameObject.GetInstanceID());
                }
            }
        }
        else
        {
            foreach (Vision vision in visibleObjects)
            {
                if (vision.CurrentBush == currentBush)
                {
                    vision.IsVisibleInBush.Remove(gameObject.GetInstanceID());
                }
            }
        }
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

                if (vision.IsVisible && (!vision.IsInBush || vision.IsVisibleInBush.Count > 0))
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
                if (vision.IsVisible && !vision.IsRendered && !isBlinded && (!vision.IsInBush || vision.IsVisibleInBush.Count > 0))
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
            UpdateBush(null);
        }
    }

    private void Update()
    {
        if (!isEnabled)
        {
            return;
        }

        UpdateVision();
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
                if (vision.IsVisible && !isBlinded && (!vision.IsInBush || vision.IsVisibleInBush.Count > 0))
                {
                    vision.SetVisibility(true);
                }
                else
                {
                    vision.SetVisibility(false);
                }

                vision.OnBushChanged += OnVisibleObjectBushChange;
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

            vision.OnBushChanged -= OnVisibleObjectBushChange;
            vision.SetVisibility(false);
        }
    }

    private void OnVisibleObjectBushChange(GameObject bush)
    {
        UpdateBushVision(currentBush);
    }

    private void UpdateVision()
    {
        for (int i = visibleObjects.Count; i > 0; i--)
        {
            int index = i - 1;
            if (visibleObjects[index] == null)
            {
                visibleObjects.RemoveAt(index);
            }
            else if (visibleObjects[index].IsVisible && !visibleObjects[index].IsRendered && !IsBlinded && (!visibleObjects[index].IsInBush || visibleObjects[index].IsVisibleInBush.Count > 0))
            {
                visibleObjects[index].SetVisibility(true);
            }
            else if (!visibleObjects[index].IsVisible || IsBlinded || !(!visibleObjects[index].IsInBush || visibleObjects[index].IsVisibleInBush.Count > 0))
            {
                visibleObjects[index].SetVisibility(false);
            }
        }
    }
}

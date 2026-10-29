using System.Collections;
using System.Collections.Generic;
using UI.ThreeDimensional;
using UnityEngine;
using UnityEngine.UI;

public class GameBeginScreenUI : MonoBehaviour
{
    [SerializeField] private RectTransform[] blueTeamPlayers;
    [SerializeField] private RectTransform[] orangeTeamPlayers;

    [SerializeField] private GameObject bluePlayerHolder;
    [SerializeField] private GameObject orangePlayerHolder;

    [SerializeField] private GameObject lineObject;

    [SerializeField] private RectTransform blueTeam;
    [SerializeField] private RectTransform orangeTeam;

    private Coroutine fadeInRoutine;
    private Coroutine fadeOutRoutine;

    private bool presentationRequested;

    private CanvasScaler canvasScaler;

    private Dictionary<int, string> playerAnimationsBlueMale = new Dictionary<int, string>()
    {
        {0, "ani_body02_40040_lob_male" },
        {1, "ani_obpos5idle_40040_lob_male" },
        {2, "ani_body01_40350_lob_male" },
        {3, "ani_win05idle_40350_lob_male" },
        {4, "ani_win03idle_40350_lob_male" },
    };

    private Dictionary<int, string> playerAnimationsOrangeMale = new Dictionary<int, string>()
    {
        {0, "ani_body03_40040_lob_male" },
        {1, "ani_win03idle_40350_lob_male" },
        {2, "ani_body15_40350_lob_male" },
        {3, "ani_pose9_40350_lob_male" },
        {4, "ani_body02_40040_lob_male" },
    };

    private Dictionary<int, string> playerAnimationsBlueFemale = new Dictionary<int, string>()
    {
        {0, "ani_body02_50350_lob_female" },
        {1, "ani_body03_50424_lob_female" },
        {2, "ani_win03idle_50040_lob_female" },
        {3, "ani_win07idle_lob_female" },
        {4, "ani_win05idle_50350_lob_female" },
    };

    private Dictionary<int, string> playerAnimationsOrangeFemale = new Dictionary<int, string>()
    {
        {0, "ani_ready01_lob_female" },
        {1, "ani_win05idle_50350_lob_female" },
        {2, "ani_ready03_lob_female" },
        {3, "ani_pose9_05350_lob_female" },
        {4, "ani_body02_50350_lob_female" },
    };

    private sealed class PlayerSlot
    {
        public RectTransform Rect;
        public Vector2 Position;
        public UIObject3D Preview;
        public Image Placeholder;
        public string PlayerId;
        public string RosterOutfit;
        public TrainerModel Source;
        public TrainerModel Target;
        public PlayerClothesInfo? Outfit;
        public bool Presented;
        public int PoseFrame = -1;
        public int Index;
        public bool Blue;
        public bool IsMale = true;
    }

    [SerializeField] private Sprite[] purpleMalePlaceholders = new Sprite[5];
    [SerializeField] private Sprite[] purpleFemalePlaceholders = new Sprite[5];
    [SerializeField] private Sprite[] orangeMalePlaceholders = new Sprite[5];
    [SerializeField] private Sprite[] orangeFemalePlaceholders = new Sprite[5];
    private PlayerSlot[] slots;
    private int bluePlayerCount;
    private int orangePlayerCount;

    public bool IsClosed { get; private set; }
    public bool IsOpen => !presentationRequested && !gameObject.activeSelf;
    public bool IsVisible => presentationRequested && gameObject.activeInHierarchy;

    private void Start()
    {
        CachePresentation();
        if (!presentationRequested) HideImmediately();
    }

    private void CachePresentation()
    {
        if (slots != null) return;
        canvasScaler = GetComponentInParent<CanvasScaler>(true);
        var cached = new List<PlayerSlot>();
        CacheSlots(blueTeamPlayers, true, cached);
        CacheSlots(orangeTeamPlayers, false, cached);
        slots = cached.ToArray();
    }

    private void CacheSlots(RectTransform[] players, bool blue, List<PlayerSlot> cached)
    {
        for (int index = 0; index < players.Length; index++)
        {
            var preview = players[index].GetComponentInChildren<UIObject3D>(true);
            if (preview != null)
            {
                preview.ManageImageVisibility = false;
                preview.imageComponent.color = Color.clear;
                preview.enabled = false;
            }
            var placeholder = new GameObject("Trainer placeholder", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)placeholder.transform;
            rect.SetParent(players[index], false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = placeholder.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            var slot = new PlayerSlot
            {
                Rect = players[index], Position = players[index].anchoredPosition,
                Preview = preview, Placeholder = image, Index = index, Blue = blue
            };
            UpdatePlaceholderSprite(slot);
            cached.Add(slot);
        }
    }

    private void UpdatePlaceholderSprite(PlayerSlot slot)
    {
        var sprites = slot.Blue
            ? (slot.IsMale ? purpleMalePlaceholders : purpleFemalePlaceholders)
            : (slot.IsMale ? orangeMalePlaceholders : orangeFemalePlaceholders);
        slot.Placeholder.sprite = sprites != null && slot.Index < sprites.Length ? sprites[slot.Index] : null;
        slot.Placeholder.enabled = slot.Placeholder.sprite != null;
    }

    public void InitializeUI(int bluePlayers, int orangePlayers)
    {
        CachePresentation();
        CancelPresentation();
        presentationRequested = true;
        bluePlayerCount = Mathf.Clamp(bluePlayers, 0, blueTeamPlayers.Length);
        orangePlayerCount = Mathf.Clamp(orangePlayers, 0, orangeTeamPlayers.Length);
        lineObject.SetActive(false);
        SetHolderVisibility(0f);
        SetPanelPositions(true, 0f);
        foreach (var slot in slots)
        {
            if (!gameObject.activeInHierarchy && slot.Preview != null) slot.Preview.enabled = false;
            slot.Rect.gameObject.SetActive(slot.Index < (slot.Blue ? bluePlayerCount : orangePlayerCount));
            slot.Presented = false;
            slot.PoseFrame = -1;
            ShowPlaceholder(slot);
        }
        SyncRoster();
        gameObject.SetActive(true);
        StartCoroutine(PreparePlayers());
    }

    private void OnDisable()
    {
        CancelPresentation();
        presentationRequested = false;
    }

    public void HideImmediately()
    {
        CancelPresentation();
        presentationRequested = false;
        if (lineObject != null) lineObject.SetActive(false);
        if (blueTeam != null && orangeTeam != null) SetPanelPositions(true, 0f);
        if (bluePlayerHolder != null && orangePlayerHolder != null) SetHolderVisibility(0f);
        gameObject.SetActive(false);
    }

    private void CancelPresentation()
    {
        StopAllCoroutines();
        fadeInRoutine = null;
        fadeOutRoutine = null;
        IsClosed = false;
        if (slots == null) return;
        foreach (var slot in slots)
        {
            slot.Rect.anchoredPosition = slot.Position;
            slot.Target = null;
            slot.Presented = false;
            slot.PoseFrame = -1;
            ShowPlaceholder(slot);
        }
    }

    public void FadeIn()
    {
        if (!isActiveAndEnabled || !presentationRequested || fadeInRoutine != null || IsClosed) return;
        if (fadeOutRoutine != null) return;
        fadeInRoutine = StartCoroutine(FadeInRoutine());
    }

    public void FadeOut()
    {
        if (!isActiveAndEnabled || !presentationRequested || fadeOutRoutine != null) return;
        if (fadeInRoutine == null && !IsClosed) FadeIn();
        fadeOutRoutine = StartCoroutine(FadeOutRoutine());
    }

    public IEnumerator FadeInRoutine()
    {
        lineObject.SetActive(false);
        SetHolderVisibility(0f);
        SetPanelPositions(true, 0f);
        yield return AnimatePanels(true);
        lineObject.SetActive(true);
        var entering = new List<PlayerSlot>();
        foreach (var slot in slots)
        {
            if (!slot.Rect.gameObject.activeSelf) continue;
            entering.Add(slot);
            slot.Rect.anchoredPosition = slot.Position + Vector2.right * (slot.Blue ? -PanelWidth : PanelWidth);
        }
        SetHolderVisibility(1f);
        float elapsed = 0f;
        float duration = 0.25f + Mathf.Max(0, entering.Count - 1) * 0.07f;
        while (elapsed < duration)
        {
            for (int index = 0; index < entering.Count; index++)
            {
                var slot = entering[index];
                float progress = Mathf.Clamp01((elapsed - index * 0.07f) / 0.25f);
                slot.Rect.anchoredPosition = slot.Position + Vector2.right *
                    (slot.Blue ? -PanelWidth : PanelWidth) * (1f - progress) * (1f - progress);
            }
            yield return null;
            elapsed += AnimationDelta;
        }
        foreach (var slot in entering) slot.Rect.anchoredPosition = slot.Position;
        IsClosed = true;
        fadeInRoutine = null;
    }

    private float PanelWidth => canvasScaler != null ? canvasScaler.referenceResolution.x : ((RectTransform)transform).rect.width;
    private static float AnimationDelta => Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

    private void SetPanelPositions(bool closing, float progress)
    {
        float offset = closing ? 1f - progress : progress;
        blueTeam.anchoredPosition = new Vector2(-PanelWidth * offset, 0f);
        orangeTeam.anchoredPosition = new Vector2(PanelWidth * offset, 0f);
    }

    private IEnumerator AnimatePanels(bool closing)
    {
        float elapsed = 0f;
        SetPanelPositions(closing, 0f);
        yield return null;
        while (elapsed < 0.3f)
        {
            elapsed += AnimationDelta;
            SetPanelPositions(closing, Mathf.Clamp01(elapsed / 0.3f));
            yield return null;
        }
        SetPanelPositions(closing, 1f);
    }

    private void SetHolderVisibility(float alpha)
    {
        foreach (var holder in new[] { bluePlayerHolder, orangePlayerHolder })
        {
            var group = holder.GetComponent<CanvasGroup>();
            if (group == null) group = holder.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            holder.SetActive(true);
        }
    }

    private IEnumerator FadeOutRoutine()
    {
        while (!IsClosed) yield return null;
        IsClosed = false;
        lineObject.SetActive(false);
        yield return AnimatePanels(false);
        HideImmediately();
    }

    private IEnumerator PreparePlayers()
    {
        while (presentationRequested)
        {
            yield return null;
            SyncRoster();
            foreach (var slot in slots)
            {
                if (slot.Rect.gameObject.activeSelf) PrepareSlot(slot);
                yield return null;
            }
        }
    }

    private void SyncRoster()
    {
        if (LobbyController.Instance == null) return;
        var blue = LobbyController.Instance.GetTeamPlayers(Team.Blue);
        var orange = LobbyController.Instance.GetTeamPlayers(Team.Orange);
        foreach (var slot in slots)
        {
            var roster = slot.Blue ? blue : orange;
            var player = slot.Rect.gameObject.activeSelf && slot.Index < roster.Length ? roster[slot.Index] : null;
            string id = player != null ? player.Id : null;
            string outfit = player != null && player.Data.TryGetValue("ClothingInfo", out var property) ? property.Value : null;
            if (slot.PlayerId == id && slot.RosterOutfit == outfit) continue;
            ResetSlot(slot);
            slot.PlayerId = id;
            slot.RosterOutfit = outfit;
            slot.IsMale = string.IsNullOrEmpty(outfit) || PlayerClothesInfo.Deserialize(outfit).IsMale;
            UpdatePlaceholderSprite(slot);
        }
    }

    private static void ShowPlaceholder(PlayerSlot slot)
    {
        slot.Placeholder.gameObject.SetActive(true);
        if (slot.Preview != null) slot.Preview.imageComponent.color = Color.clear;
    }

    private void LateUpdate()
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot.Preview == null || !slot.Rect.gameObject.activeSelf) continue;
            if (!slot.Preview.IsReady || !slot.Preview.HasRendered || slot.Target == null ||
                slot.Preview.GetTargetInstance() != slot.Target.transform ||
                slot.Target.IsLoading || !slot.Target.IsInitialized)
            {
                slot.Presented = false;
                slot.PoseFrame = -1;
                ShowPlaceholder(slot);
            }
        }
    }

    private static void ResetSlot(PlayerSlot slot)
    {
        ShowPlaceholder(slot);
        slot.Source = null;
        slot.Target = null;
        slot.Outfit = null;
        slot.Presented = false;
        slot.PoseFrame = -1;
        if (slot.Preview == null) return;
        slot.Preview.enabled = false;
        if (slot.Preview.ObjectPrefab != null) slot.Preview.ObjectPrefab = null;
    }

    private void PrepareSlot(PlayerSlot slot)
    {
        if (slot.Preview == null || string.IsNullOrEmpty(slot.PlayerId)) return;
        var sourceObject = PlayerClothesPreloader.Instance != null
            ? PlayerClothesPreloader.Instance.GetPlayerModel(slot.PlayerId) : null;
        var source = sourceObject != null ? sourceObject.GetComponent<TrainerModel>() : null;
        if (source == null || source.IsLoading || !source.IsInitialized)
        {
            if (slot.Source != null || slot.Target != null) ResetSlot(slot);
            return;
        }
        if (slot.Source != source || !slot.Outfit.HasValue || !slot.Outfit.Value.Equals(source.PlayerClothesInfo))
        {
            ResetSlot(slot);
            slot.Source = source;
            slot.Outfit = source.PlayerClothesInfo;
            if (string.IsNullOrEmpty(slot.RosterOutfit))
            {
                slot.IsMale = slot.Outfit.Value.IsMale;
                UpdatePlaceholderSprite(slot);
            }
            slot.Preview.ObjectPrefab = source.transform;
            slot.Preview.enabled = true;
            return;
        }
        if (!slot.Preview.enabled)
        {
            slot.Preview.enabled = true;
            return;
        }
        if (!slot.Preview.IsReady)
        {
            slot.Target = null;
            slot.Presented = false;
            slot.PoseFrame = -1;
            ShowPlaceholder(slot);
            return;
        }
        var target = slot.Preview.GetTargetInstance().GetComponent<TrainerModel>();
        if (target == null) return;
        if (slot.Target != target)
        {
            slot.Target = target;
            slot.Presented = false;
            slot.PoseFrame = -1;
            ShowPlaceholder(slot);
            target.InitializeClothes(slot.Outfit.Value);
            return;
        }
        if (target.IsLoading || !target.IsInitialized)
        {
            slot.Presented = false;
            slot.PoseFrame = -1;
            ShowPlaceholder(slot);
            return;
        }
        if (slot.PoseFrame < 0)
        {
            var animations = slot.Blue
                ? (target.IsMale ? playerAnimationsBlueMale : playerAnimationsBlueFemale)
                : (target.IsMale ? playerAnimationsOrangeMale : playerAnimationsOrangeFemale);
            if (target.ActiveAnimator != null && animations.TryGetValue(slot.Index, out string animation))
            {
                target.ActiveAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
                target.ActiveAnimator.Play(animation);
                target.ActiveAnimator.Update(0f);
            }
            slot.PoseFrame = Time.frameCount;
            return;
        }
        if (!slot.Presented)
        {
            if (Time.frameCount <= slot.PoseFrame) return;
            slot.Preview.UpdateDisplay(true);
            if (!slot.Preview.HasRendered) return;
            slot.Presented = true;
        }
        slot.Placeholder.gameObject.SetActive(false);
        slot.Preview.imageComponent.color = Color.white;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// How the next minigame prefab is chosen.
public enum MinigameSelectionMode
{
    WeightedRandom,
    Sequential
}

// One entry in the minigame list: a prefab and its relative weight (weight is
// only used in WeightedRandom mode).
[System.Serializable]
public class MinigameEntry
{
    public GameObject prefab;
    [Min(0f)] public float weight = 1f;
}

// Single component that owns the whole minigame system. Add it to the Feed
// object (next to ReelFeedController). Each time a new reel becomes current it
// rolls a chance to pop up a minigame overlay, which it builds in code (a
// full-screen swipe-blocking backdrop + a panel that scales in and hosts the
// chosen minigame prefab).
[RequireComponent(typeof(ReelFeedController))]
public class MinigameManager : MonoBehaviour
{
    [Header("Minigames")]
    [Tooltip("The minigames to choose from. Each is a prefab with a component inheriting MinigameBase.")]
    [SerializeField] private MinigameEntry[] minigames;

    [SerializeField] private MinigameSelectionMode selection = MinigameSelectionMode.WeightedRandom;
    [Tooltip("WeightedRandom only: avoid picking the same minigame twice in a row (when more than one exists).")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    [Header("Trigger")]
    [Tooltip("Chance per reel to spawn a minigame. Raise this for dynamic difficulty later.")]
    [Range(0f, 1f)]
    [SerializeField] private float chancePerReel = 0.15f;

    [Tooltip("No minigames for the first N reels of a session.")]
    [SerializeField] private int gracePeriodReels = 2;

    [Tooltip("Minimum reels between two minigames.")]
    [SerializeField] private int minReelsBetween = 1;

    [Header("Overlay")]
    [Tooltip("Backdrop dim colour behind the minigame (alpha controls how dark).")]
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private float scaleInDuration = 0.35f;
    [SerializeField] private float scaleOutDuration = 0.25f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Size of the minigame panel as a fraction of the screen (1,1 = full screen).")]
    [SerializeField] private Vector2 panelSizeFraction = new Vector2(0.85f, 0.7f);

    [Tooltip("Where the overlay is parented. Leave empty to use the SafeArea (found in parents) so minigames stay within the safe area, falling back to the canvas.")]
    [SerializeField] private RectTransform overlayParent;

    // Dynamic-difficulty hook: added on top of chancePerReel (result clamped to 1).
    public float ExtraChance { get; set; }

    private ReelFeedController feed;
    private Canvas canvas;
    private int reelsSeen;
    private int reelsSinceLast = 9999; // large so the first eligible reel can trigger
    private int sequentialIndex;
    private int lastPickedIndex = -1;
    private bool active;

    void Awake()
    {
        feed = GetComponent<ReelFeedController>();
        canvas = GetComponentInParent<Canvas>();

        // Default the overlay parent to the SafeArea so minigames stay within it.
        if (overlayParent == null)
        {
            SafeAreaFitter safeArea = GetComponentInParent<SafeAreaFitter>();
            if (safeArea != null)
            {
                overlayParent = safeArea.transform as RectTransform;
            }
        }
    }

    void OnEnable()
    {
        if (feed != null)
        {
            feed.TopReelChanged += OnTopReelChanged;
        }
    }

    void OnDisable()
    {
        if (feed != null)
        {
            feed.TopReelChanged -= OnTopReelChanged;
        }
    }

    private void OnTopReelChanged()
    {
        reelsSeen++;
        if (reelsSinceLast < 100000) // guard against overflow
        {
            reelsSinceLast++;
        }

        if (active || minigames == null || minigames.Length == 0)
        {
            return;
        }
        if (reelsSeen <= gracePeriodReels || reelsSinceLast < minReelsBetween)
        {
            return;
        }

        float chance = Mathf.Clamp01(chancePerReel + ExtraChance);
        if (Random.value <= chance)
        {
            TriggerMinigame();
        }
    }

    [ContextMenu("Force Trigger Minigame")]
    private void ForceTrigger()
    {
        if (!active && Application.isPlaying)
        {
            TriggerMinigame();
        }
    }

    private void TriggerMinigame()
    {
        GameObject prefab = SelectPrefab();
        if (prefab == null)
        {
            return;
        }

        active = true;
        reelsSinceLast = 0;

        if (feed != null)
        {
            feed.SetTopReelAudio(false); // duck the reel behind the overlay
        }

        StartCoroutine(RunMinigame(prefab));
    }

    private IEnumerator RunMinigame(GameObject prefab)
    {
        // --- Build the overlay in code (no prefab to leave behind in the scene) ---
        RectTransform overlay = CreateOverlayRoot();
        RectTransform panel = CreatePanel(overlay);

        // Spawn the minigame inside the panel.
        GameObject minigame = Instantiate(prefab, panel);
        FillParent(minigame.transform as RectTransform);

        IMinigame game = minigame.GetComponent<IMinigame>();
        bool done = false;
        if (game == null)
        {
            Debug.LogError("MinigameManager: prefab has no IMinigame component - dismissing.", prefab);
            done = true; // fail safe: never leave the overlay stuck
        }
        else
        {
            game.Finished += _ => done = true;
        }

        // Scale in.
        yield return Scale(panel, 0f, 1f, scaleInDuration);

        // Run.
        if (game != null)
        {
            MinigameContext context = new MinigameContext
            {
                difficulty = Mathf.Clamp01(chancePerReel + ExtraChance)
            };
            game.StartGame(context);
        }

        while (!done)
        {
            yield return null;
        }

        // Scale out and clean up.
        yield return Scale(panel, 1f, 0f, scaleOutDuration);
        Destroy(overlay.gameObject);

        active = false;
        if (feed != null)
        {
            feed.SetTopReelAudio(true);
        }
    }

    // --- Overlay construction ------------------------------------------------

    private RectTransform CreateOverlayRoot()
    {
        GameObject go = new GameObject("MinigameOverlay", typeof(RectTransform), typeof(SwipeBlocker));
        RectTransform rt = go.GetComponent<RectTransform>();
        Transform parent = overlayParent != null
            ? overlayParent
            : (canvas != null ? canvas.transform : transform);
        rt.SetParent(parent, false);
        Stretch(rt);

        // Draw the overlay directly above the feed: on top of the reels, but
        // below anything layered after the feed (e.g. the TopUI overlay), so
        // top-level UI stays in front of the minigame. If the overlay lives
        // somewhere other than the feed's own parent, just put it on top.
        if (parent == transform.parent)
        {
            rt.SetSiblingIndex(transform.GetSiblingIndex() + 1);
        }
        else
        {
            rt.SetAsLastSibling();
        }

        Image backdrop = go.AddComponent<Image>();
        backdrop.color = backdropColor;
        backdrop.raycastTarget = true; // blocks taps/swipes reaching the feed
        return rt;
    }

    private RectTransform CreatePanel(RectTransform overlay)
    {
        GameObject go = new GameObject("Panel", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(overlay, false);

        // Size via anchors as a fraction of the overlay (robust regardless of
        // when the layout pass runs).
        float halfX = Mathf.Clamp01(panelSizeFraction.x) * 0.5f;
        float halfY = Mathf.Clamp01(panelSizeFraction.y) * 0.5f;
        rt.anchorMin = new Vector2(0.5f - halfX, 0.5f - halfY);
        rt.anchorMax = new Vector2(0.5f + halfX, 0.5f + halfY);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    // --- Selection -----------------------------------------------------------

    private GameObject SelectPrefab()
    {
        return selection == MinigameSelectionMode.Sequential ? SelectSequential() : SelectWeighted();
    }

    private GameObject SelectSequential()
    {
        for (int i = 0; i < minigames.Length; i++)
        {
            int idx = (sequentialIndex + i) % minigames.Length;
            if (minigames[idx].prefab != null)
            {
                sequentialIndex = idx + 1;
                lastPickedIndex = idx;
                return minigames[idx].prefab;
            }
        }
        return null;
    }

    private GameObject SelectWeighted()
    {
        List<int> eligible = new List<int>();
        for (int i = 0; i < minigames.Length; i++)
        {
            if (minigames[i].prefab == null)
            {
                continue;
            }
            if (avoidImmediateRepeat && i == lastPickedIndex)
            {
                continue;
            }
            eligible.Add(i);
        }

        // Repeat rule left nothing (e.g. only one game): fall back to all valid.
        if (eligible.Count == 0)
        {
            for (int i = 0; i < minigames.Length; i++)
            {
                if (minigames[i].prefab != null)
                {
                    eligible.Add(i);
                }
            }
        }
        if (eligible.Count == 0)
        {
            return null;
        }

        float total = 0f;
        foreach (int i in eligible)
        {
            total += Mathf.Max(0f, minigames[i].weight);
        }

        if (total <= 0f)
        {
            int uniform = eligible[Random.Range(0, eligible.Count)];
            lastPickedIndex = uniform;
            return minigames[uniform].prefab;
        }

        float roll = Random.value * total;
        foreach (int i in eligible)
        {
            roll -= Mathf.Max(0f, minigames[i].weight);
            if (roll <= 0f)
            {
                lastPickedIndex = i;
                return minigames[i].prefab;
            }
        }

        int last = eligible[eligible.Count - 1];
        lastPickedIndex = last;
        return minigames[last].prefab;
    }

    // --- Helpers -------------------------------------------------------------

    private IEnumerator Scale(RectTransform target, float from, float to, float duration)
    {
        if (target == null)
        {
            yield break;
        }
        if (duration <= 0f)
        {
            target.localScale = new Vector3(to, to, 1f);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = scaleCurve.Evaluate(Mathf.Clamp01(t / duration));
            float s = Mathf.LerpUnclamped(from, to, k);
            target.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        target.localScale = new Vector3(to, to, 1f);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private static void FillParent(RectTransform rt)
    {
        if (rt == null)
        {
            return;
        }
        Stretch(rt);
    }
}

// Sits on the overlay backdrop and swallows drags so they never reach the feed's
// scroll handler while a minigame is up. (A raycast-target Image blocks taps;
// this also absorbs the drag events.)
public class SwipeBlocker : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData) { }
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One hang-up path: an ordered set of waypoints the finger must pass through.
/// Place the waypoints in the prefab to form the shape (a Catmull-Rom UILineRenderer
/// drawn through the same points is the visible spline — pure visual, not read here).
/// </summary>
[System.Serializable]
public class CallPath
{
    [Tooltip("A name for you in the Inspector — not shown to the player.")]
    public string label;

    [Tooltip("Optional container holding this path's line + waypoints. " +
             "Only the chosen path's container is enabled; the rest are hidden.")]
    public GameObject root;

    [Tooltip("Points the finger must cross, in order. The sprite starts at the first.")]
    public List<RectTransform> waypoints = new List<RectTransform>();

    public bool IsValid => waypoints != null && waypoints.Count > 0;
}

/// <summary>
/// "Someone is calling." Tapping pick-up loses (fires an event — no lose logic yet).
/// To hang up, the player drags the hang-up sprite along a spline path, passing each
/// waypoint in order. Finishing the path is the success that scores.
/// </summary>
public class CallOverlay : FeedOverlay,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Caller")]
    [Tooltip("Possible callers. One is picked at random each time.")]
    [SerializeField] private string[] callerNames = { "DAD", "MOM", "UNKNOWN" };

    [Tooltip("Shows who is calling.")]
    [SerializeField] private TMP_Text callerNameText;

    [Tooltip("Format for the caller label. {0} is the chosen name.")]
    [SerializeField] private string callerFormat = "{0} calling…";

    [Header("Pick up (lose)")]
    [SerializeField] private Button pickUpButton;

    [Tooltip("Fired when the player picks up — the losing action. " +
             "Hook a lose screen here later; nothing happens for now.")]
    [SerializeField] private UnityEvent onPickedUp;

    [Header("Hang up (drag to win)")]
    [Tooltip("Possible hang-up paths. One is chosen at random each time.")]
    [SerializeField] private List<CallPath> paths = new List<CallPath>();

    [Tooltip("Sprite that follows the finger along the path.")]
    [SerializeField] private RectTransform hangUpSprite;

    [Tooltip("How close (screen pixels) the finger must get to a waypoint to count as reaching it.")]
    [SerializeField, Min(1f)] private float hitRadius = 80f;

    [Tooltip("How far off the current path segment (screen pixels) resets the trace. 0 = never.")]
    [SerializeField, Min(0f)] private float strayLimit = 140f;

    /// <summary>Player tapped pick-up — the losing action. No consequence wired yet.</summary>
    public event Action<CallOverlay> PickedUp;

    private CallPath current;
    private int nextIndex;
    private bool dragging;

    private Canvas canvas;
    private Camera UICamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
        ? canvas.worldCamera
        : null;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();

        if (pickUpButton != null)
            pickUpButton.onClick.AddListener(HandlePickUp);
    }

    private void OnDestroy()
    {
        if (pickUpButton != null)
            pickUpButton.onClick.RemoveListener(HandlePickUp);
    }

    protected override void OnBegin()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        // Caller name.
        if (callerNameText != null)
        {
            string who = (callerNames != null && callerNames.Length > 0)
                ? callerNames[UnityEngine.Random.Range(0, callerNames.Length)]
                : "UNKNOWN";
            callerNameText.text = string.Format(callerFormat, who);
        }

        current = PickPath();
        nextIndex = 0;
        dragging = false;

        if (current == null)
            Debug.LogWarning("[CallOverlay] No valid paths assigned — the call can't be hung up.");
        else
            PlaceSpriteAtWaypoint(0);
    }

    private CallPath PickPath()
    {
        List<CallPath> valid = new List<CallPath>();
        for (int i = 0; i < paths.Count; i++)
            if (paths[i] != null && paths[i].IsValid)
                valid.Add(paths[i]);

        CallPath chosen = valid.Count == 0 ? null : valid[UnityEngine.Random.Range(0, valid.Count)];

        // Show only the chosen path's visuals.
        for (int i = 0; i < paths.Count; i++)
            if (paths[i] != null && paths[i].root != null)
                paths[i].root.SetActive(paths[i] == chosen);

        return chosen;
    }

    private void HandlePickUp()
    {
        if (IsFinished)
            return;

        // No lose mechanic yet — just announce it and fire the event.
        Debug.Log("[CallOverlay] Player picked up — LOSE (no lose logic wired yet).");
        onPickedUp?.Invoke();
        PickedUp?.Invoke(this);
    }

    // ---- Drag tracking (EventSystem) --------------------------------------

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsFinished || current == null)
            return;

        dragging = true;
        nextIndex = 0;
        MoveSpriteToScreen(eventData.position);
        TryAdvance(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || IsFinished || current == null)
            return;

        MoveSpriteToScreen(eventData.position);

        if (strayLimit > 0f && DistanceToCurrentSegment(eventData.position) > strayLimit)
        {
            ResetTrace();
            return;
        }

        TryAdvance(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!dragging || IsFinished)
            return;

        // Let go before finishing the shape — start over.
        ResetTrace();
    }

    private void TryAdvance(Vector2 screenPos)
    {
        if (nextIndex >= current.waypoints.Count)
            return;

        if (Vector2.Distance(screenPos, WaypointScreenPos(nextIndex)) <= hitRadius)
        {
            nextIndex++;
            ReportProgress();

            if (nextIndex >= current.waypoints.Count)
            {
                dragging = false;
                Complete(); // hung up successfully
            }
        }
    }

    private void ResetTrace()
    {
        dragging = false;
        nextIndex = 0;
        PlaceSpriteAtWaypoint(0);
        ReportProgress();
    }

    // ---- Positioning helpers ----------------------------------------------

    private Vector2 WaypointScreenPos(int index)
        => RectTransformUtility.WorldToScreenPoint(UICamera, current.waypoints[index].position);

    private void PlaceSpriteAtWaypoint(int index)
    {
        if (hangUpSprite == null || current == null || index >= current.waypoints.Count)
            return;

        MoveSpriteToScreen(WaypointScreenPos(index));
    }

    private void MoveSpriteToScreen(Vector2 screenPos)
    {
        if (hangUpSprite == null)
            return;

        RectTransform parent = hangUpSprite.parent as RectTransform;
        if (parent == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, UICamera, out Vector2 local))
            hangUpSprite.anchoredPosition = local;
    }

    // Shortest screen-space distance from the finger to the segment it should be on
    // (previous waypoint -> next waypoint). Used to detect straying off the path.
    private float DistanceToCurrentSegment(Vector2 screenPos)
    {
        if (nextIndex >= current.waypoints.Count)
            return 0f;

        Vector2 b = WaypointScreenPos(nextIndex);
        Vector2 a = nextIndex > 0 ? WaypointScreenPos(nextIndex - 1) : b;

        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f)
            return Vector2.Distance(screenPos, b);

        float t = Mathf.Clamp01(Vector2.Dot(screenPos - a, ab) / lenSq);
        Vector2 closest = a + ab * t;
        return Vector2.Distance(screenPos, closest);
    }

    protected override string GetProgressLabel()
        => current == null ? "0 / 0" : $"{nextIndex} / {current.waypoints.Count}";
}

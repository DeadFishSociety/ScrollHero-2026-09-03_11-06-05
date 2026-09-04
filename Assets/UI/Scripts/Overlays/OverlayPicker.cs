using System.Collections.Generic;
using UnityEngine;

public enum OverlaySelectionMode
{
    /// <summary>Pick at random, respecting each entry's Weight.</summary>
    RandomWeighted,
    /// <summary>Walk down the enabled entries in order, then start over.</summary>
    Sequential
}

/// <summary>
/// One entry in an overlay list. Adding a new action type is just: write the
/// FeedOverlay subclass, build a prefab for it, drop it in here.
/// </summary>
[System.Serializable]
public class OverlayOption
{
    [Tooltip("Just a name for you in the Inspector — not used by the game.")]
    public string label;

    [Tooltip("Prefab with a FeedOverlay component (LikeOverlay, AdCloseOverlay, ...).")]
    public FeedOverlay prefab;

    [Tooltip("Untick to keep this action in the list but out of rotation.")]
    public bool enabled = true;

    [Tooltip("Relative chance of being picked. 2 is twice as likely as 1. Random mode only.")]
    [Min(0f)] public float weight = 1f;

    public bool IsUsable => enabled && prefab != null;
}

/// <summary>
/// A configurable pool of overlay prefabs plus the rule for choosing between them.
/// Lives inline in the Inspector (on FeedManager, or on an individual FeedItem),
/// so there is no extra object to wire up.
/// </summary>
[System.Serializable]
public class OverlayPicker
{
    [Tooltip("The action overlays that can appear. Untick an entry to disable it without deleting it.")]
    [SerializeField] private List<OverlayOption> options = new List<OverlayOption>();

    [SerializeField] private OverlaySelectionMode mode = OverlaySelectionMode.RandomWeighted;

    [Tooltip("Random mode: never pick the same overlay twice in a row (unless it is the only one enabled).")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    private readonly List<OverlayOption> usable = new List<OverlayOption>();
    private FeedOverlay lastPicked;
    private int nextIndex;

    /// <summary>True when at least one entry is enabled and has a prefab.</summary>
    public bool HasAny
    {
        get
        {
            for (int i = 0; i < options.Count; i++)
                if (options[i] != null && options[i].IsUsable)
                    return true;
            return false;
        }
    }

    /// <summary>Returns the next overlay prefab to spawn, or null if the list is empty/all disabled.</summary>
    public FeedOverlay Pick()
    {
        usable.Clear();
        for (int i = 0; i < options.Count; i++)
            if (options[i] != null && options[i].IsUsable)
                usable.Add(options[i]);

        if (usable.Count == 0)
            return null;

        FeedOverlay picked = mode == OverlaySelectionMode.Sequential ? PickSequential() : PickRandom();
        lastPicked = picked;
        return picked;
    }

    private FeedOverlay PickSequential()
    {
        if (nextIndex >= usable.Count)
            nextIndex = 0;

        FeedOverlay picked = usable[nextIndex].prefab;
        nextIndex = (nextIndex + 1) % usable.Count;
        return picked;
    }

    private FeedOverlay PickRandom()
    {
        // Drop the previous pick from the running so the same action doesn't repeat back to back.
        if (avoidImmediateRepeat && lastPicked != null && usable.Count > 1)
            usable.RemoveAll(o => o.prefab == lastPicked);

        if (usable.Count == 0)
            return lastPicked;

        float total = 0f;
        for (int i = 0; i < usable.Count; i++)
            total += Mathf.Max(0f, usable[i].weight);

        // All weights are zero (or negative) — treat them as equally likely.
        if (total <= 0f)
            return usable[Random.Range(0, usable.Count)].prefab;

        float roll = Random.Range(0f, total);
        for (int i = 0; i < usable.Count; i++)
        {
            roll -= Mathf.Max(0f, usable[i].weight);
            if (roll <= 0f)
                return usable[i].prefab;
        }

        return usable[usable.Count - 1].prefab;
    }
}

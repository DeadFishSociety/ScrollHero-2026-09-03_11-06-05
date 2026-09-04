using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Base class for every mini-interaction that can appear on top of an action panel.
/// Handles the shared plumbing (timer, progress label, finish events) so a new
/// overlay type only has to implement its own input handling.
///
/// To add a new action type: make a class deriving from this, call ReportProgress()
/// while the player is making progress and Complete() / Fail() when it ends.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public abstract class FeedOverlay : MonoBehaviour
{
    [Header("Overlay — shared settings")]
    [Tooltip("Shown in logs and in the FeedManager overlay list. Falls back to the object name.")]
    [SerializeField] private string displayName = "";

    [Tooltip("Seconds before this overlay fails on its own. 0 = no time limit.")]
    [SerializeField, Min(0f)] private float timeLimit = 0f;

    [Tooltip("Optional label that shows progress, e.g. \"3 / 8\".")]
    [SerializeField] private TMP_Text progressText;

    [Tooltip("While this overlay is active, swipes are swallowed instead of scrolling the feed.")]
    [SerializeField] private bool blocksSwipe = true;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public bool BlocksSwipe => blocksSwipe;
    public bool IsFinished { get; private set; }
    public float TimeLimit => timeLimit;
    public float TimeRemaining => timeLimit <= 0f ? Mathf.Infinity : Mathf.Max(0f, timeLimit - elapsed);

    /// <summary>Player finished the interaction successfully — this is what scores.</summary>
    public event Action<FeedOverlay> Completed;
    /// <summary>Player ran out of time, or did something that counts as giving up.</summary>
    public event Action<FeedOverlay> Failed;
    /// <summary>Partial progress (one tap of many). Used for small feedback, not scoring.</summary>
    public event Action<FeedOverlay> Progressed;

    private float elapsed;
    private bool running;

    /// <summary>Called by FeedManager right after the overlay is spawned.</summary>
    public void Begin()
    {
        IsFinished = false;
        elapsed = 0f;
        running = true;
        OnBegin();
        RefreshProgressText();
    }

    private void Update()
    {
        if (!running)
            return;

        if (timeLimit > 0f)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= timeLimit)
            {
                Fail();
                return;
            }
        }

        OnTick(Time.deltaTime);
    }

    /// <summary>Reset your own state here — Begin() may be called again on a retry.</summary>
    protected virtual void OnBegin() { }

    /// <summary>Per-frame hook while the overlay is running.</summary>
    protected virtual void OnTick(float deltaTime) { }

    /// <summary>Text for the progress label, e.g. "3 / 8".</summary>
    protected abstract string GetProgressLabel();

    protected void ReportProgress()
    {
        if (IsFinished)
            return;

        RefreshProgressText();
        Progressed?.Invoke(this);
    }

    protected void Complete()
    {
        if (IsFinished)
            return;

        IsFinished = true;
        running = false;
        RefreshProgressText();
        Completed?.Invoke(this);
    }

    protected void Fail()
    {
        if (IsFinished)
            return;

        IsFinished = true;
        running = false;
        RefreshProgressText();
        Failed?.Invoke(this);
    }

    private void RefreshProgressText()
    {
        if (progressText != null)
            progressText.text = GetProgressLabel();
    }
}

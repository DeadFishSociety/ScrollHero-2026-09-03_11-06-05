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

    [Tooltip("The countdown for this minigame. When it empties the overlay fails on " +
             "its own. Set duration to 0 for no time limit.")]
    [SerializeField] private DrainTimer timer = new DrainTimer();

    [Tooltip("Optional dopamine gauge to drive. Usually left empty — FeedManager injects " +
             "the shared HUD gauge at spawn via SetGauge().")]
    [SerializeField] private DopamineGauge gauge;

    [Tooltip("Optional label that shows progress, e.g. \"3 / 8\".")]
    [SerializeField] private TMP_Text progressText;

    [Tooltip("While this overlay is active, swipes are swallowed instead of scrolling the feed.")]
    [SerializeField] private bool blocksSwipe = true;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public bool BlocksSwipe => blocksSwipe;
    public bool IsFinished { get; private set; }
    /// <summary>1 = full, 0 = out of time. Handy for custom visuals.</summary>
    public float TimeFraction => timer.Fraction;

    /// <summary>Point this overlay at the shared HUD gauge. Called by FeedManager on spawn.</summary>
    public void SetGauge(DopamineGauge sharedGauge) => gauge = sharedGauge;

    /// <summary>Player finished the interaction successfully — this is what scores.</summary>
    public event Action<FeedOverlay> Completed;
    /// <summary>Player ran out of time, or did something that counts as giving up.</summary>
    public event Action<FeedOverlay> Failed;
    /// <summary>Partial progress (one tap of many). Used for small feedback, not scoring.</summary>
    public event Action<FeedOverlay> Progressed;

    private bool active; // timer is counting (between StartTimer and finish)

    /// <summary>
    /// Called by FeedManager right after the overlay is spawned. Sets the minigame up
    /// and primes the gauge to full, but does NOT start the countdown yet — that waits
    /// for <see cref="StartTimer"/> so the clock only begins once the panel has slid in.
    /// </summary>
    public void Begin()
    {
        IsFinished = false;
        active = false;
        timer.Prime();
        PushToGauge();
        OnBegin();
        RefreshProgressText();
    }

    /// <summary>Start the countdown. FeedManager calls this when the panel finishes sliding in.</summary>
    public void StartTimer()
    {
        if (IsFinished)
            return;

        active = true;
        timer.Run();
        PushToGauge();
    }

    private void Update()
    {
        if (!active)
            return;

        bool justEmptied = timer.Tick(Time.deltaTime);
        PushToGauge();

        if (justEmptied)
        {
            Fail(); // ran out of time
            return;
        }

        OnTick(Time.deltaTime);
    }

    private void PushToGauge()
    {
        if (gauge != null)
            gauge.SetFraction(timer.Fraction);
    }

    /// <summary>Reset your own state here — Begin() may be called again on a retry.</summary>
    protected virtual void OnBegin() { }

    /// <summary>Per-frame hook while the overlay is running.</summary>
    protected virtual void OnTick(float deltaTime) { }

    /// <summary>
    /// Swipes from the feed's SwipeInput are forwarded here while this overlay is
    /// active. Tap-based overlays ignore it; swipe-based ones (e.g. CallOverlay)
    /// override this to read the gesture.
    /// </summary>
    public virtual void OnSwipeInput(SwipeDirection direction) { }

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
        active = false;
        timer.Stop();
        RefreshProgressText();
        Completed?.Invoke(this);
    }

    protected void Fail()
    {
        if (IsFinished)
            return;

        IsFinished = true;
        active = false;
        timer.Stop();
        RefreshProgressText();
        OnFail();
        Failed?.Invoke(this);
    }

    /// <summary>Called when the overlay fails (e.g. its timer ran out). Override for fail feedback.</summary>
    protected virtual void OnFail() { }

    private void RefreshProgressText()
    {
        if (progressText != null)
            progressText.text = GetProgressLabel();
    }
}

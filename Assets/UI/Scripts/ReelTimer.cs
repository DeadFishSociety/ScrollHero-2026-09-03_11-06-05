using System;
using UnityEngine;

/// <summary>
/// The countdown that lives on a scroll reel. It drains every frame, drives an
/// optional DopamineGauge, and fires <see cref="Expired"/> the instant it empties.
///
/// FeedManager calls <see cref="Prime"/> when the reel spawns and <see cref="Begin"/>
/// once it has slid into place, and turns <see cref="Expired"/> into a lost life. If
/// the player swipes the reel away in time, FeedManager calls <see cref="Stop"/> so no
/// life is lost.
///
/// Both knobs (duration, drain rate) live here on the reel itself, so each reel
/// prefab can be tuned independently.
/// </summary>
public class ReelTimer : MonoBehaviour
{
    [Header("Timer")]
    [SerializeField] private DrainTimer timer = new DrainTimer();

    [Header("Display")]
    [Tooltip("Optional dopamine gauge to drive. Usually left empty — FeedManager injects " +
             "the shared HUD gauge at spawn via SetGauge().")]
    [SerializeField] private DopamineGauge gauge;

    /// <summary>Fired on the frame the timer empties.</summary>
    public event Action Expired;

    /// <summary>Point this timer at the shared HUD gauge. Called by FeedManager on spawn.</summary>
    public void SetGauge(DopamineGauge sharedGauge) => gauge = sharedGauge;

    /// <summary>Scale how fast the dopamine bar drains (1 = authored speed). Adaptive difficulty.</summary>
    public void SetSpeedMultiplier(float multiplier) => timer.SetSpeedMultiplier(multiplier);

    /// <summary>Refill and show a full gauge, but don't count yet (while the reel slides in).</summary>
    public void Prime()
    {
        timer.Prime();
        PushToGauge();
    }

    /// <summary>Start draining. FeedManager calls this once the reel has slid into place.</summary>
    public void Begin()
    {
        timer.Run();
        PushToGauge();
    }

    /// <summary>Stop draining (e.g. the reel was swiped away in time).</summary>
    public void Stop() => timer.Stop();

    private void Update()
    {
        if (!timer.Running)
            return;

        bool justEmptied = timer.Tick(Time.deltaTime);
        PushToGauge();

        if (justEmptied)
            Expired?.Invoke();
    }

    private void PushToGauge()
    {
        if (gauge != null)
            gauge.SetFraction(timer.Fraction);
    }
}

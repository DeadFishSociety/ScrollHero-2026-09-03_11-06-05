using System;
using UnityEngine;

/// <summary>
/// The countdown that lives on a scroll reel. It drains every frame, drives an
/// optional DopamineGauge, and fires <see cref="Expired"/> the instant it empties.
///
/// FeedManager calls <see cref="Restart"/> when the reel becomes active, and turns
/// <see cref="Expired"/> into a lost life. If the player swipes the reel away in
/// time, FeedManager calls <see cref="Stop"/> so no life is lost.
///
/// Both knobs (duration, drain rate) live here on the reel itself, so each reel
/// prefab can be tuned independently.
/// </summary>
public class ReelTimer : MonoBehaviour
{
    [Header("Timer")]
    [SerializeField] private DrainTimer timer = new DrainTimer();

    [Header("Display")]
    [Tooltip("Optional dopamine gauge to drive. Leave empty for no visual.")]
    [SerializeField] private DopamineGauge gauge;

    /// <summary>Fired on the frame the timer empties.</summary>
    public event Action Expired;

    /// <summary>Refill and start draining. Call when this reel becomes the active one.</summary>
    public void Restart()
    {
        timer.Restart();
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

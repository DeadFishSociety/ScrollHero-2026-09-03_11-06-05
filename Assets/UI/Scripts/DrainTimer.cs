using UnityEngine;

/// <summary>
/// A countdown clock with two authorable knobs: how long it starts with
/// (<see cref="duration"/>) and how fast it drains (<see cref="drainRate"/>).
/// The actual time on screen is duration / drainRate, so you can tune "lots of
/// time but drains fast" independently from "little time, drains slow".
///
/// Pure logic — no visuals. A ReelTimer or FeedOverlay owns one of these, ticks
/// it every frame, and feeds <see cref="Fraction"/> to a DopamineGauge.
/// </summary>
[System.Serializable]
public class DrainTimer
{
    [Tooltip("Starting time on the clock — the 'how long do they have' number.")]
    [SerializeField, Min(0f)] private float duration = 5f;

    [Tooltip("How fast the clock drains. 1 = real seconds, 2 = twice as fast. " +
             "Time actually on screen = duration / drainRate.")]
    [SerializeField, Min(0.0001f)] private float drainRate = 1f;

    private float remaining;

    /// <summary>True between Run() and the moment it empties (or Stop()).</summary>
    public bool Running { get; private set; }

    /// <summary>1 = full, 0 = empty. A duration of 0 means "no timer" and stays full.</summary>
    public float Fraction => duration <= 0f ? 1f : Mathf.Clamp01(remaining / duration);

    public bool IsEmpty => remaining <= 0f;

    /// <summary>Refill to full but don't count yet — the gauge shows full while the
    /// panel slides in. Call <see cref="Run"/> to actually start draining.</summary>
    public void Prime()
    {
        remaining = duration;
        Running = false;
    }

    /// <summary>Refill to full and start draining now.</summary>
    public void Run()
    {
        remaining = duration;
        Running = duration > 0f;
    }

    public void Stop() => Running = false;

    /// <summary>Advance the clock. Returns true only on the frame it hits empty.</summary>
    public bool Tick(float deltaTime)
    {
        if (!Running)
            return false;

        remaining -= drainRate * deltaTime;
        if (remaining <= 0f)
        {
            remaining = 0f;
            Running = false;
            return true;
        }
        return false;
    }
}

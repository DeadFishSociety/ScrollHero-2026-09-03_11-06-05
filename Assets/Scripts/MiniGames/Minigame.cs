using System;
using UnityEngine;

// The result of a played minigame. Both outcomes dismiss the overlay; the value
// is passed back so scoring/penalties can be added later.
public enum MinigameOutcome
{
    Won,
    Lost
}

// Data handed to a minigame when it starts. difficulty (0..1) is how hard this
// run should play - set by DynamicDifficulty from the player's score, or the
// minigame's own authored Difficulty when no dynamic difficulty is present.
public struct MinigameContext
{
    public float difficulty;
}

// The contract every minigame implements. The manager only talks to this, so it
// never needs to know about any specific minigame.
public interface IMinigame
{
    // Authored difficulty of this minigame, 0 (easy) to 1 (hard). Drives things
    // like how fast the dopamine bar drains while it is on screen.
    float Difficulty { get; }

    // Called once the overlay has finished animating in.
    void StartGame(MinigameContext context);

    // Raised by the minigame when it is won or lost.
    event Action<MinigameOutcome> Finished;
}

// Convenience base class: implement StartGame and call Win()/Lose() when done.
// Guards against firing Finished more than once.
public abstract class MinigameBase : MonoBehaviour, IMinigame
{
    [Header("Minigame")]
    [Tooltip("How hard this minigame is, 0 (easy) to 1 (hard). Higher drains the dopamine bar faster while it is on screen.")]
    [Range(0f, 1f)]
    [SerializeField] private float difficulty;

    public float Difficulty => difficulty;

    public event Action<MinigameOutcome> Finished;

    private bool finished;

    public abstract void StartGame(MinigameContext context);

    protected void Win()
    {
        Finish(MinigameOutcome.Won);
    }

    protected void Lose()
    {
        Finish(MinigameOutcome.Lost);
    }

    protected void Finish(MinigameOutcome outcome)
    {
        if (finished)
        {
            return;
        }
        finished = true;
        Finished?.Invoke(outcome);
    }
}

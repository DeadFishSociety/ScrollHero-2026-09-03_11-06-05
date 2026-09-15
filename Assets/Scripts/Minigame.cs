using System;
using UnityEngine;

// The result of a played minigame. Both outcomes dismiss the overlay; the value
// is passed back so scoring/penalties can be added later.
public enum MinigameOutcome
{
    Won,
    Lost
}

// Data handed to a minigame when it starts. The difficulty field is the hook for
// dynamic difficulty later (0 for now).
public struct MinigameContext
{
    public float difficulty;
}

// The contract every minigame implements. The manager only talks to this, so it
// never needs to know about any specific minigame.
public interface IMinigame
{
    // Called once the overlay has finished animating in.
    void StartGame(MinigameContext context);

    // Raised by the minigame when it is won or lost.
    event Action<MinigameOutcome> Finished;
}

// Convenience base class: implement StartGame and call Win()/Lose() when done.
// Guards against firing Finished more than once.
public abstract class MinigameBase : MonoBehaviour, IMinigame
{
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

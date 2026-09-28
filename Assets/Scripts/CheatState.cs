using UnityEngine;

// Flags set by the cheat menu that need to survive a scene load into GameScene.
// Kept deliberately tiny and static so no object needs to persist between scenes.
//
// The GameScene consumers read these once on load and reset them, so a cheat flag
// can never leak into a later normal run.
public static class CheatState
{
    // Scroll mode: while true, DopamineManager never drains the bar. Consumed
    // (reset to false) by DopamineManager on scene load.
    public static bool NoDopamineDrain;

    // A minigame to open immediately on entering GameScene. Consumed (set back to
    // null) by MinigameManager once it launches it.
    public static GameObject RequestedMinigame;

    // Scene to return to after a cheat-launched minigame finishes (the cheat menu).
    // Consumed by MinigameManager. Null/empty means "stay in the game".
    public static string ReturnToScene;
}

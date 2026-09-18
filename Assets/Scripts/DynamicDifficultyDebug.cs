using UnityEngine;
using UnityEngine.InputSystem;

// On-screen debug overlay for the dynamic difficulty. Shows the live score,
// difficulty progress, and every value the DynamicDifficulty is pushing into the
// game (spawn chance, drain/gain multipliers, per-minigame difficulty).
//
// Add it to any GameObject (it auto-finds the DynamicDifficulty). It draws with
// IMGUI so it needs no canvas. Collapse it with the button, or hide it entirely
// with the backquote (`) key. Purely a debug tool - disable or remove for release.
public class DynamicDifficultyDebug : MonoBehaviour
{
    [Tooltip("The difficulty controller to inspect. Auto-found if left empty.")]
    [SerializeField] private DynamicDifficulty difficulty;

    [Tooltip("Overall size of the overlay. Bump this up on high-DPI phones.")]
    [SerializeField] private float uiScale = 2f;

    [Tooltip("Show the overlay on start.")]
    [SerializeField] private bool visible = true;

    private Rect windowRect = new Rect(12f, 12f, 260f, 0f);
    private Texture2D whiteTex;

    private void Awake()
    {
        if (difficulty == null)
        {
            difficulty = FindFirstObjectByType<DynamicDifficulty>();
        }
    }

    private void Update()
    {
        // Toggle the whole overlay with the backquote/tilde key.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.backquoteKey.wasPressedThisFrame)
        {
            visible = !visible;
        }
    }

    private void OnGUI()
    {
        if (!visible || difficulty == null)
        {
            return;
        }

        Matrix4x4 previous = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(uiScale, uiScale), Vector2.zero);

        windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Difficulty Debug");

        GUI.matrix = previous;
    }

    private void DrawWindow(int id)
    {
        int score = ScoreManager.Instance != null ? ScoreManager.Instance.Score : 0;

        GUILayout.Label($"Score: {score} / {difficulty.ScoreForMaxDifficulty}");
        DrawBar("Progress", difficulty.Progress);

        GUILayout.Space(4f);
        GUILayout.Label($"Spawn +chance: {difficulty.CurrentExtraSpawnChance * 100f:0.#}%");
        GUILayout.Label($"Drain x{difficulty.CurrentDrainMultiplier:0.00}");
        GUILayout.Label($"Gain  x{difficulty.CurrentGainMultiplier:0.00}");

        GUILayout.Space(6f);
        GUILayout.Label("Minigames:");

        DynamicDifficulty.MinigameDifficulty[] entries = difficulty.Minigames;
        bool listedAny = false;
        if (entries != null)
        {
            foreach (DynamicDifficulty.MinigameDifficulty entry in entries)
            {
                if (entry == null || entry.prefab == null)
                {
                    continue;
                }
                listedAny = true;
                DrawBar(entry.prefab.name, difficulty.CurrentDifficultyOf(entry));
            }
        }

        if (!listedAny)
        {
            GUILayout.Label(difficulty.ScaleUnlistedMinigames
                ? "  (none listed - all ramp with Progress)"
                : "  (none listed - all use authored Difficulty)");
        }
        else if (difficulty.ScaleUnlistedMinigames)
        {
            GUILayout.Label("  (unlisted ramp with Progress)");
        }

        GUILayout.Space(4f);
        if (GUILayout.Button("Hide (`)"))
        {
            visible = false;
        }

        GUI.DragWindow();
    }

    // Draws a labelled 0..1 bar with the value shown as a percentage.
    private void DrawBar(string label, float value01)
    {
        value01 = Mathf.Clamp01(value01);
        GUILayout.Label($"{label}: {value01 * 100f:0}%");

        Rect r = GUILayoutUtility.GetRect(100f, 12f, GUILayout.ExpandWidth(true));
        EnsureResources();

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.4f);
        GUI.DrawTexture(r, whiteTex);

        Rect fill = new Rect(r.x, r.y, r.width * value01, r.height);
        GUI.color = Color.Lerp(new Color(0.2f, 0.8f, 0.3f), new Color(0.9f, 0.2f, 0.2f), value01);
        GUI.DrawTexture(fill, whiteTex);

        GUI.color = prev;
    }

    private void EnsureResources()
    {
        if (whiteTex == null)
        {
            whiteTex = Texture2D.whiteTexture;
        }
    }
}

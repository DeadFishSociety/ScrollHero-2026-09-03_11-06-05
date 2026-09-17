using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Time-limit minigame: a fake "you've been scrolling too long" notification with
// three near-identical buttons. One quits the game (instant lose); the other two
// simply dismiss the overlay (win). To stop the player from learning which is
// which, every time the minigame appears the buttons are re-tinted to random
// shades of grey and their on-screen order is shuffled.
public class TimeLimitMinigame : MinigameBase
{
    [Header("Buttons")]
    [Tooltip("The button that quits the game (instant lose).")]
    [SerializeField] private Button quitButton;

    [Tooltip("The buttons that just close the minigame (win). Usually two.")]
    [SerializeField] private Button[] dismissButtons;

    [Header("Random grey shades")]
    [Tooltip("Darkest grey a button can be tinted (0 = black).")]
    [Range(0f, 1f)]
    [SerializeField] private float minGrey = 0.35f;

    [Tooltip("Lightest grey a button can be tinted (1 = white).")]
    [Range(0f, 1f)]
    [SerializeField] private float maxGrey = 0.8f;

    // Cached list of every button, quit + dismiss.
    private readonly List<Button> allButtons = new List<Button>();

    public override void StartGame(MinigameContext context)
    {
        CollectButtons();
        WireListeners();
        RandomiseGreys();
        ShuffleOrder();
    }

    // --- Setup ---------------------------------------------------------------

    private void CollectButtons()
    {
        allButtons.Clear();
        if (quitButton != null)
        {
            allButtons.Add(quitButton);
        }
        if (dismissButtons != null)
        {
            foreach (Button b in dismissButtons)
            {
                if (b != null)
                {
                    allButtons.Add(b);
                }
            }
        }
    }

    private void WireListeners()
    {
        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitClicked);
            quitButton.onClick.AddListener(OnQuitClicked);
        }
        if (dismissButtons != null)
        {
            foreach (Button b in dismissButtons)
            {
                if (b == null)
                {
                    continue;
                }
                b.onClick.RemoveListener(OnDismissClicked);
                b.onClick.AddListener(OnDismissClicked);
            }
        }
    }

    // Tints every button to its own random shade of grey so they all look
    // interchangeable and the quit button doesn't stand out.
    private void RandomiseGreys()
    {
        float lo = Mathf.Min(minGrey, maxGrey);
        float hi = Mathf.Max(minGrey, maxGrey);

        foreach (Button b in allButtons)
        {
            Image img = (b.targetGraphic as Image) ?? b.GetComponent<Image>();
            if (img == null)
            {
                continue;
            }
            float g = Random.Range(lo, hi);
            img.color = new Color(g, g, g, img.color.a);
        }
    }

    // Keeps the same on-screen slots but reassigns which button sits in each, so
    // the order changes every time the minigame appears. The buttons are placed
    // by anchoredPosition, so shuffling those positions is what reorders them.
    private void ShuffleOrder()
    {
        if (allButtons.Count < 2)
        {
            return;
        }

        // Snapshot the fixed slot positions in their current order.
        List<Vector2> slots = new List<Vector2>(allButtons.Count);
        foreach (Button b in allButtons)
        {
            slots.Add(((RectTransform)b.transform).anchoredPosition);
        }

        // Fisher-Yates shuffle of the slots.
        for (int i = slots.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }

        // Assign each button to a (now shuffled) slot.
        for (int i = 0; i < allButtons.Count; i++)
        {
            ((RectTransform)allButtons[i].transform).anchoredPosition = slots[i];
        }
    }

    // --- Outcomes ------------------------------------------------------------

    private void OnDismissClicked()
    {
        Win();
    }

    private void OnQuitClicked()
    {
        // Instant lose: this really does close the whole game.
        Lose();
        QuitGame();
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

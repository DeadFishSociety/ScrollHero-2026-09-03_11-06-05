using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// "Time limit reached." A modal screen-time popup raised on top of the current reel.
/// A scrim authored in the prefab dims the feed behind it, and scrolling is blocked
/// (blocksSwipe) while the player makes a choice against a short dopamine countdown:
///
///  * "Quit"                              — gives up: an instant game over.
///  * "Extend for &lt;math&gt; seconds" (x2)   — buys more time: the feed scrolls on and the
///                                          popup returns after that many seconds.
///
/// The three buttons are shuffled between their slots every time the popup opens, so
/// "quit" is never in a fixed spot. Each extend button shows a small arithmetic puzzle
/// — either "higher - lower" or "lower + lower" — that works out to somewhere between
/// <see cref="minExtendSeconds"/> and <see cref="maxExtendSeconds"/>.
///
/// Pressing a button acts on release (Unity's Button.onClick), so a press can be
/// cancelled by dragging off; the "darker while held" look is the button's own
/// pressed-colour tint, set up in the prefab — no code needed.
/// </summary>
public class ScreenTimeOverlay : FeedOverlay
{
    /// <summary>A choice button, paired with the label whose text it shows.</summary>
    [System.Serializable]
    private class ChoiceButton
    {
        public Button button;

        [Tooltip("Text on this button. Left empty, the first TMP_Text under the button is used.")]
        public TMP_Text label;

        public TMP_Text ResolveLabel()
            => label != null ? label
             : (button != null ? button.GetComponentInChildren<TMP_Text>() : null);
    }

    private enum Choice { Quit, Extend }

    private struct Role
    {
        public Choice choice;
        public int seconds;    // Extend only: the resolved value of the expression.
        public string display; // Text shown on the button (quit text, or the puzzle).
    }

    [Header("Message")]
    [Tooltip("Headline + body shown on the popup. \\n splits the two lines.")]
    [SerializeField, TextArea]
    private string message = "Time limit reached.\nYou have reached your time limit for this app.";

    [Tooltip("The label the message is written into.")]
    [SerializeField] private TMP_Text messageText;

    [Header("Choices")]
    [Tooltip("The three choice buttons, in their fixed on-screen positions. Their ROLES " +
             "(quit / the two extends) are shuffled between these slots every time the " +
             "popup opens, so quit is never in a predictable place.")]
    [SerializeField] private ChoiceButton[] choiceButtons = new ChoiceButton[3];

    [Tooltip("Text for the give-up button. Choosing it is an instant game over.")]
    [SerializeField] private string quitText = "Quit";

    [Tooltip("Label for an extend button. {0} is the arithmetic puzzle, e.g. \"40 - 15\".")]
    [SerializeField] private string extendFormat = "Extend for {0} seconds";

    [Header("Extend amount")]
    [Tooltip("Smallest number of seconds an extend button can work out to.")]
    [SerializeField, Min(1)] private int minExtendSeconds = 10;

    [Tooltip("Largest number of seconds an extend button can work out to.")]
    [SerializeField, Min(1)] private int maxExtendSeconds = 30;

    private Role[] roles;
    private UnityAction[] handlers;

    /// <summary>Seconds the player last chose to extend for. FeedManager reads this to
    /// decide how long until the popup returns.</summary>
    public int ChosenExtendSeconds { get; private set; }

    // The popup is a choice, not a challenge: it never scores, and finishing it always
    // scrolls the feed on to the next reel rather than resuming the one below.
    public override bool ScoresOnComplete => false;
    public override bool AdvancesFeedOnComplete => true;

    private void Awake()
    {
        int count = choiceButtons != null ? choiceButtons.Length : 0;
        handlers = new UnityAction[count];

        for (int i = 0; i < count; i++)
        {
            if (choiceButtons[i] == null || choiceButtons[i].button == null)
                continue;

            int index = i; // capture per-button so the listener knows which slot it is
            handlers[i] = () => OnChoicePressed(index);
            choiceButtons[i].button.onClick.AddListener(handlers[i]);
        }
    }

    private void OnDestroy()
    {
        if (handlers == null || choiceButtons == null)
            return;

        for (int i = 0; i < choiceButtons.Length && i < handlers.Length; i++)
            if (choiceButtons[i] != null && choiceButtons[i].button != null && handlers[i] != null)
                choiceButtons[i].button.onClick.RemoveListener(handlers[i]);
    }

    protected override void OnBegin()
    {
        ChosenExtendSeconds = Mathf.Max(1, Mathf.Min(minExtendSeconds, maxExtendSeconds));

        if (messageText != null)
            messageText.text = message;

        AssignRoles();
    }

    // Build one quit role + (n-1) extend roles, shuffle them across the buttons, and
    // write each button's label.
    private void AssignRoles()
    {
        int count = choiceButtons != null ? choiceButtons.Length : 0;
        roles = new Role[count];
        if (count == 0)
            return;

        var pool = new List<Role>(count)
        {
            new Role { choice = Choice.Quit, display = quitText }
        };

        // The extend buttons get one addition and one subtraction (alternating if there
        // are somehow more than two), so the two never show the same kind of sum. Which
        // form is generated first is random, and both draw their value from the same
        // range, so neither form tends to be the bigger of the two.
        bool useAddition = Random.value < 0.5f;
        int lastValue = int.MinValue;
        for (int i = 1; i < count; i++)
        {
            Role extend = MakeExtendRole(lastValue, useAddition);
            lastValue = extend.seconds;
            pool.Add(extend);
            useAddition = !useAddition;
        }

        Shuffle(pool);

        for (int i = 0; i < count; i++)
        {
            roles[i] = pool[i];

            TMP_Text label = choiceButtons[i] != null ? choiceButtons[i].ResolveLabel() : null;
            if (label == null)
                continue;

            label.text = roles[i].choice == Choice.Quit
                ? roles[i].display
                : string.Format(extendFormat, roles[i].display);
        }
    }

    // Build one extend role. useAddition picks the form: "lower + lower" when true,
    // "higher - lower" when false. The value is drawn from the same range either way, so
    // neither form tends to be the bigger of the two buttons. avoidValue keeps the two
    // extend buttons off the same number.
    private Role MakeExtendRole(int avoidValue, bool useAddition)
    {
        int low = Mathf.Max(1, Mathf.Min(minExtendSeconds, maxExtendSeconds));
        int high = Mathf.Max(low, Mathf.Max(minExtendSeconds, maxExtendSeconds));

        int value = Random.Range(low, high + 1);
        if (value == avoidValue && high > low)
            value = value == high ? value - 1 : value + 1; // nudge off the other button's value

        string display;
        if (useAddition)
        {
            // Addition: value = a + b, both smaller than the total.
            int a = Random.Range(1, value);    // 1 .. value-1  (value >= low >= 1, and low>=10 in practice)
            int b = value - a;
            display = $"{a} + {b}";
        }
        else
        {
            // Subtraction: value = higher - lower.
            int lower = Random.Range(1, 21);   // small second operand
            int higher = value + lower;
            display = $"{higher} - {lower}";
        }

        return new Role { choice = Choice.Extend, seconds = value, display = display };
    }

    private void OnChoicePressed(int index)
    {
        if (IsFinished || roles == null || index < 0 || index >= roles.Length)
            return;

        if (roles[index].choice == Choice.Quit)
        {
            RequestGameOver();  // give up — instant game over
        }
        else
        {
            ChosenExtendSeconds = roles[index].seconds;
            Complete();         // bought more time — the feed scrolls on
        }
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    protected override string GetProgressLabel() => string.Empty;
}

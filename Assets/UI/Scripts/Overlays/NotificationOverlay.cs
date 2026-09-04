using TMPro;
using UnityEngine;

/// <summary>
/// "New message from: Mom" — a notification banner that pops up independently of
/// the action-overlay cycle (see FeedManager.TryTriggerNotification) rather than
/// replacing a feed panel. Swiping up dismisses it; there is no tap target and no
/// reward for dismissing it, just a chance to stop the extra dopamine drain.
/// </summary>
public class NotificationOverlay : FeedOverlay
{
    [Header("Notification")]
    [Tooltip("Possible senders. One is picked at random each time.")]
    [SerializeField] private string[] senderNames = { "Mom" };

    [Tooltip("Shows who the message is from.")]
    [SerializeField] private TMP_Text messageText;

    [Tooltip("Format for the message label. {0} is the chosen sender.")]
    [SerializeField] private string messageFormat = "New message from: {0}";

    protected override void OnBegin()
    {
        if (messageText == null)
            return;

        string who = (senderNames != null && senderNames.Length > 0)
            ? senderNames[Random.Range(0, senderNames.Length)]
            : "Unknown";
        messageText.text = string.Format(messageFormat, who);
    }

    public override void OnSwipeInput(SwipeDirection direction)
    {
        if (direction == SwipeDirection.Up)
            Complete(); // swiped away
    }

    protected override string GetProgressLabel() => "";
}

using UnityEngine;
using UnityEngine.UI;

// Fake-ad minigame: shows a random ad image and a close button that jumps to a
// new random spot each time it's tapped. After it's been tapped a set number of
// times, the ad closes (win). Overlay, win-only.
public class FakeAdMinigame : MinigameBase
{
    [Header("Ad background")]
    [Tooltip("The Image that displays the ad. A random sprite from Ad Images is assigned on start.")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("Pool of ad images to pick from at random.")]
    [SerializeField] private Sprite[] adImages;

    [Header("Close button")]
    [SerializeField] private Button closeButton;

    [Tooltip("Area the button is allowed to move within. Defaults to the button's parent.")]
    [SerializeField] private RectTransform moveArea;

    [Tooltip("How many taps on the close button are needed before the ad closes.")]
    [Min(1)]
    [SerializeField] private int requiredClicks = 5;

    [Tooltip("How long (seconds) the button takes to glide to its new spot.")]
    [Min(0f)]
    [SerializeField] private float moveDuration = 0.25f;

    private RectTransform buttonRect;
    private int clicks;

    // Smooth-move state.
    private Vector2 moveStart;
    private Vector2 moveTarget;
    private float moveT;
    private bool moving;

    public override void StartGame(MinigameContext context)
    {
        // Pick a random ad image.
        if (backgroundImage != null && adImages != null && adImages.Length > 0)
        {
            backgroundImage.sprite = adImages[Random.Range(0, adImages.Length)];
        }

        clicks = 0;

        if (closeButton != null)
        {
            buttonRect = closeButton.transform as RectTransform;
            if (moveArea == null && buttonRect != null)
            {
                moveArea = buttonRect.parent as RectTransform;
            }

            closeButton.onClick.RemoveListener(OnCloseClicked);
            closeButton.onClick.AddListener(OnCloseClicked);
        }
    }

    private void OnCloseClicked()
    {
        clicks++;

        if (clicks >= Mathf.Max(1, requiredClicks))
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
            Win();
            return;
        }

        MoveButton();
    }

    void Update()
    {
        if (!moving || buttonRect == null)
        {
            return;
        }

        moveT += Time.deltaTime / Mathf.Max(0.0001f, moveDuration);
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(moveT));
        buttonRect.anchoredPosition = Vector2.LerpUnclamped(moveStart, moveTarget, k);

        if (moveT >= 1f)
        {
            moving = false;
        }
    }

    // Picks a new random spot inside the move area (keeping the button fully on
    // screen) and starts gliding toward it. Assumes the button is centre-anchored.
    private void MoveButton()
    {
        if (buttonRect == null || moveArea == null)
        {
            return;
        }

        Vector2 area = moveArea.rect.size;
        Vector2 btn = buttonRect.rect.size;
        float halfX = Mathf.Max(0f, (area.x - btn.x) * 0.5f);
        float halfY = Mathf.Max(0f, (area.y - btn.y) * 0.5f);

        Vector2 target = new Vector2(Random.Range(-halfX, halfX), Random.Range(-halfY, halfY));

        if (moveDuration <= 0f)
        {
            buttonRect.anchoredPosition = target;
            moving = false;
            return;
        }

        moveStart = buttonRect.anchoredPosition;
        moveTarget = target;
        moveT = 0f;
        moving = true;
    }
}

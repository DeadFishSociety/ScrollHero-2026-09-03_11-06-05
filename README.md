# Doomscroll Simulator — Unity 6000.3 Project Base

## 1. Project setup

1. Unity Hub → **New Project** → **2D (URP)** template (this is a UI-only game, 2D is fine, no need for 3D).
2. Once open, go to **Edit → Project Settings → Player → Other Settings → Active Input Handling** and set it to **"Both"**. This lets the simple `Input.GetMouseButtonDown` code below work, and still leaves the new Input System available for touch/mobile later.
3. **Window → Package Manager** → make sure **TextMeshPro** is installed (Unity usually prompts you to "Import TMP Essentials" the first time you drag a TMP object into a scene — click yes).

Unreal comparison: a Unity "Scene" is like an Unreal "Level". A "Prefab" is like a Blueprint class asset you can drag into the world repeatedly.

## 2. Scene hierarchy to build

In the Hierarchy panel, right-click → UI → these objects (Unity auto-creates a Canvas + EventSystem the first time you add any UI object, keep those):

```
Canvas (Screen Space - Overlay)
 ├─ FeedContainer        (empty RectTransform, stretched full screen — panels spawn inside this)
 ├─ ScrollCountText      (TextMeshPro - Text, top corner)
 └─ DopamineSlider       (UI - Slider, top or bottom of screen)
EventSystem              (auto-created, leave as is)
GameManager              (empty GameObject — holds your scripts, like a "Game Mode" actor in Unreal)
```

To stretch `FeedContainer` full screen: select it, in the RectTransform hold Alt+Shift while clicking the bottom-right "stretch" anchor preset in the Inspector.

## 3. Prefabs to build

**ScrollPanel prefab** (the green "content"):
- Create UI → Image, rename `ScrollPanel`, stretch it full screen (same anchor trick as above), set its `Color` to green.
- Add the `FeedItem` script (below) to it, set `Type = Scroll`, drag the Image component into the `Display Image` field.
- Drag it from Hierarchy into your Project window `/Prefabs` folder to make it a prefab, then delete it from the scene.

**ActionPanel prefab** (the red "event", e.g. dismiss a call):
- Same as above but red, `Type = Action`.
- Set `Required Swipe` to whichever direction should dismiss it (e.g. `Left`, to simulate swiping away a notification — different from the `Up` swipe used for normal scrolling).
- Make it a prefab too.

Later, replacing green/red rectangles with actual video: swap the `Image` component for a `RawImage` + `VideoPlayer` component, and point `VideoPlayer.targetTexture` at a `RenderTexture` assigned to the `RawImage`. The `FeedItem` script below is written so that swap only touches the prefab, not your manager logic.

## 4. Scripts

Create a `/Scripts` folder in your Project window. Create each file below (Right-click → Create → C# Script), paste the contents in, then attach as noted.

### SwipeInput.cs
Attach to `GameManager`. Detects a mouse/touch drag and fires an event with the direction.

```csharp
using UnityEngine;

public enum SwipeDirection { None, Up, Down, Left, Right }

public class SwipeInput : MonoBehaviour
{
    [Header("Swipe Settings")]
    [SerializeField] private float minSwipeDistance = 50f; // in pixels

    private Vector2 startPos;
    private bool isTouching;

    public delegate void SwipeEvent(SwipeDirection direction);
    public event SwipeEvent OnSwipe;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            startPos = Input.mousePosition;
            isTouching = true;
        }
        else if (Input.GetMouseButtonUp(0) && isTouching)
        {
            isTouching = false;
            Vector2 delta = (Vector2)Input.mousePosition - startPos;

            if (delta.magnitude < minSwipeDistance)
                return; // too small, treat as a tap, ignore

            OnSwipe?.Invoke(GetDirection(delta));
        }
    }

    private SwipeDirection GetDirection(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x > 0 ? SwipeDirection.Right : SwipeDirection.Left;
        return delta.y > 0 ? SwipeDirection.Up : SwipeDirection.Down;
    }
}
```

### FeedItem.cs
Attach to both prefabs. Just holds data about what kind of panel this is.

```csharp
using UnityEngine;
using UnityEngine.UI;

public enum FeedItemType { Scroll, Action }

public class FeedItem : MonoBehaviour
{
    public FeedItemType Type;

    [Tooltip("Later: swap this for a RawImage + VideoPlayer to show real video.")]
    public Image DisplayImage;

    [Tooltip("Action panels only — which swipe correctly dismisses this panel.")]
    public SwipeDirection RequiredSwipe = SwipeDirection.Left;
}
```

### DopamineMeter.cs
Attach to `GameManager`. Drives the Slider UI.

```csharp
using UnityEngine;
using UnityEngine.UI;

public class DopamineMeter : MonoBehaviour
{
    [SerializeField] private Slider slider; // range 0-1
    [SerializeField] private float decayPerSecond = 0.05f;
    [SerializeField] private float scrollBoost = 0.1f;
    [SerializeField] private float actionSuccessBoost = 0.15f;
    [SerializeField] private float actionFailPenalty = 0.1f;

    private float value = 1f;

    void Start() => slider.value = value;

    void Update()
    {
        value = Mathf.Clamp01(value - decayPerSecond * Time.deltaTime);
        slider.value = value;
    }

    public void OnScroll() => value = Mathf.Clamp01(value + scrollBoost);
    public void OnSuccessfulAction() => value = Mathf.Clamp01(value + actionSuccessBoost);
    public void OnFailedAction() => value = Mathf.Clamp01(value - actionFailPenalty);
}
```

### FeedManager.cs
Attach to `GameManager`. This is the "brain" — spawns panels, counts scrolls, triggers events every N scrolls.

```csharp
using UnityEngine;
using TMPro;

public class FeedManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private FeedItem scrollPanelPrefab;
    [SerializeField] private FeedItem actionPanelPrefab;

    [Header("References")]
    [SerializeField] private RectTransform feedContainer;
    [SerializeField] private SwipeInput swipeInput;
    [SerializeField] private DopamineMeter dopamineMeter;
    [SerializeField] private TMP_Text scrollCountText;

    [Header("Settings")]
    [SerializeField] private int actionEveryNScrolls = 5;

    private int scrollCount;
    private FeedItem currentItem;
    private bool waitingForActionResponse;

    void Start()
    {
        swipeInput.OnSwipe += HandleSwipe;
        SpawnNext(FeedItemType.Scroll);
    }

    void OnDestroy() => swipeInput.OnSwipe -= HandleSwipe;

    private void HandleSwipe(SwipeDirection direction)
    {
        if (waitingForActionResponse)
        {
            if (direction == currentItem.RequiredSwipe)
            {
                waitingForActionResponse = false;
                dopamineMeter.OnSuccessfulAction();
                SpawnNext(FeedItemType.Scroll);
            }
            else
            {
                dopamineMeter.OnFailedAction();
            }
            return;
        }

        if (direction != SwipeDirection.Up)
            return; // only an upward swipe counts as "scrolling"

        scrollCount++;
        scrollCountText.text = $"Scrolls: {scrollCount}";
        dopamineMeter.OnScroll();

        if (scrollCount % actionEveryNScrolls == 0)
        {
            SpawnNext(FeedItemType.Action);
            waitingForActionResponse = true;
        }
        else
        {
            SpawnNext(FeedItemType.Scroll);
        }
    }

    private void SpawnNext(FeedItemType type)
    {
        if (currentItem != null)
            Destroy(currentItem.gameObject);

        FeedItem prefab = type == FeedItemType.Scroll ? scrollPanelPrefab : actionPanelPrefab;
        currentItem = Instantiate(prefab, feedContainer);

        // stretch the new panel to fill the container
        RectTransform rt = currentItem.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
```

## 5. Wiring it up in the Inspector

Select `GameManager` in the Hierarchy. It should now show 3 components (`SwipeInput`, `DopamineMeter`, `FeedManager`). Fill in their exposed fields by dragging from the Hierarchy/Project window:

- `DopamineMeter.Slider` → your `DopamineSlider` object
- `FeedManager.Scroll Panel Prefab` → your `ScrollPanel` prefab
- `FeedManager.Action Panel Prefab` → your `ActionPanel` prefab
- `FeedManager.Feed Container` → your `FeedContainer` object
- `FeedManager.Swipe Input` → drag `GameManager` itself (it has the `SwipeInput` component on it)
- `FeedManager.Dopamine Meter` → drag `GameManager` itself
- `FeedManager.Scroll Count Text` → your `ScrollCountText` object

Press Play. Click-drag upward on screen a few times (this simulates a swipe with the mouse) — you should see the green panel refresh and the counter go up. Every 5th one, a red panel appears; swipe left to dismiss it. Dopamine bar should tick down while idle and jump up on each successful swipe.

## 6. Where to go from here

- Swap `Image`/`Color` on the prefabs for `RawImage` + `VideoPlayer` + `RenderTexture` to play real video clips — `FeedManager` doesn't need to change at all.
- Object pooling: right now panels are `Instantiate`/`Destroy`d each swipe, which is fine for a prototype but wasteful long-term — look into a simple pool once the base loop feels good.
- Multiple event types (call vs. notification) can be added by making `ActionPanel` into several prefabs and picking one randomly in `SpawnNext`.

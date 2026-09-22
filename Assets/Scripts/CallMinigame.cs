using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// "Refuse the call" minigame. An incoming call shows a random caller name. To
// hang up, the player must drag the HangUp button along a winding path from its
// start checkpoint to the end. The path is drawn as a line with visual
// checkpoints along it. The drag has to stay near the line and move in order -
// stray too far (or let go early) and the button snaps back to the start.
// Reaching the end wins. Win-only: you can't lose, you just have to get it right.
//
// Paths are authored as child objects: pathsParent holds one child per path,
// and each of those holds ordered checkpoint children whose positions define the
// polyline. A random path is shown each time the minigame appears.
public class CallMinigame : MinigameBase
{
    [Header("Caller")]
    [Tooltip("Label that shows who's calling.")]
    [SerializeField] private TMP_Text callerLabel;

    [Tooltip("Caller names to pick from at random.")]
    [SerializeField]
    private string[] callerNames =
    {
        "Mom", "Dad", "Unknown", "Boss", "Scam Likely", "No Caller ID", "Grandma"
    };

    [Header("Drag target")]
    [Tooltip("The button the player drags along the path (the HangUp button).")]
    [SerializeField] private RectTransform dragButton;

    [Header("Paths")]
    [Tooltip("Parent whose children are the paths. Each path's children are its ordered checkpoints.")]
    [SerializeField] private Transform pathsParent;

    [Tooltip("How far (in canvas units) the drag may stray from the line before it snaps back to the start (at difficulty 0).")]
    [SerializeField] private float strayTolerance = 90f;

    [Tooltip("Stray tolerance at difficulty 1 (smaller = less forgiving). Scales between this and Stray Tolerance by difficulty.")]
    [SerializeField] private float strayToleranceAtMaxDifficulty = 45f;

    [Tooltip("Most the button may advance along the path in one drag update, to stop skipping ahead.")]
    [SerializeField] private float maxAdvance = 300f;

    [Tooltip("How close to the end (in path length) counts as finishing.")]
    [SerializeField] private float finishThreshold = 40f;

    [Header("Line visuals")]
    [Tooltip("Thickness of the generated guide line.")]
    [SerializeField] private float lineThickness = 12f;

    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.5f);

    [Header("Checkpoint particles")]
    [Tooltip("Bursts a particle effect each time a checkpoint is crossed. Assign its sprite and tweak the look on this spawner. Leave empty for no effect.")]
    [SerializeField] private HeartParticleSpawner checkpointParticles;

    [Header("Sound")]
    [Tooltip("Source the checkpoint sound plays through. Auto-added if left empty.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Played each time a checkpoint is crossed. Leave empty for none.")]
    [SerializeField] private AudioClip checkpointSound;

    // World-space points of the active path's checkpoints, their anchored
    // positions (for drawing the line), and the cumulative length up to each one.
    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<Vector2> anchored = new List<Vector2>();
    private readonly List<float> cumulative = new List<float>();
    private float totalLength;

    // Generated line-segment objects for the active path, so we can clean them up.
    private readonly List<GameObject> generatedLines = new List<GameObject>();

    private Transform activePath;
    private RectTransform selfRect;
    private Canvas canvas;
    private bool[] checkpointCrossed;
    private float progress;   // arc length the button currently sits at
    private bool dragging;
    private bool solved;
    private float activeStrayTolerance; // difficulty-scaled, resolved in StartGame

    void Awake()
    {
        // Hide every authored path so none flash on screen while the overlay
        // scales in; StartGame turns exactly one back on.
        if (pathsParent != null)
        {
            for (int i = 0; i < pathsParent.childCount; i++)
            {
                pathsParent.GetChild(i).gameObject.SetActive(false);
            }
        }
    }

    public override void StartGame(MinigameContext context)
    {
        selfRect = transform as RectTransform;
        canvas = GetComponentInParent<Canvas>();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;

        // Harder = the drag has to hug the line more tightly.
        activeStrayTolerance = Mathf.Lerp(strayTolerance, strayToleranceAtMaxDifficulty, Mathf.Clamp01(context.difficulty));

        PickCallerName();
        Transform path = PickRandomPath();
        if (path == null)
        {
            Debug.LogError("CallMinigame: no paths found under pathsParent - can't play.", this);
            return;
        }

        BuildPath(path);
        DrawLine();

        progress = 0f;
        solved = false;
        SnapButtonTo(0f);

        AttachDragHandle();
    }

    // --- Setup ---------------------------------------------------------------

    private void PickCallerName()
    {
        if (callerLabel != null && callerNames != null && callerNames.Length > 0)
        {
            callerLabel.text = callerNames[Random.Range(0, callerNames.Length)];
        }
    }

    // Enables one random path child and disables the rest.
    private Transform PickRandomPath()
    {
        if (pathsParent == null || pathsParent.childCount == 0)
        {
            return null;
        }

        int choice = Random.Range(0, pathsParent.childCount);
        activePath = null;
        for (int i = 0; i < pathsParent.childCount; i++)
        {
            Transform child = pathsParent.GetChild(i);
            bool on = i == choice;
            child.gameObject.SetActive(on);
            if (on)
            {
                activePath = child;
            }
        }
        return activePath;
    }

    // Reads the checkpoint children of the chosen path into a world-space polyline.
    private void BuildPath(Transform path)
    {
        points.Clear();
        anchored.Clear();
        cumulative.Clear();
        totalLength = 0f;

        // Read every checkpoint up front, before any line segments get parented
        // under the path (which would otherwise shift the child indices).
        for (int i = 0; i < path.childCount; i++)
        {
            Transform cp = path.GetChild(i);
            points.Add(cp.position);
            RectTransform crt = cp as RectTransform;
            anchored.Add(crt != null ? crt.anchoredPosition : (Vector2)cp.localPosition);
        }

        cumulative.Add(0f);
        for (int i = 1; i < points.Count; i++)
        {
            totalLength += Vector3.Distance(points[i - 1], points[i]);
            cumulative.Add(totalLength);
        }

        checkpointCrossed = new bool[points.Count];
    }

    // Draws a UI line segment between each pair of consecutive checkpoints so the
    // player can see the route. Segments sit behind the checkpoint markers.
    private void DrawLine()
    {
        foreach (GameObject go in generatedLines)
        {
            if (go != null)
            {
                Destroy(go);
            }
        }
        generatedLines.Clear();

        if (activePath == null || anchored.Count < 2)
        {
            return;
        }

        // Iterate the captured positions, not live children: creating segments
        // below changes activePath's child list as we go.
        for (int i = 1; i < anchored.Count; i++)
        {
            Vector2 pa = anchored[i - 1];
            Vector2 pb = anchored[i];
            Vector2 dir = pb - pa;
            float len = dir.magnitude;

            GameObject seg = new GameObject("LineSegment", typeof(RectTransform), typeof(Image));
            RectTransform rt = seg.GetComponent<RectTransform>();
            rt.SetParent(activePath, false);
            rt.SetAsFirstSibling(); // draw behind the checkpoint dots

            Image img = seg.GetComponent<Image>();
            img.color = lineColor;
            img.raycastTarget = false;

            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(len, lineThickness);
            rt.anchoredPosition = (pa + pb) * 0.5f;
            rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            generatedLines.Add(seg);
        }
    }

    private void AttachDragHandle()
    {
        if (dragButton == null)
        {
            return;
        }

        // Keep the draggable on top of its siblings (e.g. the PickUp button) so
        // nothing renders over it or steals the touch when a path starts on top
        // of another element.
        dragButton.SetAsLastSibling();

        PathDragHandle handle = dragButton.GetComponent<PathDragHandle>();
        if (handle == null)
        {
            handle = dragButton.gameObject.AddComponent<PathDragHandle>();
        }
        handle.Owner = this;
    }

    // --- Drag handling (called by PathDragHandle) ----------------------------

    public void OnDragBegin(PointerEventData e)
    {
        if (solved)
        {
            return;
        }
        dragging = true;
        UpdateDrag(e);
    }

    public void OnDragMove(PointerEventData e)
    {
        if (dragging)
        {
            UpdateDrag(e);
        }
    }

    public void OnDragEnd(PointerEventData e)
    {
        if (!dragging || solved)
        {
            return;
        }
        dragging = false;

        // Let go before reaching the end: back to the start.
        if (progress < totalLength - finishThreshold)
        {
            ResetToStart();
        }
    }

    private void UpdateDrag(PointerEventData e)
    {
        if (points.Count < 2)
        {
            return;
        }

        Vector3 world;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                selfRect, e.position, e.pressEventCamera, out world))
        {
            return;
        }

        float bestDist;
        float bestArc = ClosestPointOnPath(world, out bestDist);

        // Strayed too far from the line, or tried to jump ahead: snap to start.
        if (bestDist > activeStrayTolerance || bestArc > progress + maxAdvance)
        {
            ResetToStart();
            return;
        }

        progress = bestArc;
        SnapButtonTo(progress);
        CheckCheckpointCrossings();

        if (progress >= totalLength - finishThreshold)
        {
            Complete();
        }
    }

    private void Complete()
    {
        if (solved)
        {
            return;
        }
        solved = true;
        dragging = false;
        progress = totalLength;
        SnapButtonTo(totalLength);
        CheckCheckpointCrossings(); // fire the final checkpoint too
        Win();
    }

    // Sends the button and progress back to the path start and re-arms every
    // checkpoint so they burst again on the next attempt.
    private void ResetToStart()
    {
        progress = 0f;
        SnapButtonTo(0f);
        if (checkpointCrossed != null)
        {
            System.Array.Clear(checkpointCrossed, 0, checkpointCrossed.Length);
        }
    }

    // Bursts the particle effect and plays a sound at any checkpoint the button
    // has newly passed (skipping the start point). Each checkpoint fires once per
    // attempt.
    private void CheckCheckpointCrossings()
    {
        if (checkpointCrossed == null)
        {
            return;
        }
        for (int i = 1; i < points.Count && i < checkpointCrossed.Length; i++)
        {
            if (!checkpointCrossed[i] && progress >= cumulative[i])
            {
                checkpointCrossed[i] = true;
                if (checkpointParticles != null)
                {
                    checkpointParticles.Burst(WorldToScreen(points[i]));
                }
                if (checkpointSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(checkpointSound);
                }
            }
        }
    }

    private Vector2 WorldToScreen(Vector3 world)
    {
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;
        return RectTransformUtility.WorldToScreenPoint(cam, world);
    }

    // --- Path maths ----------------------------------------------------------

    // Finds the point on the polyline closest to 'world', returning its arc
    // length and (via out) the perpendicular distance to it.
    private float ClosestPointOnPath(Vector3 world, out float distance)
    {
        float bestArc = 0f;
        float bestSqr = float.MaxValue;
        distance = 0f;

        for (int i = 1; i < points.Count; i++)
        {
            Vector3 a = points[i - 1];
            Vector3 b = points[i];
            Vector3 ab = b - a;
            float segSqr = ab.sqrMagnitude;
            float t = segSqr > 0.0001f ? Mathf.Clamp01(Vector3.Dot(world - a, ab) / segSqr) : 0f;
            Vector3 proj = a + ab * t;
            float sqr = (world - proj).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestArc = cumulative[i - 1] + Mathf.Sqrt(segSqr) * t;
            }
        }

        distance = Mathf.Sqrt(bestSqr);
        return bestArc;
    }

    // Moves the drag button to the world point at the given arc length.
    private void SnapButtonTo(float arc)
    {
        if (dragButton == null || points.Count == 0)
        {
            return;
        }
        dragButton.position = PointAtArc(arc);
    }

    private Vector3 PointAtArc(float arc)
    {
        if (points.Count == 1)
        {
            return points[0];
        }
        arc = Mathf.Clamp(arc, 0f, totalLength);

        for (int i = 1; i < points.Count; i++)
        {
            if (arc <= cumulative[i] || i == points.Count - 1)
            {
                float segLen = cumulative[i] - cumulative[i - 1];
                float t = segLen > 0.0001f ? (arc - cumulative[i - 1]) / segLen : 0f;
                return Vector3.Lerp(points[i - 1], points[i], t);
            }
        }
        return points[points.Count - 1];
    }
}

// Sits on the drag button and forwards its drag events to the CallMinigame that
// owns it. Kept separate so the controller can live on the minigame root while
// the drag is detected on the button itself.
public class PathDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CallMinigame Owner { get; set; }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Owner != null)
        {
            Owner.OnDragBegin(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Owner != null)
        {
            Owner.OnDragMove(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (Owner != null)
        {
            Owner.OnDragEnd(eventData);
        }
    }
}

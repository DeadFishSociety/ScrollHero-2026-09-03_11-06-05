using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI.Extensions; // requires the Unity UI Extensions package

/// <summary>
/// Copies a list of waypoint RectTransforms into a UILineRenderer's Points so the
/// drawn spline always matches the waypoints your CallOverlay path actually uses.
/// Place this on the same object as the UILineRenderer, drag in the SAME waypoints
/// you gave the CallPath, and the line updates itself — no typing coordinates.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(UILineRenderer))]
public class UILinePathSync : MonoBehaviour
{
    [Tooltip("The path's waypoints, in order. Use the same ones assigned to the CallPath.")]
    [SerializeField] private List<RectTransform> waypoints = new List<RectTransform>();

    private UILineRenderer line;

    private void OnEnable() => Sync();

    private void Update()
    {
        // Keep it live while editing in the Scene view; no need to run every frame in play.
        if (!Application.isPlaying)
            Sync();
    }

    public void Sync()
    {
        if (line == null)
            line = GetComponent<UILineRenderer>();

        RectTransform self = transform as RectTransform;
        if (line == null || self == null || waypoints == null || waypoints.Count == 0)
            return;

        Vector2[] points = new Vector2[waypoints.Count];
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null)
                continue;

            // World position of the waypoint, expressed in this line's local space.
            points[i] = self.InverseTransformPoint(waypoints[i].position);
        }

        line.Points = points;
    }
}

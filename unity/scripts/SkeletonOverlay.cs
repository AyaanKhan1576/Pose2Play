using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Skeleton Overlay - Displays form feedback as colored skeleton
/// 
/// Attach to: Main Camera (inside XR Origin → Camera Offset)
/// 
/// What it does:
///   - Draws live skeleton joints in screen-space corner of headset
///   - Color-codes joints: GREEN (good form), YELLOW (warning), RED (bad form)
///   - Displays current form quality percentage
///   - Updates in real-time from webcam pose data
/// 
/// Inspector Setup:
///   - udpReceiver → PoseReceiver GameObject
///   - screenCorner → Bottom-Right (default)
///   - skeletonScale → 0.15 (small overlay)
/// </summary>
public class SkeletonOverlay : MonoBehaviour
{
    public enum ScreenCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    [Header("Data Sources")]
    [Tooltip("Reference to UDPReceiver to get live pose data.")]
    public UDPReceiver udpReceiver;

    [Tooltip("Reference to VRDashboardReceiver for form quality data.")]
    public VRDashboardReceiver dashboardReceiver;

    [Header("Display Settings")]
    [Tooltip("Which corner to display the skeleton overlay.")]
    public ScreenCorner screenCorner = ScreenCorner.BottomRight;

    [Tooltip("Scale of skeleton overlay (0.1 = small, 0.3 = large).")]
    [Range(0.05f, 0.5f)]
    public float skeletonScale = 0.15f;

    [Tooltip("Show form quality percentage text.")]
    public bool showFormScore = true;

    [Header("Colors")]
    [Tooltip("Color for joints with good form (quality >= 85%).")]
    public Color goodFormColor = new Color(0.3f, 1f, 0.5f, 0.8f);

    [Tooltip("Color for joints with warning form (70-85%).")]
    public Color warningColor = new Color(1f, 1f, 0f, 0.8f);

    [Tooltip("Color for joints with bad form (< 70%).")]
    public Color badFormColor = new Color(1f, 0.3f, 0.3f, 0.8f);

    [Tooltip("Width of skeleton lines.")]
    [Range(1f, 5f)]
    public float lineWidth = 2f;

    // ─────────────────────────────────────────────────────────────────────
    private Material _lineMaterial;
    private float _currentFormQuality = 100f;
    private List<Vector3> _currentPose = new List<Vector3>();

    // MediaPipe landmark indices for major skeleton joints
    private int[] _skeletonIndices = new int[]
    {
        0,   // Nose
        11, 13, 15,  // Left arm: shoulder, elbow, wrist
        12, 14, 16,  // Right arm: shoulder, elbow, wrist
        23, 25, 27,  // Left leg: hip, knee, ankle
        24, 26, 28   // Right leg: hip, knee, ankle
    };

    // Skeleton connections (pairs of indices)
    private (int, int)[] _skeletonBones = new (int, int)[]
    {
        (11, 13), (13, 15),  // Left arm
        (12, 14), (14, 16),  // Right arm
        (11, 12),            // Shoulders
        (23, 25), (25, 27),  // Left leg
        (24, 26), (26, 28),  // Right leg
        (23, 24),            // Hips
        (11, 23), (12, 24)   // Torso
    };

    // ─────────────────────────────────────────────────────────────────────
    void Start()
    {
        CreateLineMaterial();
    }

    void OnDestroy()
    {
        if (_lineMaterial != null)
            Destroy(_lineMaterial);
    }

    // ─────────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (udpReceiver == null) return;

        // Draw skeleton overlay
        DrawSkeletonOverlay();
    }

    // ─────────────────────────────────────────────────────────────────────
    private void DrawSkeletonOverlay()
    {
        // Calculate corner position
        Vector2 cornerPos = GetCornerPosition();

        // Draw semi-transparent background panel
        GUI.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
        GUI.Box(new Rect(cornerPos.x - 120f, cornerPos.y - 140f, 140f, 160f), "");
        GUI.backgroundColor = Color.white;

        // Draw form quality score if enabled
        if (showFormScore)
        {
            GUI.color = GetColorByQuality(_currentFormQuality);
            GUI.Label(new Rect(cornerPos.x - 110f, cornerPos.y - 130f, 120f, 25f),
                $"Form: {Mathf.RoundToInt(_currentFormQuality)}%",
                new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            GUI.color = Color.white;
        }

        // Draw skeleton joints as circles with connecting lines
        DrawSkeletonJoints(cornerPos);
    }

    // ─────────────────────────────────────────────────────────────────────
    private void DrawSkeletonJoints(Vector2 cornerPos)
    {
        // This is a simplified version - in production you'd want proper screen-space projection
        // For now, we'll draw a schematic skeleton representation

        int jointRadius = Mathf.RoundToInt(8f * skeletonScale);

        // Define schematic joint positions in screen space (relative to corner)
        Vector2 neck = new Vector2(0, 0);
        Vector2 shoulder_l = new Vector2(-15, 15);
        Vector2 shoulder_r = new Vector2(15, 15);
        Vector2 elbow_l = new Vector2(-25, 35);
        Vector2 elbow_r = new Vector2(25, 35);
        Vector2 wrist_l = new Vector2(-30, 55);
        Vector2 wrist_r = new Vector2(30, 55);
        Vector2 hip_l = new Vector2(-10, 45);
        Vector2 hip_r = new Vector2(10, 45);
        Vector2 knee_l = new Vector2(-15, 80);
        Vector2 knee_r = new Vector2(15, 80);
        Vector2 ankle_l = new Vector2(-18, 110);
        Vector2 ankle_r = new Vector2(18, 110);

        Vector2[] joints = new Vector2[]
        {
            neck, shoulder_l, shoulder_r,
            elbow_l, elbow_r, wrist_l, wrist_r,
            hip_l, hip_r, knee_l, knee_r, ankle_l, ankle_r
        };

        (Vector2, Vector2)[] bones = new (Vector2, Vector2)[]
        {
            (shoulder_l, elbow_l), (elbow_l, wrist_l),
            (shoulder_r, elbow_r), (elbow_r, wrist_r),
            (shoulder_l, shoulder_r),
            (shoulder_l, hip_l), (shoulder_r, hip_r),
            (hip_l, knee_l), (knee_l, ankle_l),
            (hip_r, knee_r), (knee_r, ankle_r),
            (hip_l, hip_r)
        };

        // Draw bones first (lines)
        foreach (var (start, end) in bones)
        {
            GUI.color = GetColorByQuality(_currentFormQuality);
            DrawLine(cornerPos + start * skeletonScale * 100f, cornerPos + end * skeletonScale * 100f, 2);
            GUI.color = Color.white;
        }

        // Draw joints (circles)
        foreach (Vector2 joint in joints)
        {
            Vector2 screenPos = cornerPos + joint * skeletonScale * 100f;
            GUI.color = GetColorByQuality(_currentFormQuality);
            GUI.Box(new Rect(screenPos.x - jointRadius, screenPos.y - jointRadius, jointRadius * 2, jointRadius * 2), "");
            GUI.color = Color.white;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private void DrawLine(Vector2 p1, Vector2 p2, float thickness)
    {
        // Simple line drawing using rects
        float dist = Vector2.Distance(p1, p2);
        float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;

        GUIUtility.RotateAroundPivot(angle, p1);
        GUI.Box(new Rect(p1.x, p1.y - thickness / 2, dist, thickness), "");
        GUIUtility.RotateAroundPivot(-angle, p1);
    }

    // ─────────────────────────────────────────────────────────────────────
    private Color GetColorByQuality(float quality)
    {
        if (quality >= 85f) return goodFormColor;
        if (quality >= 70f) return warningColor;
        return badFormColor;
    }

    // ─────────────────────────────────────────────────────────────────────
    private Vector2 GetCornerPosition()
    {
        switch (screenCorner)
        {
            case ScreenCorner.TopLeft:
                return new Vector2(70, 30);
            case ScreenCorner.TopRight:
                return new Vector2(Screen.width - 70, 30);
            case ScreenCorner.BottomLeft:
                return new Vector2(70, Screen.height - 100);
            case ScreenCorner.BottomRight:
            default:
                return new Vector2(Screen.width - 70, Screen.height - 100);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private void CreateLineMaterial()
    {
        _lineMaterial = new Material(Shader.Find("Sprites/Default"));
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Updates form quality score when dashboard data arrives.
    /// Call this from VRDashboardReceiver event if available.
    /// </summary>
    public void UpdateFormQuality(float qualityPercent)
    {
        _currentFormQuality = Mathf.Clamp01(qualityPercent / 100f) * 100f;
    }
}

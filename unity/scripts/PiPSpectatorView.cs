using UnityEngine;

/// <summary>
/// PiP Spectator View - Shows coach/therapist camera in corner of headset
/// 
/// Attach to: Main Camera (inside XR Origin → Camera Offset)
/// 
/// What it does:
///   - Renders a second camera (SpectatorCamera) to a RenderTexture
///   - Displays that texture in a corner of the headset HUD
///   - Adds a green border frame + "Coach View" label
///   - Shows what a therapist/coach would see from outside
/// 
/// Setup:
///   1. Create RenderTexture named "PiPSpectatorTexture" (640x480)
///   2. Create GameObject "SpectatorCamera" with Camera component
///   3. Set SpectatorCamera → Target Texture = PiPSpectatorTexture
///   4. Position SpectatorCamera to view exercise from side angle
///   5. Attach this script to Main Camera
///   6. Drag PiPSpectatorTexture into the Inspector field
/// </summary>
public class PiPSpectatorView : MonoBehaviour
{
    public enum PiPCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    [Header("Render Texture")]
    [Tooltip("The RenderTexture from SpectatorCamera.")]
    public RenderTexture spectatorRenderTexture;

    [Header("Display Settings")]
    [Tooltip("Which corner to display the PiP window.")]
    public PiPCorner pipCorner = PiPCorner.TopRight;

    [Tooltip("Size of the PiP window in pixels.")]
    public Vector2 pipSize = new Vector2(300f, 225f);

    [Tooltip("Padding from screen edge.")]
    public float edgePadding = 20f;

    [Tooltip("Show 'Coach View' label above window.")]
    public bool showLabel = true;

    [Header("Styling")]
    [Tooltip("Color of the border frame.")]
    public Color borderColor = new Color(0f, 1f, 0f, 0.8f);

    [Tooltip("Thickness of border frame in pixels.")]
    public float borderWidth = 3f;

    [Tooltip("Label text color.")]
    public Color labelColor = Color.white;

    // ─────────────────────────────────────────────────────────────────────
    private Texture2D _borderTexture;
    private GUIStyle _labelStyle;

    // ─────────────────────────────────────────────────────────────────────
    void Start()
    {
        CreateBorderTexture();
        CreateLabelStyle();

        if (spectatorRenderTexture == null)
            Debug.LogWarning("[PiPSpectatorView] spectatorRenderTexture not assigned! PiP will not display.");
    }

    void OnGUI()
    {
        if (spectatorRenderTexture == null) return;

        // Get PiP window position
        Rect pipRect = GetPiPRect();

        // Draw the RenderTexture
        GUI.DrawTexture(pipRect, spectatorRenderTexture, ScaleMode.StretchToFill, alphaBlend: true);

        // Draw border frame
        DrawBorder(pipRect);

        // Draw label
        if (showLabel)
        {
            Rect labelRect = new Rect(pipRect.x, pipRect.y - 25f, pipRect.width, 25f);
            GUI.color = labelColor;
            GUI.Label(labelRect, "Coach View", _labelStyle);
            GUI.color = Color.white;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private Rect GetPiPRect()
    {
        float x = 0f, y = 0f;

        switch (pipCorner)
        {
            case PiPCorner.TopLeft:
                x = edgePadding;
                y = edgePadding;
                break;
            case PiPCorner.TopRight:
                x = Screen.width - pipSize.x - edgePadding;
                y = edgePadding;
                break;
            case PiPCorner.BottomLeft:
                x = edgePadding;
                y = Screen.height - pipSize.y - edgePadding;
                break;
            case PiPCorner.BottomRight:
                x = Screen.width - pipSize.x - edgePadding;
                y = Screen.height - pipSize.y - edgePadding;
                break;
        }

        return new Rect(x, y, pipSize.x, pipSize.y);
    }

    // ─────────────────────────────────────────────────────────────────────
    private void DrawBorder(Rect rect)
    {
        GUI.color = borderColor;

        // Top border
        GUI.Box(new Rect(rect.x, rect.y, rect.width, borderWidth), "");
        // Bottom border
        GUI.Box(new Rect(rect.x, rect.y + rect.height - borderWidth, rect.width, borderWidth), "");
        // Left border
        GUI.Box(new Rect(rect.x, rect.y, borderWidth, rect.height), "");
        // Right border
        GUI.Box(new Rect(rect.x + rect.width - borderWidth, rect.y, borderWidth, rect.height), "");

        GUI.color = Color.white;
    }

    // ─────────────────────────────────────────────────────────────────────
    private void CreateBorderTexture()
    {
        _borderTexture = new Texture2D(1, 1);
        _borderTexture.SetPixel(0, 0, Color.white);
        _borderTexture.Apply();
    }

    private void CreateLabelStyle()
    {
        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    void OnDestroy()
    {
        if (_borderTexture != null)
            Destroy(_borderTexture);
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;

/// <summary>
/// Skeleton Overlay - Displays form feedback as an avatar-attached body overlay
/// 
/// Attach to: Avatar root or a child object under the animated avatar
/// 
/// What it does:
///   - Draws a world-space body overlay that follows the avatar bones
///   - Color-codes exercise-relevant body regions for rehab feedback
///   - Displays a small muscle legend for the current exercise
///   - Updates in real-time from webcam pose data + dashboard exercise packets
/// 
/// Inspector Setup:
///   - udpReceiver → PoseReceiver GameObject
///   - dashboardReceiver → Dashboard receiver GameObject
///   - avatarRoot → avatar GameObject root (optional auto-find)
///   - bone transforms are auto-found if named like Mixamo bones
/// </summary>
public class SkeletonOverlay : MonoBehaviour
{
    public enum OverlayMode { WorldBodyOverlay, ScreenLegendOnly }

    public enum BodyRegion
    {
        Head,
        Torso,
        Core,
        LeftUpperArm,
        LeftForearm,
        RightUpperArm,
        RightForearm,
        LeftThigh,
        LeftCalf,
        RightThigh,
        RightCalf
    }

    [Serializable]
    private class MuscleGroup
    {
        public string label;
        public string detail;
        public Color color;
        public bool primary;

        public MuscleGroup(string label, string detail, Color color, bool primary)
        {
            this.label = label;
            this.detail = detail;
            this.color = color;
            this.primary = primary;
        }
    }

    private class ExerciseProfile
    {
        public string name;
        public string note;
        public Color bodyAccent;
        public MuscleGroup[] muscleGroups;
        public Dictionary<BodyRegion, float> regionIntensity;

        public ExerciseProfile(string name, string note, Color bodyAccent, MuscleGroup[] muscleGroups, Dictionary<BodyRegion, float> regionIntensity)
        {
            this.name = name;
            this.note = note;
            this.bodyAccent = bodyAccent;
            this.muscleGroups = muscleGroups;
            this.regionIntensity = regionIntensity;
        }
    }

    [Header("Data Sources")]
    [Tooltip("Reference to UDPReceiver to get live pose data.")]
    public UDPReceiver udpReceiver;

    [Tooltip("Reference to VRDashboardReceiver for form quality data.")]
    public VRDashboardReceiver dashboardReceiver;

    [Tooltip("Root transform of the animated avatar. Used to attach the overlay.")]
    public Transform avatarRoot;

    [Header("Avatar Bones")]
    public Transform headBone;
    public Transform torsoBone;
    public Transform leftShoulderBone;
    public Transform leftUpperArm;
    public Transform leftForearm;
    public Transform rightShoulderBone;
    public Transform rightUpperArm;
    public Transform rightForearm;
    public Transform leftThigh;
    public Transform leftCalf;
    public Transform rightThigh;
    public Transform rightCalf;

    [Header("Display Settings")]
    [Tooltip("World body overlay follows the avatar bones. Screen legend only keeps the legend in HUD form.")]
    public OverlayMode overlayMode = OverlayMode.WorldBodyOverlay;

    [Tooltip("Scale of the world overlay body markers.")]
    [Range(0.01f, 0.25f)]
    public float bodyOverlayScale = 0.06f;

    [Tooltip("How far in front of the avatar to place the overlay so it does not clip the mesh.")]
    [Range(0.001f, 0.2f)]
    public float overlayOffset = 0.03f;

    [Tooltip("Thickness of world-space overlay lines.")]
    [Range(0.001f, 0.04f)]
    public float overlayLineWidth = 0.012f;

    [Tooltip("Show form quality percentage text.")]
    public bool showFormScore = true;

    [Tooltip("Show the active muscle legend.")]
    public bool showMuscleLegend = true;

    [Tooltip("Draw the exercise label in the overlay.")]
    public bool showExerciseLabel = true;

    [Tooltip("Show live rehab metrics (activation/symmetry/control) in legend.")]
    public bool showBiofeedbackMetrics = true;

    [Tooltip("How quickly activation intensities respond to movement.")]
    [Range(1f, 18f)]
    public float activationSmoothing = 8f;

    [Tooltip("Show legacy line overlay. Turn this off for clean anatomy-style highlighting.")]
    public bool showLineOverlay = false;

    [Tooltip("Show mesh-based body-region highlights on limbs and torso.")]
    public bool showMeshOverlay = false;

    [Tooltip("Make the avatar material itself glow in engaged anatomical regions.")]
    public bool useBodyMaterialGlow = true;

    [Tooltip("Emergency kill switch: keep original avatar material and disable all tint overlays.")]
    public bool disableAllTintOverlays = false;

    [Tooltip("Optional explicit target SkinnedMeshRenderer. If empty, auto-finds one under avatarRoot.")]
    public SkinnedMeshRenderer targetBodyRenderer;

    [Tooltip("Forward offset from body surface for mesh region highlights.")]
    [Range(0.005f, 0.2f)]
    public float meshOverlayOffset = 0.03f;

    [Tooltip("Global opacity for mesh region highlights.")]
    [Range(0.05f, 1f)]
    public float meshOverlayOpacity = 0.32f;

    [Tooltip("Global strength of in-material glow.")]
    [Range(0.1f, 4f)]
    public float bodyGlowStrength = 1.15f;

    [Tooltip("Overall opacity of body glow effect.")]
    [Range(0.05f, 1f)]
    public float bodyGlowOpacity = 0.75f;

    [Header("Movement Highlight")]
    [Tooltip("If enabled, regions turn green when they are actively moving during an exercise.")]
    public bool movementDrivenHighlight = true;

    [Tooltip("Minimum local bone movement speed to mark a region as active.")]
    [Range(0.0005f, 0.08f)]
    public float movementActivationThreshold = 0.0035f;

    [Tooltip("Small movement noise floor removed before activation (helps suppress webcam jitter).")]
    [Range(0.0001f, 0.04f)]
    public float movementNoiseFloor = 0.002f;

    [Tooltip("How quickly motion scores settle. Higher is more responsive.")]
    [Range(2f, 25f)]
    public float movementResponse = 15f;

    [Header("Colors")]
    [Tooltip("Primary highlight color for active muscles.")]
    public Color activeColor = new Color(0.18f, 0.95f, 0.33f, 0.95f);

    [Tooltip("Secondary highlight color for supporting muscles.")]
    public Color supportColor = new Color(0.98f, 0.76f, 0.28f, 0.82f);

    [Tooltip("Inactive overlay color for regions not emphasized by the current exercise.")]
    public Color inactiveRegionColor = new Color(0.50f, 0.54f, 0.60f, 0.28f);

    [Tooltip("Color for the line overlay outline.")]
    public Color outlineColor = new Color(0f, 0f, 0f, 0.65f);

    // ─────────────────────────────────────────────────────────────────────
    private readonly Dictionary<BodyRegion, LineRenderer> _regionLines = new Dictionary<BodyRegion, LineRenderer>();
    private float _currentFormQuality = 100f;
    private string _currentExercise = "unknown";
    private GUIStyle _legendTitleStyle;
    private GUIStyle _legendBodyStyle;
    private GUIStyle _legendNoteStyle;
    private GUIStyle _legendLabelStyle;
    private Transform _overlayAnchor;
    private bool _dirtyBoneBinding = true;
    private readonly Dictionary<BodyRegion, float> _dynamicRegionIntensity = new Dictionary<BodyRegion, float>();
    private float _metricActivation;
    private float _metricSymmetry = 100f;
    private float _metricControl = 100f;
    private readonly Dictionary<BodyRegion, Vector3> _previousBonePositions = new Dictionary<BodyRegion, Vector3>();
    private readonly Dictionary<BodyRegion, float> _movementScores = new Dictionary<BodyRegion, float>();
    private Vector3 _previousMotionReference;
    private bool _hasPreviousMotionReference;
    private readonly Dictionary<BodyRegion, Transform> _regionMeshes = new Dictionary<BodyRegion, Transform>();
    private readonly Dictionary<BodyRegion, Renderer> _regionMeshRenderers = new Dictionary<BodyRegion, Renderer>();
    private Material _runtimeBodyGlowMaterial;
    private Material _originalBodyMaterial;
    private SkinnedMeshRenderer _bodyGlowShellRenderer;
    private Transform _bodyGlowShellObject;
    private static readonly int ZoneCountId = Shader.PropertyToID("_ZoneCount");
    private static readonly int ZoneCentersId = Shader.PropertyToID("_ZoneCenters");
    private static readonly int ZoneParamsId = Shader.PropertyToID("_ZoneParams");
    private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowOpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int ShellOffsetId = Shader.PropertyToID("_ShellOffset");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private const int MaxGlowZones = 24;

    private readonly Color _panelColor = new Color(0f, 0f, 0f, 0.48f);
    private readonly Color _textColor = new Color(1f, 1f, 1f, 0.95f);
    private readonly Color _primaryColor = new Color(0.28f, 0.96f, 0.56f, 0.95f);
    private readonly Color _secondaryColor = new Color(0.98f, 0.76f, 0.28f, 0.9f);
    private readonly Color _inactiveColor = new Color(0.55f, 0.58f, 0.62f, 0.55f);

    private readonly Dictionary<string, ExerciseProfile> _exerciseProfiles = new Dictionary<string, ExerciseProfile>(StringComparer.OrdinalIgnoreCase)
    {
        ["squat"] = new ExerciseProfile(
            "Squat",
            "Primary focus: lower body and trunk stability",
            new Color(0.30f, 0.80f, 0.95f, 1f),
            new[]
            {
                new MuscleGroup("Quadriceps", "Knee extension", new Color(0.33f, 0.95f, 0.58f, 0.95f), true),
                new MuscleGroup("Glutes", "Hip extension", new Color(0.26f, 0.82f, 0.52f, 0.95f), true),
                new MuscleGroup("Hamstrings", "Hip + knee control", new Color(0.98f, 0.77f, 0.26f, 0.90f), false),
                new MuscleGroup("Core", "Trunk stability", new Color(0.56f, 0.71f, 0.98f, 0.85f), false)
            },
            new Dictionary<BodyRegion, float>
            {
                [BodyRegion.Torso] = 0.70f,
                [BodyRegion.Core] = 1.00f,
                [BodyRegion.LeftThigh] = 1.00f,
                [BodyRegion.RightThigh] = 1.00f,
                [BodyRegion.LeftCalf] = 0.82f,
                [BodyRegion.RightCalf] = 0.82f
            }
        ),
        ["hip"] = new ExerciseProfile(
            "Hip Flexion",
            "Primary focus: hip flexors and pelvic control",
            new Color(0.95f, 0.62f, 0.33f, 1f),
            new[]
            {
                new MuscleGroup("Hip Flexors", "Leg lift initiation", new Color(0.33f, 0.95f, 0.58f, 0.95f), true),
                new MuscleGroup("Rectus Femoris", "Assistive knee/hip lift", new Color(0.98f, 0.77f, 0.26f, 0.90f), false),
                new MuscleGroup("Glutes", "Pelvic control", new Color(0.56f, 0.71f, 0.98f, 0.85f), false),
                new MuscleGroup("Core", "Balance and stability", new Color(0.77f, 0.60f, 0.98f, 0.85f), false)
            },
            new Dictionary<BodyRegion, float>
            {
                [BodyRegion.Core] = 1.00f,
                [BodyRegion.Torso] = 0.72f,
                [BodyRegion.LeftThigh] = 1.00f,
                [BodyRegion.LeftCalf] = 0.46f,
                [BodyRegion.RightThigh] = 0.38f,
                [BodyRegion.RightCalf] = 0.22f,
                [BodyRegion.LeftUpperArm] = 0.20f,
                [BodyRegion.RightUpperArm] = 0.20f
            }
        ),
        ["shoulder"] = new ExerciseProfile(
            "Lateral Shoulder Raise",
            "Primary focus: shoulder girdle and upper arm control",
            new Color(0.84f, 0.55f, 0.95f, 1f),
            new[]
            {
                new MuscleGroup("Deltoids", "Arm abduction", new Color(0.33f, 0.95f, 0.58f, 0.95f), true),
                new MuscleGroup("Supraspinatus", "Start of the lift", new Color(0.98f, 0.77f, 0.26f, 0.90f), true),
                new MuscleGroup("Upper Traps", "Shoulder support", new Color(0.56f, 0.71f, 0.98f, 0.85f), false),
                new MuscleGroup("Core", "Postural stability", new Color(0.77f, 0.60f, 0.98f, 0.85f), false)
            },
            new Dictionary<BodyRegion, float>
            {
                [BodyRegion.LeftUpperArm] = 1.00f,
                [BodyRegion.RightUpperArm] = 1.00f,
                [BodyRegion.Torso] = 0.60f,
                [BodyRegion.Core] = 0.50f,
                [BodyRegion.Head] = 0.10f,
                [BodyRegion.LeftForearm] = 0.62f,
                [BodyRegion.RightForearm] = 0.62f
            }
        )
    };

    // ─────────────────────────────────────────────────────────────────────
    void Start()
    {
        if (disableAllTintOverlays)
        {
            // Immediate safety path: preserve native avatar look and remove all tint overlays.
            useBodyMaterialGlow = false;
            showMeshOverlay = false;
            showLineOverlay = false;
        }

        if (udpReceiver == null)
        {
            udpReceiver = FindFirstObjectByType<UDPReceiver>();
        }

        if (dashboardReceiver == null)
        {
            dashboardReceiver = FindFirstObjectByType<VRDashboardReceiver>();
        }

        if (avatarRoot == null)
        {
            avatarRoot = FindAvatarRootInParentsOrSelf();
        }

        _overlayAnchor = avatarRoot;
        TryAutoBindBones();
        SetupWorldOverlayObjects();
        SetupRegionMeshOverlayObjects();
        SetupBodyGlowMaterial();
        InitializeDynamicIntensity();
        InitializeMovementTracking();
        CreateStyles();

        Debug.Log($"[SkeletonOverlay] Initialized. avatarRoot={(avatarRoot != null ? avatarRoot.name : "null")}, headBone={(headBone != null ? headBone.name : "null")}, torsoBone={(torsoBone != null ? torsoBone.name : "null")}");
    }

    private void OnEnable()
    {
        if (dashboardReceiver != null)
        {
            dashboardReceiver.OnDashboardPacket -= HandleDashboardPacket;
            dashboardReceiver.OnDashboardPacket += HandleDashboardPacket;
        }
    }

    private void OnDisable()
    {
        if (dashboardReceiver != null)
        {
            dashboardReceiver.OnDashboardPacket -= HandleDashboardPacket;
        }
    }

    void OnDestroy()
    {
        foreach (var line in _regionLines.Values)
        {
            if (line != null)
            {
                Destroy(line.gameObject);
            }
        }

        foreach (var mesh in _regionMeshes.Values)
        {
            if (mesh != null)
            {
                Destroy(mesh.gameObject);
            }
        }

        if (_bodyGlowShellObject != null)
        {
            Destroy(_bodyGlowShellObject.gameObject);
        }

        if (_runtimeBodyGlowMaterial != null)
        {
            Destroy(_runtimeBodyGlowMaterial);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (overlayMode == OverlayMode.ScreenLegendOnly)
        {
            EnsureStyles();
            DrawLegendOnlyOverlay();
            return;
        }

        EnsureStyles();
        DrawLegendOnlyOverlay();
    }

    // ─────────────────────────────────────────────────────────────────────
    void LateUpdate()
    {
        if (overlayMode != OverlayMode.WorldBodyOverlay)
            return;

        if (avatarRoot == null)
            return;

        UpdateMovementTracking();

        UpdateWorldOverlay();
    }

    // ─────────────────────────────────────────────────────────────────────
    private void DrawLegendOnlyOverlay()
    {
        ExerciseProfile profile = GetCurrentProfile();
        Vector2 cornerPos = GetLegendCornerPosition();

        // Draw semi-transparent background panel
        GUI.backgroundColor = _panelColor;
        GUI.Box(new Rect(cornerPos.x - 150f, cornerPos.y - 175f, 250f, 290f), "");
        GUI.backgroundColor = Color.white;

        if (showExerciseLabel)
        {
            GUI.color = profile.bodyAccent;
            GUI.Label(new Rect(cornerPos.x - 140f, cornerPos.y - 165f, 220f, 24f),
                $"Exercise: {profile.name}",
                _legendTitleStyle ?? GUI.skin.label);
            GUI.color = _textColor;
            GUI.Label(new Rect(cornerPos.x - 140f, cornerPos.y - 145f, 220f, 18f),
                profile.note,
                _legendNoteStyle ?? GUI.skin.label);
            GUI.color = Color.white;
        }

        // Draw form quality score if enabled
        if (showFormScore)
        {
            GUI.color = GetColorByQuality(_currentFormQuality);
            GUI.Label(new Rect(cornerPos.x - 140f, cornerPos.y - 120f, 140f, 25f),
                $"Form: {Mathf.RoundToInt(_currentFormQuality)}%",
                _legendBodyStyle ?? GUI.skin.label);
            GUI.color = Color.white;
        }

        if (showBiofeedbackMetrics)
        {
            DrawBiofeedbackMetrics(cornerPos);
        }

        if (showMuscleLegend)
        {
            DrawMuscleLegend(cornerPos, profile);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private void SetupWorldOverlayObjects()
    {
        foreach (BodyRegion region in Enum.GetValues(typeof(BodyRegion)))
        {
            if (_regionLines.ContainsKey(region)) continue;

            GameObject lineObject = new GameObject($"{region}_OverlayLine");
            lineObject.transform.SetParent(avatarRoot, false);
            lineObject.layer = avatarRoot.gameObject.layer;

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 6;
            line.numCornerVertices = 6;
            line.widthMultiplier = overlayLineWidth;
            Shader overlayShader = Shader.Find("Pose2Play/OverlayLine");
            if (overlayShader == null)
                overlayShader = Shader.Find("Unlit/Color");

            line.material = new Material(overlayShader);
            line.sortingOrder = 5000;
            _regionLines[region] = line;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private void SetupRegionMeshOverlayObjects()
    {
        foreach (BodyRegion region in Enum.GetValues(typeof(BodyRegion)))
        {
            if (_regionMeshes.ContainsKey(region))
                continue;

            PrimitiveType primitive = region == BodyRegion.Head || region == BodyRegion.Core ? PrimitiveType.Sphere : PrimitiveType.Capsule;
            GameObject obj = GameObject.CreatePrimitive(primitive);
            obj.name = $"{region}_MuscleMesh";
            obj.transform.SetParent(avatarRoot, false);
            obj.layer = avatarRoot.gameObject.layer;

            Collider col = obj.GetComponent<Collider>();
            if (col != null)
                Destroy(col);

            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Pose2Play/OverlayLine");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");

                renderer.material = new Material(shader);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _regionMeshes[region] = obj.transform;
            _regionMeshRenderers[region] = renderer;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private void SetupBodyGlowMaterial()
    {
        if (!useBodyMaterialGlow)
            return;

        if (targetBodyRenderer == null && avatarRoot != null)
        {
            targetBodyRenderer = AutoSelectBestBodyRenderer(avatarRoot);
        }

        if (targetBodyRenderer == null)
            return;

        Shader shader = Shader.Find("Pose2Play/MuscleZoneShell");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("[SkeletonOverlay] Shader Pose2Play/MuscleZoneShell missing/unsupported. Body glow disabled.");
            useBodyMaterialGlow = false;
            return;
        }

        _runtimeBodyGlowMaterial = new Material(shader);

        _runtimeBodyGlowMaterial.SetColor(GlowColorId, activeColor);
        _runtimeBodyGlowMaterial.SetFloat(GlowStrengthId, bodyGlowStrength);
        _runtimeBodyGlowMaterial.SetFloat(GlowOpacityId, bodyGlowOpacity);
        _runtimeBodyGlowMaterial.SetFloat(ShellOffsetId, 0.004f);

        GameObject shellObject = new GameObject("BodyGlowShell");
        shellObject.transform.SetParent(targetBodyRenderer.transform, false);
        shellObject.layer = targetBodyRenderer.gameObject.layer;

        _bodyGlowShellRenderer = shellObject.AddComponent<SkinnedMeshRenderer>();
        _bodyGlowShellRenderer.sharedMesh = targetBodyRenderer.sharedMesh;
        _bodyGlowShellRenderer.rootBone = targetBodyRenderer.rootBone;
        _bodyGlowShellRenderer.bones = targetBodyRenderer.bones;
        _bodyGlowShellRenderer.updateWhenOffscreen = true;
        _bodyGlowShellRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _bodyGlowShellRenderer.receiveShadows = false;
        _bodyGlowShellRenderer.sharedMaterial = _runtimeBodyGlowMaterial;
        _bodyGlowShellRenderer.localBounds = targetBodyRenderer.localBounds;
        _bodyGlowShellRenderer.enabled = false;

        _bodyGlowShellObject = shellObject.transform;

        Debug.Log($"[SkeletonOverlay] Body glow renderer: {targetBodyRenderer.name}");
    }

    // ─────────────────────────────────────────────────────────────────────
    private SkinnedMeshRenderer AutoSelectBestBodyRenderer(Transform root)
    {
        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        SkinnedMeshRenderer best = null;
        int bestScore = int.MinValue;

        foreach (SkinnedMeshRenderer r in renderers)
        {
            if (r == null || r.sharedMesh == null)
                continue;

            string n = r.name.ToLowerInvariant();

            // Hard reject helper/joint meshes.
            if (n.Contains("joint") || n.Contains("rig") || n.Contains("helper") || n.Contains("target"))
                continue;

            int score = 0;

            if (n.Contains("surface")) score += 120;
            if (n.Contains("body")) score += 100;
            if (n.Contains("skin")) score += 100;
            if (n.Contains("beta_surface")) score += 180;
            if (n.Contains("mesh")) score += 20;

            // Prefer higher vertex count when naming is ambiguous.
            score += Mathf.Min(r.sharedMesh.vertexCount / 100, 200);

            if (score > bestScore)
            {
                best = r;
                bestScore = score;
            }
        }

        // Fallback to the largest renderer if all names were ambiguous.
        if (best == null)
        {
            int maxVerts = -1;
            foreach (SkinnedMeshRenderer r in renderers)
            {
                if (r == null || r.sharedMesh == null)
                    continue;

                int verts = r.sharedMesh.vertexCount;
                if (verts > maxVerts)
                {
                    maxVerts = verts;
                    best = r;
                }
            }
        }

        return best;
    }

    // ─────────────────────────────────────────────────────────────────────
    private void UpdateBodyGlowMaterial(ExerciseProfile profile)
    {
        if (_runtimeBodyGlowMaterial == null || _bodyGlowShellRenderer == null)
            return;

        Vector4[] centers = new Vector4[MaxGlowZones];
        Vector4[] parameters = new Vector4[MaxGlowZones];
        int count = 0;

        foreach (BodyRegion region in Enum.GetValues(typeof(BodyRegion)))
        {
            if (count >= MaxGlowZones)
                break;

            Transform startBone = GetRegionStartBone(region);
            if (startBone == null)
                continue;

            Transform endBone = GetRegionEndBone(region);

            float intensity = 0f;

            if (movementDrivenHighlight)
            {
                float movement = _movementScores.TryGetValue(region, out float score) ? score : 0f;
                float normalized = Mathf.Clamp01((movement - movementActivationThreshold) / (movementActivationThreshold * 4f));
                bool relevant = IsRegionRelevant(profile, region, 0.35f);

                intensity = relevant ? Mathf.Pow(normalized, 0.55f) : 0f;
            }
            else if (_dynamicRegionIntensity.TryGetValue(region, out float dynamicIntensity))
            {
                intensity = dynamicIntensity;
            }

            if (intensity <= 0.015f)
                continue;

            Vector3 forwardOffset = (avatarRoot != null ? avatarRoot.forward : Vector3.forward) * 0.01f;
            float radius = GetGlowRadius(region);
            float scaledIntensity = Mathf.Clamp01(intensity) * bodyGlowOpacity;

            // Sample along the segment to avoid isolated oval blobs and create smooth anatomical lighting.
            if (endBone != null)
            {
                Vector3 a = startBone.position + forwardOffset;
                Vector3 b = endBone.position + forwardOffset;

                Vector3 c0 = Vector3.Lerp(a, b, 0.2f);
                Vector3 c1 = Vector3.Lerp(a, b, 0.5f);
                Vector3 c2 = Vector3.Lerp(a, b, 0.8f);

                AppendGlowSample(centers, parameters, ref count, c0, radius * 1.05f, scaledIntensity * 0.75f);
                AppendGlowSample(centers, parameters, ref count, c1, radius * 1.15f, scaledIntensity);
                AppendGlowSample(centers, parameters, ref count, c2, radius * 1.05f, scaledIntensity * 0.75f);
            }
            else
            {
                Vector3 c = startBone.position + forwardOffset;
                AppendGlowSample(centers, parameters, ref count, c, radius * 1.2f, scaledIntensity);
            }
        }

        _runtimeBodyGlowMaterial.SetInt(ZoneCountId, count);
        _runtimeBodyGlowMaterial.SetVectorArray(ZoneCentersId, centers);
        _runtimeBodyGlowMaterial.SetVectorArray(ZoneParamsId, parameters);
        _runtimeBodyGlowMaterial.SetColor(GlowColorId, activeColor);
        _runtimeBodyGlowMaterial.SetFloat(GlowStrengthId, bodyGlowStrength);
        _runtimeBodyGlowMaterial.SetFloat(GlowOpacityId, bodyGlowOpacity);

        _bodyGlowShellRenderer.enabled = count > 0;
    }

    // ─────────────────────────────────────────────────────────────────────
    private void AppendGlowSample(Vector4[] centers, Vector4[] parameters, ref int count, Vector3 center, float radius, float intensity)
    {
        if (count >= MaxGlowZones)
            return;

        centers[count] = new Vector4(center.x, center.y, center.z, 1f);
        parameters[count] = new Vector4(Mathf.Max(radius, 0.001f), Mathf.Clamp01(intensity), 0f, 0f);
        count++;
    }

    // ─────────────────────────────────────────────────────────────────────
    private float GetGlowRadius(BodyRegion region)
    {
        switch (region)
        {
            case BodyRegion.Head: return 0.10f;
            case BodyRegion.Torso: return 0.19f;
            case BodyRegion.Core: return 0.16f;
            case BodyRegion.LeftUpperArm:
            case BodyRegion.RightUpperArm: return 0.13f;
            case BodyRegion.LeftForearm:
            case BodyRegion.RightForearm: return 0.11f;
            case BodyRegion.LeftThigh:
            case BodyRegion.RightThigh: return 0.15f;
            case BodyRegion.LeftCalf:
            case BodyRegion.RightCalf: return 0.12f;
            default: return 0.12f;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private void UpdateRegionMeshOverlay(ExerciseProfile profile)
    {
        Vector3 offsetDir = avatarRoot != null ? avatarRoot.forward : Vector3.forward;

        foreach (BodyRegion region in Enum.GetValues(typeof(BodyRegion)))
        {
            Transform startBone = GetRegionStartBone(region);
            Transform endBone = GetRegionEndBone(region);

            if (!_regionMeshes.TryGetValue(region, out Transform regionMesh) || regionMesh == null)
                continue;

            if (!_regionMeshRenderers.TryGetValue(region, out Renderer renderer) || renderer == null)
                continue;

            if (startBone == null)
            {
                renderer.enabled = false;
                continue;
            }

            renderer.enabled = true;

            Vector3 start = startBone.position;
            Vector3 end = endBone != null ? endBone.position : start + GetRegionDirection(region) * bodyOverlayScale;
            Vector3 center = (start + end) * 0.5f + offsetDir * meshOverlayOffset;
            Vector3 dir = (end - start);

            float radius = GetRegionRadius(region);
            if (dir.sqrMagnitude > 0.000001f)
            {
                float length = dir.magnitude;
                regionMesh.position = center;
                regionMesh.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                float height = Mathf.Max(length, radius * 2f);
                regionMesh.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            }
            else
            {
                regionMesh.position = start + offsetDir * meshOverlayOffset;
                regionMesh.rotation = Quaternion.identity;
                float diameter = radius * 2f;
                regionMesh.localScale = new Vector3(diameter, diameter, diameter);
            }

            Color c = GetRegionColor(region, profile);
            c.a *= meshOverlayOpacity;
            renderer.material.color = c;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private Transform GetRegionEndBone(BodyRegion region)
    {
        switch (region)
        {
            case BodyRegion.LeftUpperArm: return leftForearm;
            case BodyRegion.LeftForearm: return null;
            case BodyRegion.RightUpperArm: return rightForearm;
            case BodyRegion.RightForearm: return null;
            case BodyRegion.LeftThigh: return leftCalf;
            case BodyRegion.LeftCalf: return null;
            case BodyRegion.RightThigh: return rightCalf;
            case BodyRegion.RightCalf: return null;
            case BodyRegion.Torso: return torsoBone;
            case BodyRegion.Core: return null;
            case BodyRegion.Head: return null;
            default: return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private float GetRegionRadius(BodyRegion region)
    {
        switch (region)
        {
            case BodyRegion.Head: return 0.08f;
            case BodyRegion.Torso: return 0.11f;
            case BodyRegion.Core: return 0.10f;
            case BodyRegion.LeftUpperArm:
            case BodyRegion.RightUpperArm: return 0.07f;
            case BodyRegion.LeftForearm:
            case BodyRegion.RightForearm: return 0.06f;
            case BodyRegion.LeftThigh:
            case BodyRegion.RightThigh: return 0.09f;
            case BodyRegion.LeftCalf:
            case BodyRegion.RightCalf: return 0.075f;
            default: return 0.07f;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private void SetLineOverlayEnabled(bool enabled)
    {
        foreach (var kvp in _regionLines)
        {
            if (kvp.Value != null)
            {
                kvp.Value.enabled = enabled;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private void SetMeshOverlayEnabled(bool enabled)
    {
        foreach (var kvp in _regionMeshRenderers)
        {
            if (kvp.Value != null)
            {
                kvp.Value.enabled = enabled;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private void UpdateWorldOverlay()
    {
        if (_dirtyBoneBinding)
        {
            TryAutoBindBones();
            _dirtyBoneBinding = false;
        }

        if (headBone == null && torsoBone == null && leftUpperArm == null && rightUpperArm == null && leftThigh == null && rightThigh == null)
        {
            Debug.LogWarning("[SkeletonOverlay] No avatar bones were bound. Check avatarRoot and bone names in the Inspector.");
            return;
        }

        ExerciseProfile profile = GetCurrentProfile();
        UpdateDynamicActivation(profile);

        // In body-glow mode, fully suppress legacy overlays to avoid blob artifacts.
        if (useBodyMaterialGlow)
        {
            SetLineOverlayEnabled(false);
            SetMeshOverlayEnabled(false);
        }

        if (showLineOverlay && !useBodyMaterialGlow)
        {
            UpdateRegionLine(BodyRegion.Head, headBone, profile);
            UpdateRegionLine(BodyRegion.Torso, torsoBone, profile);
            UpdateRegionLine(BodyRegion.Core, torsoBone != null ? torsoBone : avatarRoot, profile);

            UpdateRegionLine(BodyRegion.LeftUpperArm, leftUpperArm, profile, leftForearm);
            UpdateRegionLine(BodyRegion.LeftForearm, leftForearm, profile, leftHandFallback: true);
            UpdateRegionLine(BodyRegion.RightUpperArm, rightUpperArm, profile, rightForearm);
            UpdateRegionLine(BodyRegion.RightForearm, rightForearm, profile, leftHandFallback: true);
            UpdateRegionLine(BodyRegion.LeftThigh, leftThigh, profile, leftCalf);
            UpdateRegionLine(BodyRegion.LeftCalf, leftCalf, profile, leftHandFallback: true);
            UpdateRegionLine(BodyRegion.RightThigh, rightThigh, profile, rightCalf);
            UpdateRegionLine(BodyRegion.RightCalf, rightCalf, profile, leftHandFallback: true);
        }
        else
        {
            SetLineOverlayEnabled(false);
        }

        if (showMeshOverlay && !useBodyMaterialGlow)
        {
            UpdateRegionMeshOverlay(profile);
        }
        else
        {
            SetMeshOverlayEnabled(false);
        }

        if (useBodyMaterialGlow)
        {
            UpdateBodyGlowMaterial(profile);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private void UpdateRegionLine(BodyRegion region, Transform startBone, ExerciseProfile profile, Transform endBone = null, bool leftHandFallback = false)
    {
        if (!_regionLines.TryGetValue(region, out LineRenderer line) || line == null)
            return;

        if (startBone == null)
        {
            line.enabled = false;
            return;
        }

        Vector3 start = startBone.position;
        Vector3 end = endBone != null ? endBone.position : startBone.position + GetRegionDirection(region) * bodyOverlayScale;

        if (leftHandFallback)
        {
            end += GetRegionDirection(region) * bodyOverlayScale;
        }

        Vector3 offset = GetRegionDirection(region) * overlayOffset;
        start += offset;
        end += offset;

        line.enabled = true;
        line.startWidth = overlayLineWidth;
        line.endWidth = overlayLineWidth;
        line.sortingOrder = 5000;
        line.SetPosition(0, start);
        line.SetPosition(1, end);

        Color regionColor = GetRegionColor(region, profile);
        line.startColor = regionColor;
        line.endColor = regionColor;
    }

    // ─────────────────────────────────────────────────────────────────────
    private Vector3 GetRegionDirection(BodyRegion region)
    {
        switch (region)
        {
            case BodyRegion.Head: return Vector3.up * 0.22f;
            case BodyRegion.Torso: return Vector3.up * 0.16f;
            case BodyRegion.Core: return Vector3.forward * 0.08f;
            case BodyRegion.LeftUpperArm: return Vector3.left * 0.12f;
            case BodyRegion.LeftForearm: return Vector3.left * 0.10f;
            case BodyRegion.RightUpperArm: return Vector3.right * 0.12f;
            case BodyRegion.RightForearm: return Vector3.right * 0.10f;
            case BodyRegion.LeftThigh: return Vector3.down * 0.18f + Vector3.left * 0.04f;
            case BodyRegion.LeftCalf: return Vector3.down * 0.16f + Vector3.left * 0.03f;
            case BodyRegion.RightThigh: return Vector3.down * 0.18f + Vector3.right * 0.04f;
            case BodyRegion.RightCalf: return Vector3.down * 0.16f + Vector3.right * 0.03f;
            default: return Vector3.forward * 0.05f;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private Color GetRegionColor(BodyRegion region, ExerciseProfile profile)
    {
        if (movementDrivenHighlight)
        {
            float movement = _movementScores.TryGetValue(region, out float score) ? score : 0f;
            bool isRelevant = IsRegionRelevant(profile, region, 0.35f);

            if (isRelevant && movement >= movementActivationThreshold)
            {
                Color c = activeColor;
                c.a = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(movement / (movementActivationThreshold * 3f)));
                return c;
            }

            if (isRelevant)
            {
                Color c = supportColor;
                c.a = 0.35f;
                return c;
            }

            Color inactive = inactiveRegionColor;
            inactive.a = 0.16f;
            return inactive;
        }

        float intensity = 0f;

        if (_dynamicRegionIntensity.TryGetValue(region, out float dynamicIntensity))
        {
            intensity = dynamicIntensity;
        }
        else if (profile.regionIntensity != null && profile.regionIntensity.TryGetValue(region, out float baseIntensity))
        {
            intensity = baseIntensity;
        }

        float qualityScale = Mathf.Clamp01((_currentFormQuality / 100f) * 0.5f + 0.5f);

        if (intensity >= 0.85f)
        {
            Color c = activeColor;
            c.a *= qualityScale;
            return c;
        }

        if (intensity >= 0.50f)
        {
            Color c = supportColor;
            c.a *= qualityScale;
            return c;
        }

        return inactiveRegionColor;
    }

    // ─────────────────────────────────────────────────────────────────────
    private bool IsRegionRelevant(ExerciseProfile profile, BodyRegion region, float minimumRelevance)
    {
        if (profile == null || profile.regionIntensity == null)
            return false;

        if (IsRegionSuppressedForExercise(profile, region))
            return false;

        return profile.regionIntensity.TryGetValue(region, out float relevance) && relevance >= minimumRelevance;
    }

    // ─────────────────────────────────────────────────────────────────────
    private bool IsRegionSuppressedForExercise(ExerciseProfile profile, BodyRegion region)
    {
        if (profile == null)
            return false;

        // During squats, arm glow is misleading. Keep highlights focused on lower body + trunk.
        if (string.Equals(profile.name, "Squat", StringComparison.OrdinalIgnoreCase))
        {
            if (region == BodyRegion.LeftUpperArm || region == BodyRegion.RightUpperArm ||
                region == BodyRegion.LeftForearm || region == BodyRegion.RightForearm)
            {
                return true;
            }
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────────────
    private void InitializeDynamicIntensity()
    {
        _dynamicRegionIntensity.Clear();

        foreach (BodyRegion region in Enum.GetValues(typeof(BodyRegion)))
        {
            _dynamicRegionIntensity[region] = 0f;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private void InitializeMovementTracking()
    {
        _previousBonePositions.Clear();
        _movementScores.Clear();
        _hasPreviousMotionReference = false;

        foreach (BodyRegion region in Enum.GetValues(typeof(BodyRegion)))
        {
            _movementScores[region] = 0f;
            Transform bone = GetRegionStartBone(region);
            if (bone != null)
            {
                _previousBonePositions[region] = bone.position;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private void UpdateMovementTracking()
    {
        if (_movementScores.Count == 0)
        {
            InitializeMovementTracking();
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 motionReference = GetMotionReferencePoint();
        Vector3 referenceVelocity = Vector3.zero;

        if (_hasPreviousMotionReference)
        {
            referenceVelocity = (motionReference - _previousMotionReference) / dt;
        }

        _previousMotionReference = motionReference;
        _hasPreviousMotionReference = true;

        foreach (BodyRegion region in Enum.GetValues(typeof(BodyRegion)))
        {
            if (!TryGetRegionMotionSample(region, out Vector3 motionCenter, out float motionScale))
            {
                _movementScores[region] = Mathf.Lerp(_movementScores.TryGetValue(region, out float currentMissing) ? currentMissing : 0f, 0f, dt * movementResponse);
                continue;
            }

            Vector3 prevPos = _previousBonePositions.TryGetValue(region, out Vector3 cached) ? cached : motionCenter;
            Vector3 rawVelocity = (motionCenter - prevPos) / dt;
            Vector3 localVelocity = rawVelocity - referenceVelocity * GetReferenceCompensation(region);
            float speed = Mathf.Max(0f, localVelocity.magnitude - movementNoiseFloor) * motionScale;

            float existing = _movementScores.TryGetValue(region, out float e) ? e : 0f;
            _movementScores[region] = Mathf.Lerp(existing, speed, dt * movementResponse);
            _previousBonePositions[region] = motionCenter;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private Vector3 GetMotionReferencePoint()
    {
        if (torsoBone != null)
            return torsoBone.position;

        if (avatarRoot != null)
            return avatarRoot.position;

        return transform.position;
    }

    // ─────────────────────────────────────────────────────────────────────
    private float GetReferenceCompensation(BodyRegion region)
    {
        switch (region)
        {
            case BodyRegion.LeftUpperArm:
            case BodyRegion.LeftForearm:
            case BodyRegion.RightUpperArm:
            case BodyRegion.RightForearm:
            case BodyRegion.LeftThigh:
            case BodyRegion.LeftCalf:
            case BodyRegion.RightThigh:
            case BodyRegion.RightCalf:
                return 1f;
            case BodyRegion.Torso:
            case BodyRegion.Core:
                return 0.45f;
            case BodyRegion.Head:
                return 0.7f;
            default:
                return 1f;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private bool TryGetRegionMotionSample(BodyRegion region, out Vector3 center, out float motionScale)
    {
        center = Vector3.zero;
        motionScale = 1f;

        Transform start = GetRegionStartBone(region);
        Transform end = GetRegionEndBone(region);

        if (start == null)
            return false;

        if (end != null)
        {
            center = (start.position + end.position) * 0.5f;
        }
        else
        {
            center = start.position;
        }

        switch (region)
        {
            case BodyRegion.LeftUpperArm:
            case BodyRegion.RightUpperArm:
                motionScale = 0.90f;
                break;
            case BodyRegion.LeftForearm:
            case BodyRegion.RightForearm:
                motionScale = 0.85f;
                break;
            case BodyRegion.LeftThigh:
            case BodyRegion.RightThigh:
                motionScale = 0.85f;
                break;
            case BodyRegion.LeftCalf:
            case BodyRegion.RightCalf:
                motionScale = 0.75f;
                break;
            case BodyRegion.Torso:
            case BodyRegion.Core:
                motionScale = 0.95f;
                break;
        }

        return true;
    }

    // ─────────────────────────────────────────────────────────────────────
    private Transform GetRegionStartBone(BodyRegion region)
    {
        switch (region)
        {
            case BodyRegion.Head: return headBone;
            case BodyRegion.Torso: return torsoBone;
            case BodyRegion.Core: return torsoBone != null ? torsoBone : avatarRoot;
            case BodyRegion.LeftUpperArm: return leftShoulderBone != null ? leftShoulderBone : leftUpperArm;
            case BodyRegion.LeftForearm: return leftForearm;
            case BodyRegion.RightUpperArm: return rightShoulderBone != null ? rightShoulderBone : rightUpperArm;
            case BodyRegion.RightForearm: return rightForearm;
            case BodyRegion.LeftThigh: return leftThigh;
            case BodyRegion.LeftCalf: return leftCalf;
            case BodyRegion.RightThigh: return rightThigh;
            case BodyRegion.RightCalf: return rightCalf;
            default: return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private void UpdateDynamicActivation(ExerciseProfile profile)
    {
        if (_dynamicRegionIntensity.Count == 0)
        {
            InitializeDynamicIntensity();
        }

        Dictionary<BodyRegion, float> targetIntensity = new Dictionary<BodyRegion, float>();
        foreach (BodyRegion region in Enum.GetValues(typeof(BodyRegion)))
        {
            float baseline = 0.15f;
            if (profile.regionIntensity != null && profile.regionIntensity.TryGetValue(region, out float baseValue))
            {
                baseline = Mathf.Clamp01(baseValue);
            }
            targetIntensity[region] = baseline;
        }

        float leftSignal = 0f;
        float rightSignal = 0f;

        if (udpReceiver != null && udpReceiver.pose != null)
        {
            var p = udpReceiver.pose;

            if (profile.name == "Squat")
            {
                float leftKnee = TryAngle(p.left_hip, p.left_knee, p.left_ankle, 170f);
                float rightKnee = TryAngle(p.right_hip, p.right_knee, p.right_ankle, 170f);

                leftSignal = Mathf.Clamp01(Mathf.InverseLerp(170f, 80f, leftKnee));
                rightSignal = Mathf.Clamp01(Mathf.InverseLerp(170f, 80f, rightKnee));
                float depth = Mathf.Max(leftSignal, rightSignal);

                targetIntensity[BodyRegion.LeftThigh] = Mathf.Max(targetIntensity[BodyRegion.LeftThigh], 0.55f + leftSignal * 0.45f);
                targetIntensity[BodyRegion.RightThigh] = Mathf.Max(targetIntensity[BodyRegion.RightThigh], 0.55f + rightSignal * 0.45f);
                targetIntensity[BodyRegion.LeftCalf] = Mathf.Max(targetIntensity[BodyRegion.LeftCalf], 0.45f + leftSignal * 0.45f);
                targetIntensity[BodyRegion.RightCalf] = Mathf.Max(targetIntensity[BodyRegion.RightCalf], 0.45f + rightSignal * 0.45f);
                targetIntensity[BodyRegion.Core] = Mathf.Max(targetIntensity[BodyRegion.Core], 0.45f + depth * 0.45f);
                targetIntensity[BodyRegion.Torso] = Mathf.Max(targetIntensity[BodyRegion.Torso], 0.35f + depth * 0.35f);
            }
            else if (profile.name == "Hip Flexion")
            {
                float leftHip = TryAngle(p.left_shoulder, p.left_hip, p.left_knee, 170f);
                float rightHip = TryAngle(p.right_shoulder, p.right_hip, p.right_knee, 170f);

                leftSignal = Mathf.Clamp01(Mathf.InverseLerp(170f, 80f, leftHip));
                rightSignal = Mathf.Clamp01(Mathf.InverseLerp(170f, 80f, rightHip));

                float dominant = Mathf.Max(leftSignal, rightSignal);
                targetIntensity[BodyRegion.LeftThigh] = Mathf.Max(targetIntensity[BodyRegion.LeftThigh], 0.28f + leftSignal * 0.72f);
                targetIntensity[BodyRegion.RightThigh] = Mathf.Max(targetIntensity[BodyRegion.RightThigh], 0.28f + rightSignal * 0.72f);
                targetIntensity[BodyRegion.LeftCalf] = Mathf.Max(targetIntensity[BodyRegion.LeftCalf], 0.18f + leftSignal * 0.42f);
                targetIntensity[BodyRegion.RightCalf] = Mathf.Max(targetIntensity[BodyRegion.RightCalf], 0.18f + rightSignal * 0.42f);
                targetIntensity[BodyRegion.Core] = Mathf.Max(targetIntensity[BodyRegion.Core], 0.42f + dominant * 0.50f);
                targetIntensity[BodyRegion.Torso] = Mathf.Max(targetIntensity[BodyRegion.Torso], 0.30f + dominant * 0.35f);
            }
            else if (profile.name == "Lateral Shoulder Raise")
            {
                float leftShoulder = TryAngle(p.left_hip, p.left_shoulder, p.left_elbow, 25f);
                float rightShoulder = TryAngle(p.right_hip, p.right_shoulder, p.right_elbow, 25f);

                leftSignal = Mathf.Clamp01(Mathf.InverseLerp(25f, 110f, leftShoulder));
                rightSignal = Mathf.Clamp01(Mathf.InverseLerp(25f, 110f, rightShoulder));

                float dominant = Mathf.Max(leftSignal, rightSignal);
                targetIntensity[BodyRegion.LeftUpperArm] = Mathf.Max(targetIntensity[BodyRegion.LeftUpperArm], 0.38f + leftSignal * 0.62f);
                targetIntensity[BodyRegion.RightUpperArm] = Mathf.Max(targetIntensity[BodyRegion.RightUpperArm], 0.38f + rightSignal * 0.62f);
                targetIntensity[BodyRegion.LeftForearm] = Mathf.Max(targetIntensity[BodyRegion.LeftForearm], 0.22f + leftSignal * 0.45f);
                targetIntensity[BodyRegion.RightForearm] = Mathf.Max(targetIntensity[BodyRegion.RightForearm], 0.22f + rightSignal * 0.45f);
                targetIntensity[BodyRegion.Torso] = Mathf.Max(targetIntensity[BodyRegion.Torso], 0.32f + dominant * 0.35f);
                targetIntensity[BodyRegion.Core] = Mathf.Max(targetIntensity[BodyRegion.Core], 0.25f + dominant * 0.30f);
            }
        }

        foreach (BodyRegion region in Enum.GetValues(typeof(BodyRegion)))
        {
            float current = _dynamicRegionIntensity.TryGetValue(region, out float v) ? v : 0f;
            float target = targetIntensity.TryGetValue(region, out float t) ? t : 0f;
            _dynamicRegionIntensity[region] = Mathf.Lerp(current, target, Time.deltaTime * activationSmoothing);
        }

        _metricActivation = Mathf.Clamp01(GetTopRegionValue()) * 100f;
        _metricSymmetry = 100f - Mathf.Clamp01(Mathf.Abs(leftSignal - rightSignal)) * 100f;
        _metricControl = _currentFormQuality;
    }

    // ─────────────────────────────────────────────────────────────────────
    private float GetTopRegionValue()
    {
        float top = 0f;
        foreach (float value in _dynamicRegionIntensity.Values)
        {
            if (value > top) top = value;
        }
        return top;
    }

    // ─────────────────────────────────────────────────────────────────────
    private float TryAngle(float[] a, float[] b, float[] c, float fallback)
    {
        if (!IsValidPoint(a) || !IsValidPoint(b) || !IsValidPoint(c))
            return fallback;

        Vector3 av = new Vector3(a[0], a[1], a[2]);
        Vector3 bv = new Vector3(b[0], b[1], b[2]);
        Vector3 cv = new Vector3(c[0], c[1], c[2]);

        Vector3 ba = (av - bv).normalized;
        Vector3 bc = (cv - bv).normalized;
        float dot = Mathf.Clamp(Vector3.Dot(ba, bc), -1f, 1f);
        return Mathf.Acos(dot) * Mathf.Rad2Deg;
    }

    // ─────────────────────────────────────────────────────────────────────
    private bool IsValidPoint(float[] p)
    {
        return p != null && p.Length >= 3;
    }

    // ─────────────────────────────────────────────────────────────────────
    private void DrawBiofeedbackMetrics(Vector2 cornerPos)
    {
        float x = cornerPos.x - 140f;
        float y = cornerPos.y - 98f;
        float width = 220f;

        DrawMetricRow(x, y, width, "Activation", _metricActivation, activeColor);
        DrawMetricRow(x, y + 18f, width, "Symmetry", _metricSymmetry, supportColor);
        DrawMetricRow(x, y + 36f, width, "Control", _metricControl, new Color(0.50f, 0.80f, 1f, 0.9f));
    }

    // ─────────────────────────────────────────────────────────────────────
    private void DrawMetricRow(float x, float y, float width, string label, float value, Color fill)
    {
        float clamped = Mathf.Clamp(value, 0f, 100f);
        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        GUI.Label(new Rect(x, y, 80f, 16f), $"{label}", _legendLabelStyle ?? GUI.skin.label);
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);
        GUI.Box(new Rect(x + 82f, y + 3f, width - 130f, 8f), GUIContent.none);
        GUI.color = fill;
        GUI.Box(new Rect(x + 82f, y + 3f, (width - 130f) * (clamped / 100f), 8f), GUIContent.none);
        GUI.color = _textColor;
        GUI.Label(new Rect(x + width - 44f, y, 44f, 16f), $"{Mathf.RoundToInt(clamped)}", _legendLabelStyle ?? GUI.skin.label);
        GUI.color = Color.white;
    }

    // ─────────────────────────────────────────────────────────────────────
    private void TryAutoBindBones()
    {
        if (avatarRoot == null)
            return;

        if (headBone == null) headBone = FindBone(avatarRoot, "Head");
        if (torsoBone == null) torsoBone = FindBone(avatarRoot, "Spine", "Chest", "UpperChest", "Hips");
        if (leftShoulderBone == null) leftShoulderBone = FindBone(avatarRoot, "LeftShoulder");
        if (leftUpperArm == null) leftUpperArm = FindBone(avatarRoot, "LeftArm", "LeftUpperArm", "LeftShoulder");
        if (leftForearm == null) leftForearm = FindBone(avatarRoot, "LeftForeArm", "LeftLowerArm", "LeftElbow");
        if (rightShoulderBone == null) rightShoulderBone = FindBone(avatarRoot, "RightShoulder");
        if (rightUpperArm == null) rightUpperArm = FindBone(avatarRoot, "RightArm", "RightUpperArm", "RightShoulder");
        if (rightForearm == null) rightForearm = FindBone(avatarRoot, "RightForeArm", "RightLowerArm", "RightElbow");
        if (leftThigh == null) leftThigh = FindBone(avatarRoot, "LeftUpLeg", "LeftThigh", "LeftHip");
        if (leftCalf == null) leftCalf = FindBone(avatarRoot, "LeftLeg", "LeftCalf", "LeftKnee");
        if (rightThigh == null) rightThigh = FindBone(avatarRoot, "RightUpLeg", "RightThigh", "RightHip");
        if (rightCalf == null) rightCalf = FindBone(avatarRoot, "RightLeg", "RightCalf", "RightKnee");
    }

    // ─────────────────────────────────────────────────────────────────────
    private Transform FindAvatarRootInParentsOrSelf()
    {
        Transform current = transform;

        while (current != null)
        {
            if (HasMixamoBones(current))
            {
                return current;
            }

            current = current.parent;
        }

        return transform;
    }

    // ─────────────────────────────────────────────────────────────────────
    private bool HasMixamoBones(Transform root)
    {
        if (root == null) return false;

        Transform hips = FindByNameRecursive(root, "Hips");
        Transform spine = FindByNameRecursive(root, "Spine");
        Transform leftArm = FindByNameRecursive(root, "LeftArm");

        return hips != null || spine != null || leftArm != null;
    }

    // ─────────────────────────────────────────────────────────────────────
    private Transform FindBone(Transform root, params string[] names)
    {
        foreach (string name in names)
        {
            Transform found = FindByNameRecursive(root, name);
            if (found != null) return found;
        }

        return null;
    }

    // ─────────────────────────────────────────────────────────────────────
    private Transform FindByNameRecursive(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                return child;
        }

        return null;
    }

    // ─────────────────────────────────────────────────────────────────────
    private void CreateStyles()
    {
        if (_legendTitleStyle != null && _legendBodyStyle != null && _legendNoteStyle != null && _legendLabelStyle != null)
            return;

        _legendTitleStyle = new GUIStyle()
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = _textColor }
        };

        _legendBodyStyle = new GUIStyle()
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = _textColor }
        };

        _legendNoteStyle = new GUIStyle()
        {
            fontSize = 10,
            fontStyle = FontStyle.Normal,
            wordWrap = true,
            normal = { textColor = _textColor }
        };

        _legendLabelStyle = new GUIStyle()
        {
            fontSize = 11,
            fontStyle = FontStyle.Normal,
            normal = { textColor = _textColor }
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    private void EnsureStyles()
    {
        if (_legendTitleStyle == null || _legendBodyStyle == null || _legendNoteStyle == null || _legendLabelStyle == null)
        {
            CreateStyles();
        }
    }

    private void DrawMuscleLegend(Vector2 cornerPos, ExerciseProfile profile)
    {
        float legendX = cornerPos.x - 140f;
        float legendY = cornerPos.y + 10f;
        float rowHeight = 31f;
        float legendWidth = 220f;

        GUI.Label(new Rect(legendX, legendY, legendWidth, 18f),
            "Muscle emphasis",
            _legendBodyStyle ?? GUI.skin.label);

        for (int i = 0; i < profile.muscleGroups.Length; i++)
        {
            MuscleGroup group = profile.muscleGroups[i];
            float rowY = legendY + 20f + (i * rowHeight);

            GUI.color = group.color;
            GUI.Box(new Rect(legendX, rowY + 4f, 12f, 12f), GUIContent.none);

            GUI.color = _textColor;
            GUI.Label(new Rect(legendX + 18f, rowY, legendWidth - 18f, 16f),
                group.label,
                _legendLabelStyle ?? GUI.skin.label);

            GUI.Label(new Rect(legendX + 18f, rowY + 12f, legendWidth - 18f, 14f),
                group.detail,
                new GUIStyle(GUI.skin.label) { fontSize = 9, fontStyle = FontStyle.Normal, normal = { textColor = new Color(1f, 1f, 1f, 0.72f) } });
            GUI.color = Color.white;
        }

        GUI.Label(new Rect(legendX, legendY + (profile.muscleGroups.Length * rowHeight) + 18f, legendWidth, 30f),
            "Note: this is an anatomical approximation based on pose landmarks, not true muscle sensing.",
            new GUIStyle(GUI.skin.label) { fontSize = 9, fontStyle = FontStyle.Italic, wordWrap = true, normal = { textColor = new Color(1f, 1f, 1f, 0.72f) } });
    }

    // ─────────────────────────────────────────────────────────────────────
    private Color GetColorByQuality(float quality)
    {
        if (quality >= 85f) return activeColor;
        if (quality >= 70f) return supportColor;
        return inactiveRegionColor;
    }

    // ─────────────────────────────────────────────────────────────────────
    private void HandleDashboardPacket(DashboardPacket packet)
    {
        if (packet == null) return;

        _currentExercise = string.IsNullOrWhiteSpace(packet.exercise) ? "unknown" : packet.exercise.Trim();
        _currentFormQuality = ParsePercent(packet.formQuality);
    }

    // ─────────────────────────────────────────────────────────────────────
    private float ParsePercent(string quality)
    {
        if (string.IsNullOrWhiteSpace(quality)) return _currentFormQuality;

        string cleaned = quality.Replace("%", "").Trim();
        if (float.TryParse(cleaned, out float value))
        {
            return Mathf.Clamp(value, 0f, 100f);
        }

        return _currentFormQuality;
    }

    // ─────────────────────────────────────────────────────────────────────
    private ExerciseProfile GetCurrentProfile()
    {
        if (!string.IsNullOrWhiteSpace(_currentExercise))
        {
            string normalized = _currentExercise.Trim().ToLowerInvariant();

            if (_exerciseProfiles.TryGetValue(normalized, out ExerciseProfile profile))
            {
                return profile;
            }

            if (normalized.Contains("shoulder")) return _exerciseProfiles["shoulder"];
            if (normalized.Contains("hip")) return _exerciseProfiles["hip"];
            if (normalized.Contains("squat") || normalized.Contains("knee")) return _exerciseProfiles["squat"];
        }

        return new ExerciseProfile(
            "Movement",
            "Primary movers and stabilizers highlighted for rehab",
            _secondaryColor,
            new[]
            {
                new MuscleGroup("Primary movers", "Depends on exercise selection", _primaryColor, true),
                new MuscleGroup("Stabilizers", "Core and posture support", _secondaryColor, false),
                new MuscleGroup("Inactive", "Not highlighted for this movement", _inactiveColor, false)
            },
            new Dictionary<BodyRegion, float>
            {
                [BodyRegion.Core] = 0.80f,
                [BodyRegion.Torso] = 0.60f
            }
        );
    }

    // ─────────────────────────────────────────────────────────────────────
    private Vector2 GetLegendCornerPosition()
    {
        return new Vector2(Screen.width - 70, Screen.height - 100);
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

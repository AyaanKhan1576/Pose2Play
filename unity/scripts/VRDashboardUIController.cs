using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class VRDashboardUIController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private VRDashboardReceiver dashboardReceiver;

    [Header("Layout")]
    [Tooltip("Optional root RectTransform of the dashboard. Assign to scale the whole panel with one value.")]
    [SerializeField] private RectTransform dashboardRoot;
    [Tooltip("Overall dashboard scale multiplier.")]
    [Range(0.8f, 2.5f)]
    [SerializeField] private float dashboardScale = 1.0f;
    [Tooltip("Scales all TMP font sizes for a larger, easier-to-read panel.")]
    [Range(0.8f, 2.2f)]
    [SerializeField] private float textScale = 1.22f;
    [Tooltip("Automatically shrinks text per-field if a large text scale would clip the HUD.")]
    [SerializeField] private bool adaptiveTextAutosize = true;
    [Tooltip("Minimum autosize font as a multiplier of each text's original size.")]
    [Range(0.4f, 0.95f)]
    [SerializeField] private float autosizeMinMultiplier = 0.62f;
    [Tooltip("Arranges the dashboard as a compact top-of-screen HUD instead of a tall center panel.")]
    [SerializeField] private bool compactTopHudLayout = true;
    [Tooltip("Padding used when positioning the top HUD inside the shared text container.")]
    [SerializeField] private Vector2 topHudPadding = new Vector2(28f, 18f);
    [Tooltip("Spacing between compact HUD rows.")]
    [Range(4f, 36f)]
    [SerializeField] private float topHudRowGap = 10f;
    [Tooltip("Spacing between compact HUD chips.")]
    [Range(4f, 28f)]
    [SerializeField] private float topHudChipGap = 12f;
    [Tooltip("Extra horizontal spacing multiplier for left-side telemetry rows.")]
    [Range(1.0f, 2.0f)]
    [SerializeField] private float leftSectionSpacingMultiplier = 1.15f;

    [Header("HUD Anchor")]
    [Tooltip("Pins this dashboard to a stable viewport position like the on-screen legend.")]
    [SerializeField] private bool pinToCameraView = true;
    [Tooltip("Optional camera override. If empty, Camera.main is used.")]
    [SerializeField] private Camera anchorCamera;
    [Tooltip("Viewport anchor position (0,0 bottom-left to 1,1 top-right).")]
    [SerializeField] private Vector2 viewportAnchor = new Vector2(0.70f, 0.90f);
    [Tooltip("Distance from camera while pinned in viewport mode.")]
    [Range(0.5f, 5f)]
    [SerializeField] private float cameraDistance = 1.20f;
    [Tooltip("Keeps panel upright by using camera yaw only (no roll/pitch tilt).")]
    [SerializeField] private bool yawOnlyFacing = true;
    [Tooltip("How quickly the panel settles into anchor pose.")]
    [Range(1f, 30f)]
    [SerializeField] private float anchorSmoothing = 16f;
    [Tooltip("Extra local rotation offset after anchoring.")]
    [SerializeField] private Vector3 anchoredEulerOffset = Vector3.zero;

    [Header("Theme")]
    [Tooltip("Optional background image of the dashboard panel.")]
    [SerializeField] private Image panelBackground;
    [SerializeField] private Color panelColor = new Color(0.03f, 0.07f, 0.14f, 0.62f);
    [SerializeField] private Color titleColor = new Color(1f, 1f, 1f, 0.96f);
    [SerializeField] private Color scoreColor = new Color(0.90f, 0.95f, 1f, 0.98f);
    [SerializeField] private Color levelColor = new Color(0.90f, 0.98f, 0.88f, 0.98f);
    [SerializeField] private Color comboColor = new Color(1f, 0.92f, 0.72f, 0.98f);
    [SerializeField] private Color rankColor = new Color(1f, 0.84f, 0.48f, 0.98f);
    [SerializeField] private Color achievementColor = new Color(0.86f, 0.92f, 1f, 0.90f);
    [SerializeField] private Color bodyColor = new Color(0.88f, 0.96f, 1f, 0.98f);
    [SerializeField] private Color mutedColor = new Color(0.74f, 0.86f, 0.98f, 0.92f);
    [SerializeField] private Color accentColor = new Color(0.24f, 0.96f, 0.84f, 0.99f);

    [Header("Text")]
    [SerializeField] private TMP_Text exerciseText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text achievementsText;
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private TMP_Text repsText;
    [SerializeField] private TMP_Text angleText;
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private TMP_Text minText;
    [SerializeField] private TMP_Text qualityText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private TMP_Text calibrationText;

    [Header("Visual")]
    [SerializeField] private Image qualityBarFill;
    [Tooltip("Optional quality bar background image (recommended: parent of fill).")]
    [SerializeField] private Image qualityBarBackground;
    [SerializeField] private Color qualityGood = new Color(0.30f, 0.72f, 0.47f);
    [SerializeField] private Color qualityWarn = new Color(0.95f, 0.60f, 0.14f);
    [SerializeField] private Color qualityBad = new Color(0.91f, 0.30f, 0.30f);
    [Tooltip("Scales quality bar width relative to available space.")]
    [Range(0.45f, 1.0f)]
    [SerializeField] private float qualityBarWidthRatio = 0.68f;

    [Header("Gamification HUD")]
    [Tooltip("Places score, level, combo, rank into a dedicated right-side column.")]
    [SerializeField] private bool useGamificationColumn = true;
    [Tooltip("Creates highlighted chip backgrounds behind gamification texts.")]
    [SerializeField] private bool enableGamificationChips = true;
    [SerializeField] private Color gamificationColumnColor = new Color(0.08f, 0.13f, 0.22f, 0.72f);
    [SerializeField] private Color scoreChipColor = new Color(0.10f, 0.22f, 0.35f, 0.86f);
    [SerializeField] private Color levelChipColor = new Color(0.13f, 0.28f, 0.18f, 0.86f);
    [SerializeField] private Color comboChipColor = new Color(0.35f, 0.25f, 0.10f, 0.86f);
    [SerializeField] private Color rankChipColor = new Color(0.37f, 0.27f, 0.09f, 0.88f);
    [SerializeField] private Color achievementsChipColor = new Color(0.12f, 0.20f, 0.31f, 0.74f);
    [SerializeField] private Vector2 gamificationChipPadding = new Vector2(12f, 8f);
    [SerializeField] private Vector2 gamificationColumnPadding = new Vector2(14f, 12f);
    [Tooltip("Moves only the gamification column (X left/right, Y up/down) without affecting the left telemetry block.")]
    [SerializeField] private Vector2 gamificationColumnOffset = Vector2.zero;

    [Header("Cards")]
    [Tooltip("Creates a semi-transparent card image behind each key stat text at runtime.")]
    [SerializeField] private bool enableStatCards = false;
    [SerializeField] private Color statCardColor = new Color(0.05f, 0.09f, 0.14f, 0.52f);
    [SerializeField] private Color statusCardColor = new Color(0.10f, 0.15f, 0.22f, 0.72f);
    [SerializeField] private Color feedbackCardColor = new Color(0.08f, 0.11f, 0.16f, 0.50f);
    [SerializeField] private Vector2 statCardPadding = new Vector2(18f, 10f);
    [SerializeField] private bool autoReorderStats = true;

    [Header("Unified Card")]
    [Tooltip("Creates one coherent semi-transparent background for the whole dashboard text block.")]
    [SerializeField] private bool useUnifiedCardBackground = true;
    [SerializeField] private Color unifiedCardColor = new Color(0.07f, 0.11f, 0.16f, 0.66f);
    [SerializeField] private Vector2 unifiedCardPadding = new Vector2(22f, 16f);
    [Tooltip("Adds extra downward extension so the feedback row remains inside the card.")]
    [Range(0f, 120f)]
    [SerializeField] private float unifiedCardExtraBottom = 50f;
    [Tooltip("Include feedback row in unified card bounds.")]
    [SerializeField] private bool includeFeedbackInUnifiedCard = true;
    [Tooltip("Max feedback characters shown to avoid clipping in compact dashboard mode.")]
    [Range(30, 220)]
    [SerializeField] private int maxFeedbackCharacters = 90;

    private readonly Dictionary<TMP_Text, float> _baseFontSizes = new Dictionary<TMP_Text, float>();
    private readonly Dictionary<TMP_Text, Image> _cardsByText = new Dictionary<TMP_Text, Image>();
    private readonly Dictionary<TMP_Text, Image> _gamificationChipByText = new Dictionary<TMP_Text, Image>();
    private RectTransform _resolvedRoot;
    private Image _unifiedCardImage;
    private Image _gamificationColumnImage;

    private void Awake()
    {
        if (dashboardReceiver == null)
        {
            dashboardReceiver = FindFirstObjectByType<VRDashboardReceiver>();
            if (dashboardReceiver != null)
            {
                Debug.Log("[VRDashboardUIController] Auto-linked VRDashboardReceiver.");
            }
        }
    }

    private void Start()
    {
        ValidateBindings();
        ResolveRootAndCamera();
        CacheBaseFontSizes();
        ApplyDashboardStyle();
        ConfigureTopHudLayout();
        BuildStatCards();
        BuildUnifiedCard();
        RearrangeStats();
        BuildGamificationChrome();
        RefreshCardGeometry();
    }

    private void OnEnable()
    {
        if (dashboardReceiver != null)
        {
            dashboardReceiver.OnDashboardPacket -= HandleDashboardPacket;
            dashboardReceiver.OnDashboardPacket += HandleDashboardPacket;
        }
        else
        {
            Debug.LogWarning("[VRDashboardUIController] dashboardReceiver is not assigned.");
        }
    }

    private void OnDisable()
    {
        if (dashboardReceiver != null)
        {
            dashboardReceiver.OnDashboardPacket -= HandleDashboardPacket;
        }
    }

    private void LateUpdate()
    {
        ApplyPinnedAnchor();
        ConfigureTopHudLayout();
        RefreshCardGeometry();
    }

    private void HandleDashboardPacket(DashboardPacket packet)
    {
        if (packet == null) return;

        string prettyExercise = PrettyExercise(packet.exercise);
        float qualityPercent = ParsePercent(packet.formQuality);
        Color statusColor = GetStatusColor(packet.status);
        int repCount = Mathf.Max(0, packet.repCount);

        int score = ResolveScore(packet, repCount, qualityPercent);
        int level = ResolveLevel(packet, score);
        int combo = ResolveCombo(packet, repCount, qualityPercent);
        string rank = ResolveRank(packet, qualityPercent);
        string[] achievements = ResolveAchievements(packet, repCount, qualityPercent, combo);

        string phaseValue = string.IsNullOrWhiteSpace(packet.phase) ? "UNKNOWN" : packet.phase.ToUpperInvariant();
        string statusValue = string.IsNullOrWhiteSpace(packet.status) ? "WAITING" : packet.status.ToUpperInvariant();

        SetText(exerciseText, $"<b><size=122%>{prettyExercise.ToUpperInvariant()} DASHBOARD</size></b>");
        SetText(scoreText, $"<size=72%><color=#FFFFFFAA>SCORE</color></size>  <b><size=110%>{score}</size></b>");
        SetText(levelText, $"<size=72%><color=#FFFFFFAA>LEVEL</color></size>  <b><size=110%>{level}</size></b>");
        SetText(comboText, $"<size=72%><color=#FFFFFFAA>COMBO</color></size>  <b><size=110%>x{combo}</size></b>");
        SetText(rankText, $"<size=72%><color=#FFFFFFAA>RANK</color></size>  <b><size=106%><color=#{ColorToHex(GetRankColor(rank))}>{PrettyRank(rank)}</color></size></b>");
        SetText(achievementsText, $"<size=62%><color=#FFFFFF99>ACHIEVEMENTS</color></size>\n<b>{FormatAchievements(achievements)}</b>");
        SetText(phaseText, $"<size=86%><color=#7FE7FF>PHASE</color></size>  <b><size=118%>{phaseValue}</size></b>");
        SetText(repsText, $"<size=86%><color=#7FE7FF>REPS</color></size>  <b><size=122%>{repCount}</size></b>");
        SetText(angleText, $"<size=84%><color=#7FE7FF>ANGLE</color></size>  <b><size=116%>{Mathf.RoundToInt(packet.currentAngle)} deg</size></b>");
        SetText(targetText, $"<size=84%><color=#7FE7FF>PUSH</color></size>  <b><size=118%>{Mathf.RoundToInt(packet.pushTarget)} deg</size></b>");
        SetText(minText, $"<size=84%><color=#7FE7FF>MIN</color></size>  <b><size=116%>{Mathf.RoundToInt(packet.minimumThreshold)} deg</size></b>");
        SetText(qualityText, $"<size=84%><color=#7FE7FF>FORM</color></size>  <b><size=118%><color=#{ColorToHex(GetQualityColor(qualityPercent))}>{Mathf.RoundToInt(qualityPercent)}%</color></size></b>");
        SetText(statusText, $"<size=84%><color=#7FE7FF>STATUS</color></size>  <b><size=114%><color=#{ColorToHex(statusColor)}>{statusValue}</color></size></b>");
        string feedbackValue = string.IsNullOrWhiteSpace(packet.feedback)
            ? "Awaiting movement feedback..."
            : Truncate(packet.feedback.Trim(), maxFeedbackCharacters);
        SetText(feedbackText, $"<size=82%><color=#FFFFFFAA>COACH FEEDBACK</color></size>  <i>{feedbackValue}</i>");

        int done = packet.calibration != null ? packet.calibration.count : 0;
        int required = packet.calibration != null ? packet.calibration.required : 3;
        SetText(calibrationText, $"<size=84%><color=#7FE7FF>CALIBRATION</color></size>  <b><size=118%>{done}/{required}</size></b>");

        UpdateQualityBar(packet.formQuality);
        UpdateStatusCard(statusColor);
    }

    private void UpdateQualityBar(string quality)
    {
        if (qualityBarFill == null) return;

        float percent = ParsePercent(quality);
        qualityBarFill.fillAmount = Mathf.Clamp01(percent / 100f);

        if (percent >= 85f)
        {
            qualityBarFill.color = qualityGood;
        }
        else if (percent >= 70f)
        {
            qualityBarFill.color = qualityWarn;
        }
        else
        {
            qualityBarFill.color = qualityBad;
        }
    }

    private static float ParsePercent(string quality)
    {
        if (string.IsNullOrEmpty(quality)) return 0f;
        string cleaned = quality.Replace("%", "").Trim();
        if (float.TryParse(cleaned, out float value))
        {
            return value;
        }
        return 0f;
    }

    private static string PrettyExercise(string exercise)
    {
        if (string.IsNullOrEmpty(exercise)) return "Unknown";
        if (exercise == "squat") return "Squat";
        if (exercise == "hip") return "Hip";
        if (exercise == "shoulder") return "Shoulder";
        return exercise;
    }

    private static void SetText(TMP_Text field, string value)
    {
        if (field != null)
        {
            field.richText = true;
            field.text = value;
        }
    }

    private void CacheBaseFontSizes()
    {
        _baseFontSizes.Clear();
        CacheTextSize(exerciseText);
        CacheTextSize(scoreText);
        CacheTextSize(levelText);
        CacheTextSize(comboText);
        CacheTextSize(rankText);
        CacheTextSize(achievementsText);
        CacheTextSize(phaseText);
        CacheTextSize(repsText);
        CacheTextSize(angleText);
        CacheTextSize(targetText);
        CacheTextSize(minText);
        CacheTextSize(qualityText);
        CacheTextSize(statusText);
        CacheTextSize(feedbackText);
        CacheTextSize(calibrationText);
    }

    private void CacheTextSize(TMP_Text text)
    {
        if (text != null && !_baseFontSizes.ContainsKey(text))
        {
            _baseFontSizes[text] = text.fontSize;
        }
    }

    private void ApplyDashboardStyle()
    {
        if (dashboardRoot != null)
        {
            dashboardRoot.localScale = Vector3.one * dashboardScale;
        }

        if (panelBackground != null)
        {
            panelBackground.color = panelColor;
            panelBackground.raycastTarget = false;
        }

        ApplyTextTheme(exerciseText, titleColor);
        ApplyTextTheme(scoreText, scoreColor, TextAlignmentOptions.Center);
        ApplyTextTheme(levelText, levelColor, TextAlignmentOptions.Center);
        ApplyTextTheme(comboText, comboColor, TextAlignmentOptions.Center);
        ApplyTextTheme(rankText, rankColor, TextAlignmentOptions.Center);
        ApplyTextTheme(achievementsText, achievementColor, TextAlignmentOptions.Center);
        ApplyTextTheme(phaseText, bodyColor);
        ApplyTextTheme(repsText, bodyColor);
        ApplyTextTheme(angleText, bodyColor);
        ApplyTextTheme(targetText, bodyColor);
        ApplyTextTheme(minText, mutedColor);
        ApplyTextTheme(qualityText, bodyColor);
        ApplyTextTheme(statusText, bodyColor);
        ApplyTextTheme(feedbackText, mutedColor);
        ApplyTextTheme(calibrationText, bodyColor);

        if (exerciseText != null)
        {
            exerciseText.characterSpacing = 1.8f;
            exerciseText.enableWordWrapping = false;
        }

        if (feedbackText != null)
        {
            feedbackText.enableWordWrapping = false;
        }

        ApplyOverflowModes();
    }

    private void ApplyTextTheme(TMP_Text text, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        if (text == null)
            return;

        text.enableWordWrapping = false;
        text.color = color;
        text.alignment = alignment;

        if (_baseFontSizes.TryGetValue(text, out float baseSize))
        {
            float maxSize = baseSize * textScale;
            float minSize = baseSize * autosizeMinMultiplier;
            text.fontSize = maxSize;
            text.enableAutoSizing = adaptiveTextAutosize;
            text.fontSizeMin = Mathf.Min(minSize, maxSize);
            text.fontSizeMax = Mathf.Max(minSize, maxSize);
        }
    }

    private Color GetQualityColor(float percent)
    {
        if (percent >= 85f) return qualityGood;
        if (percent >= 70f) return qualityWarn;
        return qualityBad;
    }

    private Color GetStatusColor(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return mutedColor;

        string s = status.ToLowerInvariant();
        if (s.Contains("good") || s.Contains("ready") || s.Contains("tracking"))
            return qualityGood;
        if (s.Contains("pause") || s.Contains("calib") || s.Contains("hold"))
            return qualityWarn;
        if (s.Contains("bad") || s.Contains("error") || s.Contains("stop"))
            return qualityBad;

        return bodyColor;
    }

    private static string ColorToHex(Color c)
    {
        Color32 c32 = c;
        return $"{c32.r:X2}{c32.g:X2}{c32.b:X2}";
    }

    private void BuildStatCards()
    {
        if (!enableStatCards || useUnifiedCardBackground)
            return;

        CreateCardForText(scoreText, statCardColor);
        CreateCardForText(levelText, statCardColor);
        CreateCardForText(comboText, statCardColor);
        CreateCardForText(rankText, statCardColor);
        CreateCardForText(achievementsText, statCardColor);
        CreateCardForText(repsText, statCardColor);
        CreateCardForText(angleText, statCardColor);
        CreateCardForText(targetText, statCardColor);
        CreateCardForText(minText, statCardColor);
        CreateCardForText(qualityText, statCardColor);
        CreateCardForText(calibrationText, statCardColor);
        CreateCardForText(phaseText, statCardColor);
        CreateCardForText(statusText, statusCardColor);
        CreateCardForText(feedbackText, feedbackCardColor);
    }

    private void CreateCardForText(TMP_Text text, Color color)
    {
        if (text == null)
            return;

        RectTransform textRect = text.rectTransform;
        if (textRect == null || textRect.parent == null)
            return;

        if (_cardsByText.TryGetValue(text, out Image existingCard) && existingCard != null)
        {
            existingCard.color = color;
            return;
        }

        string cardName = $"{text.name}_Card";
        Transform parent = textRect.parent;
        Transform existing = parent.Find(cardName);

        GameObject cardObject = existing != null ? existing.gameObject : new GameObject(cardName, typeof(RectTransform), typeof(Image));
        cardObject.transform.SetParent(parent, false);

        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = textRect.anchorMin;
        cardRect.anchorMax = textRect.anchorMax;
        cardRect.pivot = textRect.pivot;
        cardRect.anchoredPosition = textRect.anchoredPosition;
        cardRect.sizeDelta = textRect.sizeDelta + (statCardPadding * 2f);
        cardRect.localScale = Vector3.one;

        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = color;
        cardImage.raycastTarget = false;

        Shadow shadow = cardObject.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = cardObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
        shadow.effectDistance = new Vector2(0f, -3f);
        shadow.useGraphicAlpha = true;

        int textIndex = textRect.GetSiblingIndex();
        cardRect.SetSiblingIndex(Mathf.Max(0, textIndex - 1));

        _cardsByText[text] = cardImage;
    }

    private void RearrangeStats()
    {
        if (!autoReorderStats)
            return;

        TMP_Text[] order =
        {
            exerciseText,
            scoreText,
            levelText,
            comboText,
            rankText,
            achievementsText,
            phaseText,
            statusText,
            repsText,
            qualityText,
            angleText,
            targetText,
            minText,
            calibrationText,
            feedbackText
        };

        RectTransform sharedParent = null;
        for (int i = 0; i < order.Length; i++)
        {
            TMP_Text t = order[i];
            if (t != null && t.rectTransform != null && t.rectTransform.parent != null)
            {
                sharedParent = t.rectTransform.parent as RectTransform;
                break;
            }
        }

        if (sharedParent == null)
            return;

        int index = 0;
        for (int i = 0; i < order.Length; i++)
        {
            TMP_Text t = order[i];
            if (t == null || t.rectTransform == null || t.rectTransform.parent != sharedParent)
                continue;

            t.rectTransform.SetSiblingIndex(index++);

            if (_cardsByText.TryGetValue(t, out Image card) && card != null)
            {
                card.rectTransform.SetSiblingIndex(Mathf.Max(0, t.rectTransform.GetSiblingIndex() - 1));
            }
        }
    }

    private void ResolveRootAndCamera()
    {
        if (dashboardRoot != null)
        {
            _resolvedRoot = dashboardRoot;
        }
        else
        {
            _resolvedRoot = transform as RectTransform;
        }

        if (anchorCamera == null)
        {
            anchorCamera = Camera.main;
        }
    }

    private void ApplyPinnedAnchor()
    {
        if (!pinToCameraView || _resolvedRoot == null)
            return;

        if (anchorCamera == null)
        {
            anchorCamera = Camera.main;
            if (anchorCamera == null)
                return;
        }

        Vector3 viewPoint = new Vector3(
            Mathf.Clamp01(viewportAnchor.x),
            Mathf.Clamp01(viewportAnchor.y),
            Mathf.Max(0.2f, cameraDistance));

        Vector3 targetPos = anchorCamera.ViewportToWorldPoint(viewPoint);
        Quaternion targetRot;

        if (yawOnlyFacing)
        {
            Vector3 forward = anchorCamera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            targetRot = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
        else
        {
            targetRot = Quaternion.LookRotation(anchorCamera.transform.forward, Vector3.up);
        }

        targetRot *= Quaternion.Euler(anchoredEulerOffset);
        float t = Mathf.Clamp01(anchorSmoothing * Time.deltaTime);

        _resolvedRoot.position = Vector3.Lerp(_resolvedRoot.position, targetPos, t);
        _resolvedRoot.rotation = Quaternion.Slerp(_resolvedRoot.rotation, targetRot, t);
    }

    private void RefreshCardGeometry()
    {
        if (enableStatCards && !useUnifiedCardBackground && _cardsByText.Count > 0)
        {
            foreach (KeyValuePair<TMP_Text, Image> pair in _cardsByText)
            {
                TMP_Text text = pair.Key;
                Image card = pair.Value;

                if (text == null || card == null)
                    continue;

                RectTransform textRect = text.rectTransform;
                RectTransform cardRect = card.rectTransform;
                if (textRect == null || cardRect == null)
                    continue;

                cardRect.anchorMin = textRect.anchorMin;
                cardRect.anchorMax = textRect.anchorMax;
                cardRect.pivot = textRect.pivot;
                cardRect.anchoredPosition = textRect.anchoredPosition;
                cardRect.sizeDelta = textRect.sizeDelta + (statCardPadding * 2f);
                cardRect.localScale = Vector3.one;
            }
        }

        RefreshGamificationChrome();

        RefreshUnifiedCardGeometry();
    }

    private void BuildUnifiedCard()
    {
        if (!useUnifiedCardBackground)
            return;

        RectTransform parent = GetSharedTextParent();
        if (parent == null)
            return;

        const string unifiedCardName = "Dashboard_UniformCard";
        Transform existing = parent.Find(unifiedCardName);
        GameObject cardObject = existing != null
            ? existing.gameObject
            : new GameObject(unifiedCardName, typeof(RectTransform), typeof(Image), typeof(Shadow));

        cardObject.transform.SetParent(parent, false);
        _unifiedCardImage = cardObject.GetComponent<Image>();
        _unifiedCardImage.color = unifiedCardColor;
        _unifiedCardImage.raycastTarget = false;

        Shadow shadow = cardObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.33f);
        shadow.effectDistance = new Vector2(0f, -4f);
        shadow.useGraphicAlpha = true;

        int firstIndex = GetTopMostTextSiblingIndex(parent);
        cardObject.GetComponent<RectTransform>().SetSiblingIndex(Mathf.Max(0, firstIndex - 1));

        RefreshUnifiedCardGeometry();
    }

    private void RefreshUnifiedCardGeometry()
    {
        if (!useUnifiedCardBackground || _unifiedCardImage == null)
            return;

        RectTransform parent = _unifiedCardImage.rectTransform.parent as RectTransform;
        if (parent == null)
            return;

        TMP_Text[] blockTexts =
        {
            exerciseText,
            scoreText,
            levelText,
            comboText,
            rankText,
            achievementsText,
            phaseText,
            statusText,
            repsText,
            qualityText,
            angleText,
            targetText,
            minText,
            calibrationText,
            includeFeedbackInUnifiedCard ? feedbackText : null
        };

        bool hasBounds = false;
        float minX = 0f;
        float maxX = 0f;
        float minY = 0f;
        float maxY = 0f;

        for (int i = 0; i < blockTexts.Length; i++)
        {
            TMP_Text text = blockTexts[i];
            if (text == null || text.rectTransform == null || text.rectTransform.parent != parent)
                continue;

            Vector3[] corners = new Vector3[4];
            text.rectTransform.GetWorldCorners(corners);

            for (int c = 0; c < 4; c++)
            {
                Vector2 local = parent.InverseTransformPoint(corners[c]);
                if (!hasBounds)
                {
                    minX = maxX = local.x;
                    minY = maxY = local.y;
                    hasBounds = true;
                }
                else
                {
                    if (local.x < minX) minX = local.x;
                    if (local.x > maxX) maxX = local.x;
                    if (local.y < minY) minY = local.y;
                    if (local.y > maxY) maxY = local.y;
                }
            }
        }

        if (!hasBounds)
            return;

        minX -= unifiedCardPadding.x;
        maxX += unifiedCardPadding.x;
        minY -= (unifiedCardPadding.y + unifiedCardExtraBottom);
        maxY += unifiedCardPadding.y;

        float width = maxX - minX;
        float height = maxY - minY;

        RectTransform cardRect = _unifiedCardImage.rectTransform;
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        // Pin to the top-left of the computed bounds so extra bottom space extends downward.
        cardRect.pivot = new Vector2(0f, 1f);
        cardRect.anchoredPosition = new Vector2(minX, maxY);
        cardRect.sizeDelta = new Vector2(width, height);
        cardRect.localScale = Vector3.one;
    }

    private RectTransform GetSharedTextParent()
    {
        TMP_Text[] texts = { exerciseText, scoreText, levelText, comboText, rankText, achievementsText, phaseText, repsText, angleText, targetText, minText, qualityText, statusText, feedbackText, calibrationText };
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text t = texts[i];
            if (t != null && t.rectTransform != null && t.rectTransform.parent != null)
            {
                return t.rectTransform.parent as RectTransform;
            }
        }

        return null;
    }

    private int GetTopMostTextSiblingIndex(RectTransform parent)
    {
        int best = int.MaxValue;
        TMP_Text[] texts = { exerciseText, scoreText, levelText, comboText, rankText, achievementsText, phaseText, repsText, angleText, targetText, minText, qualityText, statusText, feedbackText, calibrationText };
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text t = texts[i];
            if (t == null || t.rectTransform == null || t.rectTransform.parent != parent)
                continue;

            int idx = t.rectTransform.GetSiblingIndex();
            if (idx < best) best = idx;
        }

        return best == int.MaxValue ? 0 : best;
    }

    private void ApplyOverflowModes()
    {
        ApplyOverflowMode(exerciseText, TextOverflowModes.Truncate);
        ApplyOverflowMode(scoreText, TextOverflowModes.Overflow);
        ApplyOverflowMode(levelText, TextOverflowModes.Overflow);
        ApplyOverflowMode(comboText, TextOverflowModes.Overflow);
        ApplyOverflowMode(rankText, TextOverflowModes.Overflow);
        ApplyOverflowMode(achievementsText, TextOverflowModes.Ellipsis);
        ApplyOverflowMode(phaseText, TextOverflowModes.Ellipsis);
        ApplyOverflowMode(repsText, TextOverflowModes.Ellipsis);
        ApplyOverflowMode(angleText, TextOverflowModes.Ellipsis);
        ApplyOverflowMode(targetText, TextOverflowModes.Ellipsis);
        ApplyOverflowMode(minText, TextOverflowModes.Ellipsis);
        ApplyOverflowMode(qualityText, TextOverflowModes.Ellipsis);
        ApplyOverflowMode(statusText, TextOverflowModes.Ellipsis);
        ApplyOverflowMode(feedbackText, TextOverflowModes.Ellipsis);
        ApplyOverflowMode(calibrationText, TextOverflowModes.Ellipsis);
    }

    private static void ApplyOverflowMode(TMP_Text text, TextOverflowModes mode)
    {
        if (text == null)
            return;

        text.overflowMode = mode;
    }

    private static string Truncate(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            return text;

        if (maxChars <= 3)
            return text.Substring(0, Mathf.Max(0, maxChars));

        return text.Substring(0, maxChars - 3).TrimEnd() + "...";
    }

    private void UpdateStatusCard(Color statusColor)
    {
        if (_cardsByText.TryGetValue(statusText, out Image statusCard) && statusCard != null)
        {
            Color tint = statusColor;
            tint.a = Mathf.Clamp01(statusCardColor.a);
            statusCard.color = tint;
        }
    }

    private void BuildGamificationChrome()
    {
        if (!enableGamificationChips)
            return;

        RectTransform parent = GetSharedTextParent();
        if (parent == null)
            return;

        const string columnName = "Dashboard_GamificationColumn";
        Transform existingColumn = parent.Find(columnName);
        GameObject columnObject = existingColumn != null
            ? existingColumn.gameObject
            : new GameObject(columnName, typeof(RectTransform), typeof(Image), typeof(Shadow));
        columnObject.transform.SetParent(parent, false);

        _gamificationColumnImage = columnObject.GetComponent<Image>();
        _gamificationColumnImage.color = gamificationColumnColor;
        _gamificationColumnImage.raycastTarget = false;

        Shadow columnShadow = columnObject.GetComponent<Shadow>();
        columnShadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        columnShadow.effectDistance = new Vector2(0f, -3f);
        columnShadow.useGraphicAlpha = true;

        CreateGamificationChip(scoreText, scoreChipColor);
        CreateGamificationChip(levelText, levelChipColor);
        CreateGamificationChip(comboText, comboChipColor);
        CreateGamificationChip(rankText, rankChipColor);
        CreateGamificationChip(achievementsText, achievementsChipColor);

        RefreshGamificationChrome();
    }

    private void CreateGamificationChip(TMP_Text text, Color color)
    {
        if (text == null || text.rectTransform == null || text.rectTransform.parent == null)
            return;

        if (_gamificationChipByText.TryGetValue(text, out Image existingChip) && existingChip != null)
        {
            existingChip.color = color;
            return;
        }

        string chipName = $"{text.name}_GamificationChip";
        Transform parent = text.rectTransform.parent;
        Transform existing = parent.Find(chipName);
        GameObject chipObject = existing != null
            ? existing.gameObject
            : new GameObject(chipName, typeof(RectTransform), typeof(Image), typeof(Shadow));
        chipObject.transform.SetParent(parent, false);

        Image chipImage = chipObject.GetComponent<Image>();
        chipImage.color = color;
        chipImage.raycastTarget = false;

        Shadow shadow = chipObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.34f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;

        _gamificationChipByText[text] = chipImage;
    }

    private void RefreshGamificationChrome()
    {
        if (!enableGamificationChips || _gamificationChipByText.Count == 0)
            return;

        RectTransform parent = GetSharedTextParent();
        if (parent == null)
            return;

        TMP_Text[] chips = { scoreText, levelText, comboText, rankText, achievementsText };

        bool hasBounds = false;
        float minX = 0f;
        float maxX = 0f;
        float minY = 0f;
        float maxY = 0f;

        for (int i = 0; i < chips.Length; i++)
        {
            TMP_Text text = chips[i];
            if (text == null || text.rectTransform == null || text.rectTransform.parent != parent)
                continue;

            if (_gamificationChipByText.TryGetValue(text, out Image chip) && chip != null)
            {
                RectTransform textRect = text.rectTransform;
                RectTransform chipRect = chip.rectTransform;
                chipRect.anchorMin = textRect.anchorMin;
                chipRect.anchorMax = textRect.anchorMax;
                chipRect.pivot = textRect.pivot;
                chipRect.anchoredPosition = textRect.anchoredPosition;
                chipRect.sizeDelta = textRect.sizeDelta + (gamificationChipPadding * 2f);
                chipRect.localScale = Vector3.one;
                chipRect.SetSiblingIndex(Mathf.Max(0, textRect.GetSiblingIndex() - 1));

                Vector3[] corners = new Vector3[4];
                chipRect.GetWorldCorners(corners);
                for (int c = 0; c < 4; c++)
                {
                    Vector2 local = parent.InverseTransformPoint(corners[c]);
                    if (!hasBounds)
                    {
                        minX = maxX = local.x;
                        minY = maxY = local.y;
                        hasBounds = true;
                    }
                    else
                    {
                        if (local.x < minX) minX = local.x;
                        if (local.x > maxX) maxX = local.x;
                        if (local.y < minY) minY = local.y;
                        if (local.y > maxY) maxY = local.y;
                    }
                }
            }
        }

        if (_gamificationColumnImage == null || !hasBounds)
            return;

        minX -= gamificationColumnPadding.x;
        maxX += gamificationColumnPadding.x;
        minY -= gamificationColumnPadding.y;
        maxY += gamificationColumnPadding.y;

        RectTransform columnRect = _gamificationColumnImage.rectTransform;
        columnRect.anchorMin = new Vector2(0.5f, 0.5f);
        columnRect.anchorMax = new Vector2(0.5f, 0.5f);
        columnRect.pivot = new Vector2(0f, 1f);
        columnRect.anchoredPosition = new Vector2(minX, maxY);
        columnRect.sizeDelta = new Vector2(maxX - minX, maxY - minY);
        columnRect.localScale = Vector3.one;

        int firstChipIndex = int.MaxValue;
        for (int i = 0; i < chips.Length; i++)
        {
            TMP_Text text = chips[i];
            if (text == null || text.rectTransform == null || text.rectTransform.parent != parent)
                continue;

            int idx = text.rectTransform.GetSiblingIndex();
            if (idx < firstChipIndex) firstChipIndex = idx;
        }

        columnRect.SetSiblingIndex(firstChipIndex == int.MaxValue ? 0 : Mathf.Max(0, firstChipIndex - 1));
    }

    private void ConfigureTopHudLayout()
    {
        if (!compactTopHudLayout)
            return;

        Canvas.ForceUpdateCanvases();
        RectTransform parent = GetSharedTextParent();
        if (parent == null)
            return;

        float scaleT = Mathf.InverseLerp(1.0f, 2.2f, Mathf.Clamp(textScale, 1.0f, 2.2f));
        float adaptiveRowGap = topHudRowGap * Mathf.Lerp(1.0f, 1.35f, scaleT);
        float leftGap = topHudChipGap * Mathf.Clamp(leftSectionSpacingMultiplier, 1.0f, 2.0f);
        float width = Mathf.Max(1f, parent.rect.width);
        float height = Mathf.Max(1f, parent.rect.height);
        float padX = Mathf.Clamp(topHudPadding.x, 10f, width * 0.08f);
        float padY = Mathf.Clamp(topHudPadding.y, 8f, height * 0.12f);
        float chipHeight = Mathf.Clamp(height * Mathf.Lerp(0.11f, 0.145f, scaleT), 38f, 86f);
        float columnWidth = useGamificationColumn
            ? Mathf.Clamp(width * Mathf.Lerp(0.23f, 0.32f, scaleT), 220f, 460f)
            : Mathf.Clamp(width * 0.12f, 130f, 240f);
        float columnX = width - padX - columnWidth - Mathf.Lerp(8f, 16f, scaleT);
        float leftWidth = Mathf.Max(420f, columnX - padX - leftGap);
        float rowOneY = -padY;
        float rowTwoY = rowOneY - chipHeight - adaptiveRowGap;
        float rowThreeY = rowTwoY - chipHeight - adaptiveRowGap;
        float rowFourY = rowThreeY - chipHeight - adaptiveRowGap;
        float rowFiveY = rowFourY - chipHeight - adaptiveRowGap;

        ApplyHudRect(exerciseText, parent, new Vector2(padX, rowOneY), new Vector2(leftWidth, chipHeight + 10f), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);

        if (useGamificationColumn)
        {
            float gx = gamificationColumnOffset.x;
            float gy = gamificationColumnOffset.y;
            float columnChipHeight = Mathf.Clamp(chipHeight * Mathf.Lerp(0.90f, 0.98f, scaleT), 34f, 62f);
            float columnGap = Mathf.Clamp(adaptiveRowGap * 0.48f, 4f, 11f);
            float columnRowOneY = rowOneY + gy;
            float columnRowTwoY = columnRowOneY - columnChipHeight - columnGap;
            float columnRowThreeY = columnRowTwoY - columnChipHeight - columnGap;
            float columnRowFourY = columnRowThreeY - columnChipHeight - columnGap;
            float columnRowFiveY = columnRowFourY - columnChipHeight - columnGap;
            float columnXOffset = columnX + gx;
            ApplyHudRect(scoreText, parent, new Vector2(columnXOffset, columnRowOneY), new Vector2(columnWidth, columnChipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);
            ApplyHudRect(levelText, parent, new Vector2(columnXOffset, columnRowTwoY), new Vector2(columnWidth, columnChipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);
            ApplyHudRect(comboText, parent, new Vector2(columnXOffset, columnRowThreeY), new Vector2(columnWidth, columnChipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);
            ApplyHudRect(rankText, parent, new Vector2(columnXOffset, columnRowFourY), new Vector2(columnWidth, columnChipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);
            ApplyHudRect(achievementsText, parent, new Vector2(columnXOffset, columnRowFiveY), new Vector2(columnWidth, columnChipHeight + 6f), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);
        }
        else
        {
            float chipWidth = Mathf.Clamp(width * 0.12f, 130f, 240f);
            float rightStart = width - padX - (chipWidth * 4f) - (topHudChipGap * 3f);
            ApplyHudRect(scoreText, parent, new Vector2(rightStart, rowOneY), new Vector2(chipWidth, chipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Center);
            ApplyHudRect(levelText, parent, new Vector2(rightStart + chipWidth + topHudChipGap, rowOneY), new Vector2(chipWidth, chipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Center);
            ApplyHudRect(comboText, parent, new Vector2(rightStart + (chipWidth + topHudChipGap) * 2f, rowOneY), new Vector2(chipWidth, chipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Center);
            ApplyHudRect(rankText, parent, new Vector2(rightStart + (chipWidth + topHudChipGap) * 3f, rowOneY), new Vector2(chipWidth, chipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Center);
            ApplyHudRect(achievementsText, parent, new Vector2(width - padX - (chipWidth * 2f + topHudChipGap), rowTwoY), new Vector2(chipWidth * 2f + topHudChipGap, chipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Center);
        }

        float totalGap = leftGap * 3f;
        float phaseWidth = Mathf.Clamp(leftWidth * 0.20f, 120f, 360f);
        float statusWidth = Mathf.Clamp(leftWidth * 0.34f, 200f, 520f);
        float repsWidth = Mathf.Clamp(leftWidth * 0.17f, 110f, 280f);
        float qualityWidth = Mathf.Max(120f, leftWidth - totalGap - phaseWidth - statusWidth - repsWidth);
        float xRowTwo = padX;
        ApplyHudRect(phaseText, parent, new Vector2(xRowTwo, rowTwoY), new Vector2(phaseWidth, chipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);
        xRowTwo += phaseWidth + leftGap;
        ApplyHudRect(statusText, parent, new Vector2(xRowTwo, rowTwoY), new Vector2(statusWidth, chipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);
        xRowTwo += statusWidth + leftGap;
        ApplyHudRect(repsText, parent, new Vector2(xRowTwo, rowTwoY), new Vector2(repsWidth, chipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);
        xRowTwo += repsWidth + leftGap;
        ApplyHudRect(qualityText, parent, new Vector2(xRowTwo, rowTwoY), new Vector2(qualityWidth, chipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);

        float detailWidth = Mathf.Clamp((leftWidth - (leftGap * 2f)) / 3f, 120f, 520f);
        ApplyHudRect(calibrationText, parent, new Vector2(padX, rowThreeY), new Vector2(detailWidth, chipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);
        ApplyHudRect(angleText, parent, new Vector2(padX + detailWidth + leftGap, rowThreeY), new Vector2(detailWidth, chipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);
        ApplyHudRect(minText, parent, new Vector2(padX + (detailWidth + leftGap) * 2f, rowThreeY), new Vector2(detailWidth, chipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);

        float pushWidth = Mathf.Clamp(leftWidth * Mathf.Lerp(0.22f, 0.30f, scaleT), 130f, 310f);
        ApplyHudRect(targetText, parent, new Vector2(padX, rowFourY), new Vector2(pushWidth, chipHeight), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);
        RectTransform qualityBarRoot = GetQualityBarRoot();
        if (qualityBarRoot != null)
        {
            float availableBarWidth = Mathf.Clamp(leftWidth - pushWidth - leftGap, 180f, 500f);
            float barWidth = Mathf.Clamp(availableBarWidth * qualityBarWidthRatio, 150f, 420f);
            float slimBarHeight = Mathf.Clamp(chipHeight * 0.58f, 16f, 28f);
            float slimBarY = rowFourY - ((chipHeight - slimBarHeight) * 0.5f);
            ApplyHudRect(qualityBarRoot, parent, new Vector2(padX + pushWidth + leftGap, slimBarY), new Vector2(barWidth, slimBarHeight), new Vector2(0f, 1f), new Vector2(0f, 1f));
            Image qualityBarBg = qualityBarBackground != null ? qualityBarBackground : qualityBarRoot.GetComponent<Image>();
            if (qualityBarBg != null)
            {
                qualityBarBg.color = new Color(0.10f, 0.18f, 0.30f, 0.92f);
            }

            RectTransform fillRect = qualityBarFill != null ? qualityBarFill.rectTransform : null;
            if (fillRect != null && fillRect.parent == qualityBarRoot)
            {
                // Keep fill and background aligned when layout resizes dynamically.
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(1f, 1f);
                fillRect.pivot = new Vector2(0.5f, 0.5f);
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = new Vector2(-6f, -4f);
                fillRect.localScale = Vector3.one;
            }
        }

        ApplyHudRect(feedbackText, parent, new Vector2(padX, rowFiveY), new Vector2(leftWidth, chipHeight + 8f), new Vector2(0f, 1f), new Vector2(0f, 1f), TextAlignmentOptions.Left);
    }

    private void ApplyHudRect(TMP_Text text, RectTransform parent, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions alignment)
    {
        if (text == null || text.rectTransform == null || text.rectTransform.parent != parent)
            return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        text.alignment = alignment;
        text.enableWordWrapping = false;
    }

    private void ApplyHudRect(RectTransform rect, RectTransform parent, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rect == null || rect.parent != parent)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
    }

    private RectTransform GetQualityBarRoot()
    {
        if (qualityBarFill == null)
            return null;

        if (qualityBarFill.transform.parent is RectTransform parent)
            return parent;

        return qualityBarFill.rectTransform;
    }

    private static int ResolveScore(DashboardPacket packet, int repCount, float qualityPercent)
    {
        if (packet.score > 0 || repCount == 0)
            return Mathf.Max(0, packet.score);

        return Mathf.Max(0, (repCount * 120) + Mathf.RoundToInt(qualityPercent * 4f));
    }

    private static int ResolveLevel(DashboardPacket packet, int resolvedScore)
    {
        if (packet.level > 1 || resolvedScore == 0)
            return Mathf.Max(1, packet.level);

        return Mathf.Max(1, (resolvedScore / 1200) + 1);
    }

    private static int ResolveCombo(DashboardPacket packet, int repCount, float qualityPercent)
    {
        if (packet.combo > 0)
            return packet.combo;

        if (repCount <= 0)
            return 0;

        return qualityPercent >= 70f ? repCount : 0;
    }

    private static string ResolveRank(DashboardPacket packet, float qualityPercent)
    {
        if (!string.IsNullOrWhiteSpace(packet.rank) && (packet.rank != "BRONZE" || qualityPercent < 70f))
            return packet.rank;

        if (qualityPercent >= 95f) return "PLATINUM";
        if (qualityPercent >= 85f) return "GOLD";
        if (qualityPercent >= 70f) return "SILVER";
        return "BRONZE";
    }

    private static string[] ResolveAchievements(DashboardPacket packet, int repCount, float qualityPercent, int combo)
    {
        if (packet.achievements != null && packet.achievements.Length > 0)
            return packet.achievements;

        var local = new List<string>();
        if (repCount == 1) local.Add("FIRST_REP");
        if (repCount > 0 && repCount % 5 == 0) local.Add($"REPS_{repCount}");
        if (qualityPercent >= 95f && repCount > 0) local.Add("PERFECT_FORM");
        if (combo >= 3 && combo % 3 == 0) local.Add($"COMBO_{combo}");
        return local.ToArray();
    }

    private string PrettyRank(string rank)
    {
        if (string.IsNullOrWhiteSpace(rank))
            return "BRONZE";

        return rank.Trim().ToUpperInvariant();
    }

    private Color GetRankColor(string rank)
    {
        if (string.IsNullOrWhiteSpace(rank))
            return rankColor;

        string normalized = rank.Trim().ToUpperInvariant();
        if (normalized.Contains("DIAMOND")) return new Color(0.60f, 0.96f, 1f, 0.98f);
        if (normalized.Contains("PLATINUM")) return new Color(0.70f, 0.90f, 1f, 0.98f);
        if (normalized.Contains("GOLD")) return new Color(1f, 0.83f, 0.35f, 0.98f);
        if (normalized.Contains("SILVER")) return new Color(0.88f, 0.91f, 0.97f, 0.98f);
        if (normalized.Contains("BRONZE")) return new Color(0.85f, 0.66f, 0.44f, 0.98f);

        return rankColor;
    }

    private string FormatAchievements(string[] achievements)
    {
        if (achievements == null || achievements.Length == 0)
            return "None yet";

        int shown = Mathf.Min(achievements.Length, 3);
        List<string> labels = new List<string>(shown + 1);
        for (int i = 0; i < shown; i++)
        {
            string label = PrettyAchievement(achievements[i]);
            if (!string.IsNullOrWhiteSpace(label))
            {
                labels.Add(label);
            }
        }

        if (achievements.Length > shown)
        {
            labels.Add($"+{achievements.Length - shown}");
        }

        return string.Join(" • ", labels);
    }

    private string PrettyAchievement(string achievement)
    {
        if (string.IsNullOrWhiteSpace(achievement))
            return string.Empty;

        string normalized = achievement.Trim().Replace("_", " ").Replace("-", " ");
        string[] parts = normalized.Split(' ');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
                continue;

            parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1).ToLowerInvariant();
        }

        return string.Join(" ", parts);
    }

    private void ValidateBindings()
    {
        var missing = new List<string>();

        if (dashboardReceiver == null) missing.Add(nameof(dashboardReceiver));
        if (exerciseText == null) missing.Add(nameof(exerciseText));
        if (phaseText == null) missing.Add(nameof(phaseText));
        if (repsText == null) missing.Add(nameof(repsText));
        if (angleText == null) missing.Add(nameof(angleText));
        if (targetText == null) missing.Add(nameof(targetText));
        if (minText == null) missing.Add(nameof(minText));
        if (qualityText == null) missing.Add(nameof(qualityText));
        if (statusText == null) missing.Add(nameof(statusText));
        if (feedbackText == null) missing.Add(nameof(feedbackText));
        if (calibrationText == null) missing.Add(nameof(calibrationText));
        if (qualityBarFill == null) missing.Add(nameof(qualityBarFill));

        if (missing.Count > 0)
        {
            Debug.LogWarning($"[VRDashboardUIController] Missing bindings: {string.Join(", ", missing)}");
        }
    }
}

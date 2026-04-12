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
    [SerializeField] private float dashboardScale = 1.25f;
    [Tooltip("Scales all TMP font sizes for a larger, easier-to-read panel.")]
    [Range(0.8f, 2.2f)]
    [SerializeField] private float textScale = 1.0f;

    [Header("HUD Anchor")]
    [Tooltip("Pins this dashboard to a stable viewport position like the on-screen legend.")]
    [SerializeField] private bool pinToCameraView = true;
    [Tooltip("Optional camera override. If empty, Camera.main is used.")]
    [SerializeField] private Camera anchorCamera;
    [Tooltip("Viewport anchor position (0,0 bottom-left to 1,1 top-right).")]
    [SerializeField] private Vector2 viewportAnchor = new Vector2(0.24f, 0.56f);
    [Tooltip("Distance from camera while pinned in viewport mode.")]
    [Range(0.5f, 5f)]
    [SerializeField] private float cameraDistance = 1.45f;
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
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.46f);
    [SerializeField] private Color titleColor = new Color(1f, 1f, 1f, 0.96f);
    [SerializeField] private Color bodyColor = new Color(0.93f, 0.96f, 1f, 0.92f);
    [SerializeField] private Color mutedColor = new Color(0.78f, 0.84f, 0.92f, 0.92f);
    [SerializeField] private Color accentColor = new Color(0.29f, 0.94f, 0.56f, 0.98f);

    [Header("Text")]
    [SerializeField] private TMP_Text exerciseText;
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
    [SerializeField] private Color qualityGood = new Color(0.30f, 0.72f, 0.47f);
    [SerializeField] private Color qualityWarn = new Color(0.95f, 0.60f, 0.14f);
    [SerializeField] private Color qualityBad = new Color(0.91f, 0.30f, 0.30f);

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
    private RectTransform _resolvedRoot;
    private Image _unifiedCardImage;

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
        BuildStatCards();
        BuildUnifiedCard();
        RearrangeStats();
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
        RefreshCardGeometry();
    }

    private void HandleDashboardPacket(DashboardPacket packet)
    {
        if (packet == null) return;

        string prettyExercise = PrettyExercise(packet.exercise);
        float qualityPercent = ParsePercent(packet.formQuality);
        Color statusColor = GetStatusColor(packet.status);

        string phaseValue = string.IsNullOrWhiteSpace(packet.phase) ? "UNKNOWN" : packet.phase.ToUpperInvariant();
        string statusValue = string.IsNullOrWhiteSpace(packet.status) ? "WAITING" : packet.status.ToUpperInvariant();

        SetText(exerciseText, $"<b><size=122%>{prettyExercise.ToUpperInvariant()} DASHBOARD</size></b>");
        SetText(phaseText, $"<size=84%><color=#FFFFFFB8>PHASE</color></size>  <b>{phaseValue}</b>");
        SetText(repsText, $"<size=82%><color=#FFFFFFB8>REPS</color></size>  <b>{packet.repCount}</b>");
        SetText(angleText, $"<size=82%><color=#FFFFFFB8>CURRENT ANGLE</color></size>  <b>{Mathf.RoundToInt(packet.currentAngle)} deg</b>");
        SetText(targetText, $"<size=82%><color=#FFFFFFB8>TARGET</color></size>  <b>{Mathf.RoundToInt(packet.pushTarget)} deg</b>");
        SetText(minText, $"<size=82%><color=#FFFFFFB8>MIN THRESHOLD</color></size>  <b>{Mathf.RoundToInt(packet.minimumThreshold)} deg</b>");
        SetText(qualityText, $"<size=82%><color=#FFFFFFB8>FORM QUALITY</color></size>  <b><color=#{ColorToHex(GetQualityColor(qualityPercent))}>{Mathf.RoundToInt(qualityPercent)}%</color></b>");
        SetText(statusText, $"<size=84%><color=#FFFFFFCC>SYSTEM STATUS</color></size>  <b><color=#{ColorToHex(statusColor)}>[ {statusValue} ]</color></b>");
        string feedbackValue = string.IsNullOrWhiteSpace(packet.feedback)
            ? "Awaiting movement feedback..."
            : Truncate(packet.feedback.Trim(), maxFeedbackCharacters);
        SetText(feedbackText, $"<size=82%><color=#FFFFFFAA>COACH FEEDBACK</color></size>  <i>{feedbackValue}</i>");

        int done = packet.calibration != null ? packet.calibration.count : 0;
        int required = packet.calibration != null ? packet.calibration.required : 3;
        SetText(calibrationText, $"<size=82%><color=#FFFFFFB8>CALIBRATION</color></size>  <b>{done}/{required}</b>");

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

    private void ApplyTextTheme(TMP_Text text, Color color)
    {
        if (text == null)
            return;

        text.enableWordWrapping = false;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;

        if (_baseFontSizes.TryGetValue(text, out float baseSize))
        {
            text.fontSize = baseSize * textScale;
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
        TMP_Text[] texts = { exerciseText, phaseText, repsText, angleText, targetText, minText, qualityText, statusText, feedbackText, calibrationText };
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
        TMP_Text[] texts = { exerciseText, phaseText, repsText, angleText, targetText, minText, qualityText, statusText, feedbackText, calibrationText };
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

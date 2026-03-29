using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class VRDashboardUIController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private VRDashboardReceiver dashboardReceiver;

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

    private void HandleDashboardPacket(DashboardPacket packet)
    {
        if (packet == null) return;

        SetText(exerciseText, $"Exercise: {PrettyExercise(packet.exercise)}");
        SetText(phaseText, $"Phase: {packet.phase}");
        SetText(repsText, $"Reps: {packet.repCount}");
        SetText(angleText, $"Angle: {Mathf.RoundToInt(packet.currentAngle)} deg");
        SetText(targetText, $"Push: {Mathf.RoundToInt(packet.pushTarget)} deg");
        SetText(minText, $"Min: {Mathf.RoundToInt(packet.minimumThreshold)} deg");
        SetText(qualityText, $"Quality: {packet.formQuality}");
        SetText(statusText, packet.status);
        SetText(feedbackText, packet.feedback);

        int done = packet.calibration != null ? packet.calibration.count : 0;
        int required = packet.calibration != null ? packet.calibration.required : 3;
        SetText(calibrationText, $"Calibration: {done}/{required}");

        UpdateQualityBar(packet.formQuality);
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
            field.text = value;
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

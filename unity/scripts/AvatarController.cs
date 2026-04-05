using UnityEngine;
using UnityEngine.InputSystem;

public class AvatarController : MonoBehaviour
{
    [Header("UDP Source")]
    public UDPReceiver udpReceiver;

    [Header("IK Targets")]
    public Transform targetLeftHand;
    public Transform targetRightHand;
    public Transform targetLeftFoot;
    public Transform targetRightFoot;
    public Transform targetHips;

    [Header("Hip Reference (drag mixamorig:Hips here)")]
    public Transform avatarHip;

    [Header("Scale")]
    public float bodyScale = 2.5f;

    [Header("Arm Tuning")]
    public float armReferenceHeight = 0.45f;
    public float armLateralScale = 1.0f;
    public float armVerticalScale = 1.0f;
    public float armDepthScale = 1.0f;
    public float armVerticalOffset = -0.12f;
    public float armDepthOffset = 0.10f;
    [Tooltip("Use -1 to invert depth if arms move backwards when reaching forward.")]
    public float armDepthSign = 1.0f;

    [Header("Smoothing")]
    [Range(1f, 30f)]
    public float smoothing = 12f;

    [Header("Stability")]
    public bool disableAnimatorRootMotion = true;
    public bool useGroundYOverride = true;
    public float groundY = 1f;

    [Header("Squat / Jump Tracking")]
    public bool trackBodyHeight = true;
    public float rootHeightScale = 3.0f;
    public float jumpHeightScale = 10.0f;
    public float kneeBendScale = 0.1f;
    public float maxSquatDownOffset = 0.55f;
    public float maxRiseOffset = 1.0f;
    public bool trackHipTargetHeight = true;
    public float hipHeightScale = 1.8f;
    public float maxHipDownOffset = 0.40f;
    public float maxHipRiseOffset = 0.05f;

    [Header("Debug")]
    public bool showDebugLog = true;

    private Vector3 avatarStartPos;
    private Vector3 hipsTargetStartPos;
    private float standingHipY;
    private bool hasStandingCalibration;

    void Start()
    {
        if (disableAnimatorRootMotion)
        {
            Animator anim = GetComponent<Animator>();
            if (anim != null)
                anim.applyRootMotion = false;
        }

        avatarStartPos = transform.position;
        if (useGroundYOverride)
        {
            avatarStartPos.y = groundY;
            transform.position = avatarStartPos;
        }

        if (maxRiseOffset <= 0f)
            maxRiseOffset = 1.0f;

        if (targetHips != null)
            hipsTargetStartPos = targetHips.position;
    }

    void Update()
    {
        // // Press Space in Play mode to recalibrate standing pose
        // if (Input.GetKeyDown(KeyCode.Space))
        // {
        //     hasStandingCalibration = false;
        //     Debug.Log("Standing pose recalibrated!");
        // }

        #if UNITY_EDITOR
                // Press Space in Play mode to recalibrate standing pose
                if (UnityEngine.InputSystem.Keyboard.current != null &&
                    UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    hasStandingCalibration = false;
                    Debug.Log("Standing pose recalibrated!");
                }
        #endif

        if (udpReceiver == null || udpReceiver.pose == null)
            return;

        var p = udpReceiver.pose;

        if (!IsValid(p.left_hip) || !IsValid(p.right_hip))
            return;

        Vector3 mpHip = new Vector3(
            (p.left_hip[0] + p.right_hip[0]) / 2f,
            (p.left_hip[1] + p.right_hip[1]) / 2f,
            (p.left_hip[2] + p.right_hip[2]) / 2f
        );

        Vector3 mpShoulder = mpHip;
        if (IsValid(p.left_shoulder) && IsValid(p.right_shoulder))
        {
            mpShoulder = new Vector3(
                (p.left_shoulder[0] + p.right_shoulder[0]) / 2f,
                (p.left_shoulder[1] + p.right_shoulder[1]) / 2f,
                (p.left_shoulder[2] + p.right_shoulder[2]) / 2f
            );
        }

        // Calibrate on first valid frame after reset
        if (!hasStandingCalibration)
        {
            standingHipY = mpHip.y;
            hasStandingCalibration = true;
            Debug.Log($"Calibrated standingHipY = {standingHipY:F3}");
        }

        ApplySquatRootTracking(mpHip.y, p);

        Vector3 refPoint = avatarHip != null ? avatarHip.position : avatarStartPos + Vector3.up;
        Vector3 armRefPoint = refPoint + Vector3.up * armReferenceHeight;

        if (targetHips != null)
        {
            if (trackHipTargetHeight)
            {
                float hipOffsetY = -(mpHip.y - standingHipY) * hipHeightScale;
                hipOffsetY = Mathf.Clamp(hipOffsetY, -maxHipDownOffset, maxHipRiseOffset);

                Vector3 hipGoal = hipsTargetStartPos + new Vector3(0f, hipOffsetY, 0f);
                targetHips.position = Vector3.Lerp(targetHips.position, hipGoal, Time.deltaTime * smoothing);
            }
            else
            {
                targetHips.position = Vector3.Lerp(targetHips.position, refPoint, Time.deltaTime * smoothing);
            }
        }

        SmoothArmTarget(targetLeftHand,  p.left_wrist,  mpShoulder, armRefPoint);
        SmoothArmTarget(targetRightHand, p.right_wrist, mpShoulder, armRefPoint);
        SmoothLimbTarget(targetLeftFoot,  p.left_ankle,  mpHip, refPoint);
        SmoothLimbTarget(targetRightFoot, p.right_ankle, mpHip, refPoint);
    }

    bool IsValid(float[] lm)
    {
        return lm != null && lm.Length >= 3;
    }

    void SmoothArmTarget(Transform target, float[] lm, Vector3 mpShoulder, Vector3 worldRef)
    {
        if (target == null || !IsValid(lm))
            return;

        float dx = (lm[0] - mpShoulder.x) * bodyScale * armLateralScale;
        float dy = -(lm[1] - mpShoulder.y) * bodyScale * armVerticalScale + armVerticalOffset;
        float dz = (lm[2] - mpShoulder.z) * bodyScale * armDepthScale * armDepthSign + armDepthOffset;

        Vector3 goal = worldRef + new Vector3(dx, dy, dz);
        target.position = Vector3.Lerp(target.position, goal, Time.deltaTime * smoothing);
    }

    void SmoothLimbTarget(Transform target, float[] lm, Vector3 mpHip, Vector3 worldRef)
    {
        if (target == null || !IsValid(lm))
            return;

        float dx = (lm[0] - mpHip.x) * bodyScale;
        float dy = -(lm[1] - mpHip.y) * bodyScale;
        float dz = -(lm[2] - mpHip.z) * bodyScale;

        Vector3 goal = worldRef + new Vector3(dx, dy, dz);
        target.position = Vector3.Lerp(target.position, goal, Time.deltaTime * smoothing);
    }

    void ApplySquatRootTracking(float currentHipY, UDPReceiver.PoseData p)
    {
        if (!trackBodyHeight)
        {
            transform.position = avatarStartPos;
            return;
        }

        // MediaPipe Y: smaller = higher in real world
        // hipY decreases → you jumped up
        // hipY increases → you squatted down
        float jumpUp    = Mathf.Max(0f, standingHipY - currentHipY) * jumpHeightScale;
        float squatDown = Mathf.Max(0f, currentHipY - standingHipY) * rootHeightScale;
        float kneeDown  = GetKneeBendSignal(p) * kneeBendScale;

        float offsetY = jumpUp - squatDown - kneeDown;
        offsetY = Mathf.Clamp(offsetY, -maxSquatDownOffset, maxRiseOffset);

        if (showDebugLog)
            Debug.Log($"hipY={currentHipY:F3} standingY={standingHipY:F3} | jump={jumpUp:F3} squat={squatDown:F3} knee={kneeDown:F3} | offsetY={offsetY:F3}");

        Vector3 goal = new Vector3(avatarStartPos.x, avatarStartPos.y + offsetY, avatarStartPos.z);
        transform.position = Vector3.Lerp(transform.position, goal, Time.deltaTime * smoothing);
    }

    float GetKneeBendSignal(UDPReceiver.PoseData p)
    {
        if (!IsValid(p.left_hip)  || !IsValid(p.left_knee)  || !IsValid(p.left_ankle) ||
            !IsValid(p.right_hip) || !IsValid(p.right_knee) || !IsValid(p.right_ankle))
            return 0f;

        Vector3 lh = ToV3(p.left_hip);
        Vector3 lk = ToV3(p.left_knee);
        Vector3 la = ToV3(p.left_ankle);

        Vector3 rh = ToV3(p.right_hip);
        Vector3 rk = ToV3(p.right_knee);
        Vector3 ra = ToV3(p.right_ankle);

        float leftAngle  = CalculateAngleDeg(lh, lk, la);
        float rightAngle = CalculateAngleDeg(rh, rk, ra);
        float avgAngle   = (leftAngle + rightAngle) * 0.5f;

        float bend01 = Mathf.InverseLerp(170f, 90f, avgAngle);
        return Mathf.Clamp01(bend01);
    }

    Vector3 ToV3(float[] lm)
    {
        return new Vector3(lm[0], lm[1], lm[2]);
    }

    float CalculateAngleDeg(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ba = (a - b).normalized;
        Vector3 bc = (c - b).normalized;
        float dot  = Mathf.Clamp(Vector3.Dot(ba, bc), -1f, 1f);
        return Mathf.Acos(dot) * Mathf.Rad2Deg;
    }
}
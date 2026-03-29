// using UnityEngine;

// public class AvatarController : MonoBehaviour
// {
//     [Header("UDP Source")]
//     public UDPReceiver udpReceiver;

//     [Header("IK Targets")]
//     public Transform targetLeftHand;
//     public Transform targetRightHand;
//     public Transform targetLeftFoot;
//     public Transform targetRightFoot;
//     public Transform targetHips;

//     [Header("Hip Reference (drag mixamorig:Hips here)")]
//     public Transform avatarHip;

//     [Header("Scale")]
//     public float bodyScale = 2.5f;

//     [Header("Smoothing (higher = smoother but more lag)")]
//     [Range(1f, 30f)]
//     public float smoothing = 12f;

//     [Header("Body Movement (squat/jump)")]
//     public bool trackBodyHeight = true;
//     public float groundY = 1f;          // avatar Y when standing — match your X Bot Y position

//     private Vector3 avatarStartPos;

//     void Start()
//     {
//         avatarStartPos = transform.position;
//     }

//     void Update()
//     {
//         if (udpReceiver == null || udpReceiver.pose == null) return;
//         var p = udpReceiver.pose;

//         if (!IsValid(p.left_hip) || !IsValid(p.right_hip)) return;

//         Vector3 mpHip = new Vector3(
//             (p.left_hip[0] + p.right_hip[0]) / 2f,
//             (p.left_hip[1] + p.right_hip[1]) / 2f,
//             (p.left_hip[2] + p.right_hip[2]) / 2f
//         );

//         // Move avatar root up/down based on hip Y (squat/jump)
//         if (trackBodyHeight)
//         {
//             // mpHip.y in MediaPipe: 0=top, 1=bottom of frame
//             // When standing: mpHip.y ≈ 0.55, squatting: higher value, jumping: lower value
//             float heightOffset = -(mpHip.y - 0.55f) * bodyScale;
//             Vector3 targetRootPos = new Vector3(avatarStartPos.x, groundY + heightOffset, avatarStartPos.z);
//             transform.position = Vector3.Lerp(transform.position, targetRootPos, Time.deltaTime * smoothing);
//         }

//         Vector3 refPoint = avatarHip != null ? avatarHip.position : transform.position + Vector3.up;

//         if (targetHips != null) targetHips.position = refPoint;

//         SmoothTarget(targetLeftHand,  p.left_wrist,  mpHip, refPoint);
//         SmoothTarget(targetRightHand, p.right_wrist, mpHip, refPoint);
//         SmoothTarget(targetLeftFoot,  p.left_ankle,  mpHip, refPoint);
//         SmoothTarget(targetRightFoot, p.right_ankle, mpHip, refPoint);
//     }

//     bool IsValid(float[] lm) => lm != null && lm.Length >= 3;

//     void SmoothTarget(Transform target, float[] lm, Vector3 mpHip, Vector3 worldRef)
//     {
//         if (target == null || !IsValid(lm)) return;
//         float dx =  (lm[0] - mpHip.x) * bodyScale;
//         float dy = -(lm[1] - mpHip.y) * bodyScale;
//         float dz = -(lm[2] - mpHip.z) * bodyScale;
//         Vector3 goal = worldRef + new Vector3(dx, dy, dz);
//         target.position = Vector3.Lerp(target.position, goal, Time.deltaTime * smoothing);
//     }
// }

using UnityEngine;

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

    [Header("Smoothing")]
    [Range(1f, 30f)]
    public float smoothing = 12f;

    [Header("Stability")]
    public bool disableAnimatorRootMotion = true;
    public bool useGroundYOverride = true;
    public float groundY = 1f;

    [Header("Squat Tracking")]
    public bool trackBodyHeight = true;
    public float rootHeightScale = 8.0f;
    public float maxSquatDownOffset = 0.55f;
    public float maxRiseOffset = 0.40f;
    public bool enableJumpImpulse = true;
    public float jumpDetectThreshold = 0.025f;
    public float jumpImpulseHeight = 0.30f;
    public float jumpImpulseDecay = 3.5f;
    public bool trackHipTargetHeight = true;
    public float hipHeightScale = 1.8f;
    public float maxHipDownOffset = 0.40f;
    public float maxHipRiseOffset = 0.05f;

    private Vector3 avatarStartPos;
    private Vector3 hipsTargetStartPos;
    private float standingHipY;
    private bool hasStandingCalibration;
    private float jumpOffset;

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

        // Existing scene instances may have persisted maxRiseOffset=0 from older versions.
        if (maxRiseOffset <= 0f)
            maxRiseOffset = 0.20f;

        if (targetHips != null)
            hipsTargetStartPos = targetHips.position;
    }

    void Update()
    {
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

        if (!hasStandingCalibration)
        {
            standingHipY = mpHip.y;
            hasStandingCalibration = true;
        }

        ApplySquatRootTracking(mpHip.y);

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
        float dz = -(lm[2] - mpShoulder.z) * bodyScale * armDepthScale + armDepthOffset;

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

    void ApplySquatRootTracking(float currentHipY)
    {
        if (!trackBodyHeight)
        {
            transform.position = avatarStartPos;
            return;
        }

        // MediaPipe y: bigger value = body lower in frame (squat), smaller value = body higher (jump).
        float hipDelta = standingHipY - currentHipY;
        float offsetY = hipDelta * rootHeightScale;

        if (enableJumpImpulse)
        {
            if (hipDelta > jumpDetectThreshold)
                jumpOffset = Mathf.Max(jumpOffset, jumpImpulseHeight);

            if (jumpOffset > 0f)
                jumpOffset = Mathf.Max(0f, jumpOffset - Time.deltaTime * jumpImpulseDecay);

            offsetY += jumpOffset;
        }

        offsetY = Mathf.Clamp(offsetY, -maxSquatDownOffset, maxRiseOffset);

        Vector3 goal = new Vector3(avatarStartPos.x, avatarStartPos.y + offsetY, avatarStartPos.z);
        transform.position = Vector3.Lerp(transform.position, goal, Time.deltaTime * smoothing);
    }
}
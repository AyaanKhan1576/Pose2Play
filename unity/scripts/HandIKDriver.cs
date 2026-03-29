using UnityEngine;

public class HandIKDriver : MonoBehaviour
{
    [Header("XR Controllers (Action-based)")]
    public Transform leftController;
    public Transform rightController;

    [Header("IK Targets")]
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    [Header("Rotation Offsets (degrees)")]
    public Vector3 leftHandRotationOffset = new Vector3(25f, 0f, 0f);
    public Vector3 rightHandRotationOffset = new Vector3(25f, 0f, 0f);

    void LateUpdate()
    {
        if (leftController && leftHandTarget)
        {
            leftHandTarget.position = leftController.position;
            leftHandTarget.rotation =
                leftController.rotation * Quaternion.Euler(leftHandRotationOffset);
        }

        if (rightController && rightHandTarget)
        {
            rightHandTarget.position = rightController.position;
            rightHandTarget.rotation =
                rightController.rotation * Quaternion.Euler(rightHandRotationOffset);
        }
    }
}

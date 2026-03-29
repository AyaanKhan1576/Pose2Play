using UnityEngine;

/*public class AvatarBodyFollower : MonoBehaviour
{
    public Transform head;
    public float bodyHeightOffset = -0.9f;

    void LateUpdate()
    {
        if (!head) return;

        Vector3 pos = head.position;
        pos.y -= bodyHeightOffset;
        transform.position = pos;

        // Only rotate around Y (body doesn't tilt when head looks up/down)
        Vector3 euler = head.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }
}*/
using UnityEngine;

public class AvatarBodyFollower : MonoBehaviour
{
    public Transform head;              // Main Camera
    public float heightOffset = -1.4f;  // tweak until feet on ground

    void LateUpdate()
    {
        if (!head) return;

        // Follow head position, only adjust height
        Vector3 pos = transform.position;
        pos.x = head.position.x;
        pos.z = head.position.z;
        pos.y = head.position.y + heightOffset;
        transform.position = pos;

        // Only rotate with head yaw
        Vector3 euler = head.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }
}


/*using UnityEngine;

public class AvatarBodyFollower : MonoBehaviour
{
    public Transform head;        // Main Camera
    // Local space offset from the head: (x, y, z)
    // y negative = lower body, z positive = a bit in front
    public Vector3 bodyOffset = new Vector3(0f, -1.3f, 0.25f);

    void LateUpdate()
    {
        if (!head) return;

        // Position the avatar below & slightly in front of the head
        Vector3 pos = head.position + head.rotation * bodyOffset;
        transform.position = pos;

        // Only rotate around Y so body doesn't lean when you look up/down
        Vector3 euler = head.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }
}
*/
/*using UnityEngine;

public class AvatarBodyFollower : MonoBehaviour
{
    public Transform head;              // assign Main Camera here
    public Vector3 offset = new Vector3(0f, -1.3f, 1.15f);
    // y ~ -1.2 to -1.4 depending on avatar height

    void LateUpdate()
    {
        if (!head) return;

        // Position body just below the head
        transform.position = head.position + offset;

        // Only rotate with head yaw (no pitch/roll)
        Vector3 euler = head.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }
}*/

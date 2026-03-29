using UnityEngine;

public class HideHead : MonoBehaviour
{
    public Transform headBone;       // assign mixamorig:Head in Inspector
    public float scale = 0.001f;     // small, but not exactly 0

    void Start()
    {
        if (headBone != null)
        {
            headBone.localScale = new Vector3(scale, scale, scale);
        }
    }
}

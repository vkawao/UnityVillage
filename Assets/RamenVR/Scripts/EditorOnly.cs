using UnityEngine;

namespace RamenVR
{
    /// Removes this object in real builds (used on the XR Interaction Simulator,
    /// which is only for testing on the PC).
    public class EditorOnly : MonoBehaviour
    {
        void Awake()
        {
            if (!Application.isEditor) Destroy(gameObject);
        }
    }
}

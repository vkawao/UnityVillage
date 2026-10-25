using UnityEngine;

namespace RamenVR
{
    /// Always turns to face the player's camera (arrows, signs, click areas).
    /// With an anchor it also floats a little in front of that object, toward the player.
    public class FaceCamera : MonoBehaviour
    {
        public Transform anchor;
        public Vector3 anchorOffset;
        public float pushTowardCamera = 0f;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam) return;
            if (anchor)
            {
                Vector3 basePos = anchor.TransformPoint(anchorOffset);
                Vector3 toCam = cam.transform.position - basePos;
                transform.position = basePos + (toCam.sqrMagnitude > 0.0001f ? toCam.normalized * pushTowardCamera : Vector3.zero);
            }
            Vector3 d = transform.position - cam.transform.position; d.y = 0;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
        }
    }
}

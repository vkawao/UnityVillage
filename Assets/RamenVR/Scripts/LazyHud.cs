using UnityEngine;

namespace RamenVR
{
    /// Small panel that softly follows the player's view (lower part of the view).
    public class LazyHud : MonoBehaviour
    {
        public float distance = 1.1f;
        public float drop = 0.42f;
        public float speed = 3f;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam) return;
            Vector3 fwd = cam.transform.forward; fwd.y = 0;
            if (fwd.sqrMagnitude < 0.001f) return;
            fwd.Normalize();
            Vector3 target = cam.transform.position + fwd * distance + Vector3.down * drop;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * speed);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwd), Time.deltaTime * speed);
        }
    }
}

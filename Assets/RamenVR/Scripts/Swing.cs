using UnityEngine;

namespace RamenVR
{
    /// Rocks a target back and forth (swing seat, a tree being shaken).
    /// Starts when this object is switched on. duration 0 = keeps going.
    public class Swing : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Axis in the target's parent space")] public Vector3 localAxis = Vector3.right;
        public float angle = 25f;
        public float period = 2f;
        public float duration = 0f;
        Quaternion start; float t0; bool ready;

        void OnEnable()
        {
            if (!target) return;
            start = target.localRotation; t0 = Time.time; ready = true;
        }

        void Update()
        {
            if (!ready) return;
            float t = Time.time - t0;
            if (duration > 0 && t > duration) { target.localRotation = start; ready = false; enabled = false; return; }
            float fade = duration > 0 ? 1f - t / duration : 1f;
            target.localRotation = Quaternion.AngleAxis(Mathf.Sin(t * 2f * Mathf.PI / period) * angle * fade, localAxis) * start;
        }
    }
}

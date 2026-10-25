using System;
using System.Collections;
using UnityEngine;

namespace RamenVR
{
    /// Swings a door open around its own pivot (the hinge).
    public class HingeDoor : MonoBehaviour
    {
        [Tooltip("Degrees around world up. Sign decides inward/outward (set by the setup tool).")]
        public float openAngle = 95f;
        public float duration = 1.0f;

        bool isOpen;

        public void Open(Action onDone = null)
        {
            if (isOpen) { onDone?.Invoke(); return; }
            isOpen = true;
            StartCoroutine(Swing(onDone));
        }

        IEnumerator Swing(Action onDone)
        {
            Quaternion start = transform.rotation;
            Quaternion end = Quaternion.AngleAxis(openAngle, Vector3.up) * start;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / duration);
                transform.rotation = Quaternion.Slerp(start, end, k);
                yield return null;
            }
            transform.rotation = end;
            onDone?.Invoke();
        }
    }
}

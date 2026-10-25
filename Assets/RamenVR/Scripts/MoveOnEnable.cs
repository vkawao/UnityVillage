using System.Collections;
using UnityEngine;

namespace RamenVR
{
    /// When switched on, glides each target towards a destination (e.g. ducks swimming to the bread).
    public class MoveOnEnable : MonoBehaviour
    {
        public Transform[] targets;
        public Transform destination;
        public float stopShort = 0.3f;
        public float duration = 2.5f;

        void OnEnable() { StartCoroutine(Go()); }

        IEnumerator Go()
        {
            if (destination == null || targets == null) yield break;
            var from = new Vector3[targets.Length]; var to = new Vector3[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                if (!targets[i]) continue;
                from[i] = targets[i].position;
                Vector3 d = destination.position - from[i]; d.y = 0;
                Vector3 side = Vector3.Cross(Vector3.up, d.normalized) * (i - (targets.Length - 1) * 0.5f) * 0.3f;
                to[i] = from[i] + d - d.normalized * stopShort + side;
            }
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / duration);
                for (int i = 0; i < targets.Length; i++) if (targets[i]) targets[i].position = Vector3.Lerp(from[i], to[i], k);
                yield return null;
            }
            for (int i = 0; i < targets.Length; i++) if (targets[i]) targets[i].position = to[i];
        }
    }
}

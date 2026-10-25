using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RamenVR
{
    /// Makes plants grow from small to full size when this object is switched on
    /// (the setup turns it on when the plot is watered). Grown plants can then be picked.
    public class Grow : MonoBehaviour
    {
        public Transform[] targets;
        public Vector3[] fullScales;
        public float duration = 1.5f;

        void OnEnable() => StartCoroutine(Run());

        IEnumerator Run()
        {
            if (targets == null) yield break;
            var start = new Vector3[targets.Length];
            for (int i = 0; i < targets.Length; i++) if (targets[i]) start[i] = targets[i].localScale;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / duration);
                for (int i = 0; i < targets.Length; i++)
                    if (targets[i]) targets[i].localScale = Vector3.Lerp(start[i], fullScales[i], k);
                yield return null;
            }
            for (int i = 0; i < targets.Length; i++)
            {
                if (!targets[i]) continue;
                targets[i].localScale = fullScales[i];
                if (targets[i].TryGetComponent<XRGrabInteractable>(out var g)) g.enabled = true;   // ready to pick
            }
        }
    }
}

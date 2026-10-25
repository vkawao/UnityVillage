using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace RamenVR
{
    /// Let go of the balloon and it floats away into the sky (finishes a step).
    [RequireComponent(typeof(XRGrabInteractable))]
    public class BalloonRelease : MonoBehaviour
    {
        public int stepIndex = -1;
        public int[] requiredSteps;
        public string notReadyHint = "";
        public float riseSpeed = 0.8f;
        public float lifetime = 9f;
        bool gone;

        void Awake()
        {
            var grab = GetComponent<XRGrabInteractable>();
            grab.selectExited.AddListener(a =>
            {
                if (gone || a.interactorObject is XRSocketInteractor) return;
                var gm = RamenGameManager.For(this);
                if (gm != null && !gm.AllDone(requiredSteps)) { if (!string.IsNullOrEmpty(notReadyHint)) gm.Hint(notReadyHint); return; }
                gone = true;
                if (gm != null && stepIndex >= 0) gm.Complete(stepIndex);
                StartCoroutine(FlyAway(grab));
            });
        }

        IEnumerator FlyAway(XRGrabInteractable grab)
        {
            yield return null;
            if (grab) grab.enabled = false;
            if (TryGetComponent<Rigidbody>(out var rb)) { rb.isKinematic = true; rb.useGravity = false; }
            Vector3 p0 = transform.position;
            for (float t = 0; t < lifetime; t += Time.deltaTime)
            {
                transform.position = p0 + Vector3.up * riseSpeed * t + new Vector3(Mathf.Sin(t * 1.3f), 0, Mathf.Cos(t * 0.9f)) * 0.15f;
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}

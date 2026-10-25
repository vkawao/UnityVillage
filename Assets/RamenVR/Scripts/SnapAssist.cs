using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace RamenVR
{
    /// Makes placing easy: when the player lets go of an item near the right spot,
    /// it snaps into that spot even if it wasn't exactly inside it.
    [RequireComponent(typeof(XRGrabInteractable))]
    public class SnapAssist : MonoBehaviour
    {
        public float snapDistance = 0.4f;

        XRGrabInteractable grab;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            grab.selectExited.AddListener(OnReleased);
        }

        void OnReleased(SelectExitEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor) return;   // only when a hand lets go
            StartCoroutine(SnapNextFrame());
        }

        IEnumerator SnapNextFrame()
        {
            yield return null;
            if (grab == null || grab.isSelected) yield break;

            RamenSocket best = null;
            float bestDist = snapDistance;
            foreach (var s in FindObjectsByType<RamenSocket>(FindObjectsSortMode.None))
            {
                if (!s.isActiveAndEnabled || s.hasSelection || !s.AcceptsItem(transform)) continue;
                if (RamenGameManager.For(s) != RamenGameManager.For(this)) continue;   // only spots of the same shop
                float d = Vector3.Distance(s.transform.position, transform.position);
                if (d < bestDist) { bestDist = d; best = s; }
            }
            if (best != null && best.interactionManager != null)
                best.interactionManager.SelectEnter((IXRSelectInteractor)best, (IXRSelectInteractable)grab);
        }
    }
}

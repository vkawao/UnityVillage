using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace RamenVR
{
    /// Items wait still where they are (in the soil, on the table) until grabbed.
    /// When the player lets go in the air they fall down instead of floating.
    [RequireComponent(typeof(XRGrabInteractable))]
    public class DropOnRelease : MonoBehaviour
    {
        void Awake()
        {
            var grab = GetComponent<XRGrabInteractable>();
            grab.selectExited.AddListener(a =>
            {
                if (a.interactorObject is XRSocketInteractor) return;
                if (isActiveAndEnabled) StartCoroutine(Drop(grab));
            });
        }

        IEnumerator Drop(XRGrabInteractable grab)
        {
            yield return null;
            if (grab == null || grab.isSelected) yield break;
            if (!TryGetComponent<Rigidbody>(out var rb)) yield break;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;   // lands upright
        }
    }
}

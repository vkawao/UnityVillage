using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRVillage
{
    /// <summary>
    /// A socket that only accepts the listed items, so a hip holster never grabs
    /// other houses' objects (or this house's cans) as you walk past them.
    /// </summary>
    public class HolsterSocket : XRSocketInteractor
    {
        [Tooltip("Only these items can be stored here.")]
        [SerializeField] private List<XRGrabInteractable> m_Accepts = new List<XRGrabInteractable>();

        public override bool CanHover(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRHoverInteractable interactable) =>
            Accepts(interactable as XRGrabInteractable) && base.CanHover(interactable);

        public override bool CanSelect(IXRSelectInteractable interactable) =>
            Accepts(interactable as XRGrabInteractable) && base.CanSelect(interactable);

        /// <summary>The items this socket takes.</summary>
        public IReadOnlyList<XRGrabInteractable> AcceptedItems => m_Accepts;

        public bool Accepts(XRGrabInteractable grab) => grab != null && m_Accepts.Contains(grab);
    }
}

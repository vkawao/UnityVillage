using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRVillage
{
    public static class GrabUtil
    {
        /// <summary>True when a hand/controller (not a socket) is holding the interactable.</summary>
        public static bool IsHeldByHand(XRGrabInteractable grab)
        {
            if (grab == null || !grab.isSelected) return false;
            var selectors = grab.interactorsSelecting;
            for (int i = 0; i < selectors.Count; i++)
            {
                if (!(selectors[i] is XRSocketInteractor)) return true;
            }
            return false;
        }
    }
}

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// An item presented frozen in place (kinematic, e.g. a card standing in a slot) that becomes
    /// a normal physics item once it's been picked up. Without this, the grab would restore
    /// "kinematic" on release and the item would hang in the air.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class FreeAfterGrab : MonoBehaviour
    {
        private XRGrabInteractable m_Grab;
        private Rigidbody m_Body;

        private void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Body = GetComponent<Rigidbody>();
            m_Grab.selectExited.AddListener(OnReleased);
        }

        private void OnDestroy()
        {
            if (m_Grab != null) m_Grab.selectExited.RemoveListener(OnReleased);
        }

        /// <summary>Freeze the item where it is until it's grabbed.</summary>
        public void Present()
        {
            if (m_Body == null) return;
            m_Body.isKinematic = true;
            m_Body.useGravity = true;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            // Runs after the grab has restored the body; a socket that takes it next sets its own state.
            if (m_Body == null || m_Grab.isSelected) return;
            m_Body.isKinematic = false;
            m_Body.useGravity = true;
        }
    }
}

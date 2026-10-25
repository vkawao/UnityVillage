using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Something that can sit on a plate (a cup, a cake). Knows its bottom point and how it
    /// stands upright. Items on a shelf can start "resting" (held still, no gravity) until
    /// first picked up, e.g. cakes on the glass shelves of a pastry case.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class PlateItem : MonoBehaviour
    {
        [Tooltip("Bottom centre of the item (a child).")]
        [SerializeField] private Transform m_Base;
        [Tooltip("World rotation of the item standing upright (as modelled).")]
        [SerializeField] private Quaternion m_Upright = Quaternion.identity;
        [Tooltip("Hold still until first picked up (for items on shelves without colliders).")]
        [SerializeField] private bool m_StartResting;

        private XRGrabInteractable m_Grab;
        private Rigidbody m_Body;
        private bool m_Grabbed;

        public XRGrabInteractable Grab => m_Grab;
        public Rigidbody Body => m_Body;
        public Transform Base => m_Base != null ? m_Base : transform;
        public Quaternion Upright => m_Upright;

        /// <summary>The plate it's sitting on, or null.</summary>
        public PlateHolder Plate { get; set; }

        private void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Body = GetComponent<Rigidbody>();
            m_Grab.selectEntered.AddListener(OnGrabbed);
            if (m_StartResting && m_Body != null) m_Body.isKinematic = true;
        }

        private void OnDestroy()
        {
            if (m_Grab != null) m_Grab.selectEntered.RemoveListener(OnGrabbed);
        }

        private void OnGrabbed(SelectEnterEventArgs args) => m_Grabbed = true;

        private void Update()
        {
            // Let go of (and not on a plate or in a socket): it falls like anything else.
            // The grab restores the "kinematic" it had when picked up, so undo that here.
            if (!m_Grabbed || m_Body == null || !m_Body.isKinematic || Plate != null || m_Grab.isSelected) return;
            m_Body.isKinematic = false;
        }
    }
}

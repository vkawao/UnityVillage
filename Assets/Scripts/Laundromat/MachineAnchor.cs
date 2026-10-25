using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// Keeps this object (a panel anchor, a "Here!" marker, a zone) at the same spot relative to the
    /// booked washer or dryer as it has relative to the reference machine (number 1) in the editor.
    /// Place it by hand around machine 1; at runtime it follows the player's booking.
    /// </summary>
    public class MachineAnchor : MonoBehaviour
    {
        [SerializeField] private QueueStep m_Queue;
        [SerializeField] private bool m_Dryer;
        [Tooltip("The machine it was placed around in the editor.")]
        [SerializeField] private Transform m_Reference;
        [Tooltip("Follow sideways only (floor zones, and things by the dryer stack that shouldn't jump up for a top dryer).")]
        [SerializeField] private bool m_KeepHeight;

        private Vector3 m_LocalPos;
        private Quaternion m_LocalRot;
        private LaundryMachine m_At;

        private void Awake()
        {
            if (m_Reference == null) return;
            m_LocalPos = m_Reference.InverseTransformPoint(transform.position);
            m_LocalRot = Quaternion.Inverse(m_Reference.rotation) * transform.rotation;
        }

        private void LateUpdate()
        {
            if (m_Queue == null || m_Reference == null) return;
            LaundryMachine machine = m_Queue.Machine(m_Dryer);
            if (machine == null || machine == m_At) return;
            m_At = machine;
            Transform t = machine.transform;
            Vector3 p = t.TransformPoint(m_LocalPos);
            if (m_KeepHeight) p.y = transform.position.y;
            transform.SetPositionAndRotation(p, t.rotation * m_LocalRot);
        }
    }
}

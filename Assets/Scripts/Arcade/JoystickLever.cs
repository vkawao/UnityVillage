using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// An arcade joystick: grab it (hand, or point + grip) and move your hand left/right; the stick
    /// tilts that way and <see cref="Value"/> goes from -1 (left) to 1 (right). Let go and it springs
    /// back to the middle. The stick turns about its own origin (its base).
    /// </summary>
    public class JoystickLever : MonoBehaviour
    {
        [SerializeField] private XRSimpleInteractable m_Interactable;
        [Tooltip("World direction toward the player (the stick tilts about this axis).")]
        [SerializeField] private Vector3 m_Front = Vector3.back;
        [Tooltip("Hand movement (m) for a full tilt.")]
        [SerializeField] private float m_Throw = 0.1f;
        [SerializeField] private float m_MaxAngle = 25f;
        [SerializeField] private float m_SpringBack = 10f;

        private Quaternion m_Rest;
        private Vector3 m_GrabHand;
        private float m_GrabValue;
        private bool m_WasHeld;

        /// <summary>-1 = full left, 1 = full right, as seen by the player.</summary>
        public float Value { get; private set; }
        public bool Held => m_Interactable != null && m_Interactable.isSelected;

        private Vector3 Right => Vector3.Cross(m_Front.normalized, Vector3.up); // the player's right (they look along -front)

        private void Awake()
        {
            m_Rest = transform.rotation;
        }

        private void Update()
        {
            bool held = Held;
            if (held)
            {
                Vector3 hand = m_Interactable.interactorsSelecting[0].transform.position;
                if (!m_WasHeld)
                {
                    m_GrabHand = hand;
                    m_GrabValue = Value;
                }
                // Relative to where the grab started, so pointing at it with a ray works too.
                Value = Mathf.Clamp(m_GrabValue + Vector3.Dot(hand - m_GrabHand, Right) / m_Throw, -1f, 1f);
            }
            else
            {
                Value = Mathf.MoveTowards(Value, 0f, m_SpringBack * Time.deltaTime);
            }
            m_WasHeld = held;
            // A positive turn about the front axis moves the top of the stick to the player's right.
            transform.rotation = Quaternion.AngleAxis(Value * m_MaxAngle, m_Front.normalized) * m_Rest;
        }
    }
}

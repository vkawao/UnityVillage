using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// A box volume (slot, bin opening, card reader...) that can be asked whether a given
    /// item is inside it right now. Stateless: it only checks when asked, so it works for
    /// items that are held, thrown or teleported.
    /// Two tests, either one counts:
    ///   - geometric: the item's collider boxes overlap this box (plus a small tolerance),
    ///     which works for tiny/thin items like coins and cards;
    ///   - physics: Physics.OverlapBox finds one of the item's colliders.
    /// </summary>
    public class ItemTrigger : MonoBehaviour
    {
        [Tooltip("Size of the box (local space, centred on this object).")]
        [SerializeField] private Vector3 m_Size = new Vector3(0.2f, 0.1f, 0.1f);
        [Tooltip("Extra reach (m) around the box, so brushing the slot counts.")]
        [SerializeField] private float m_Tolerance = 0.02f;
        [Tooltip("Log how far an item is when it's close but not counted (helps placing the box).")]
        [SerializeField] private bool m_LogNearMisses = true;
        [SerializeField] private Color m_GizmoColor = new Color(1f, 0.6f, 0.2f, 0.6f);

        private static readonly Collider[] s_Hits = new Collider[32];
        private float m_NextLog;

        public Vector3 Size => m_Size;

        /// <summary>True if <paramref name="body"/> is touching/inside the box.</summary>
        public bool Contains(Rigidbody body)
        {
            if (body == null || !body.gameObject.activeInHierarchy) return false;
            if (GeometricOverlap(body, out float gap)) return true;
            if (PhysicsOverlap(body)) return true;

            if (m_LogNearMisses && gap < 0.25f && Time.time >= m_NextLog)
            {
                m_NextLog = Time.time + 1f;
                Debug.Log($"[Quest] {body.name} is {gap * 100f:0} cm from {name} (not counted yet).", this);
            }
            return false;
        }

        /// <summary>
        /// Compare in this box's local space: each collider's world bounds (8 corners) against
        /// the box grown by the tolerance. <paramref name="gap"/> = closest distance found.
        /// </summary>
        private bool GeometricOverlap(Rigidbody body, out float gap)
        {
            gap = float.MaxValue;
            Vector3 half = m_Size * 0.5f + Vector3.one * m_Tolerance;
            foreach (Collider c in body.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c.isTrigger) continue;
                Bounds wb = c.bounds;
                Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = wb.center + Vector3.Scale(wb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 l = transform.InverseTransformPoint(corner);
                    min = Vector3.Min(min, l);
                    max = Vector3.Max(max, l);
                }
                // Distance between the two local boxes along each axis (0 when they overlap on that axis).
                Vector3 d = new Vector3(
                    Mathf.Max(0f, Mathf.Max(min.x - half.x, -half.x - max.x)),
                    Mathf.Max(0f, Mathf.Max(min.y - half.y, -half.y - max.y)),
                    Mathf.Max(0f, Mathf.Max(min.z - half.z, -half.z - max.z)));
                float dist = d.magnitude;
                if (dist <= 0f) { gap = 0f; return true; }
                gap = Mathf.Min(gap, dist);
            }
            return false;
        }

        private bool PhysicsOverlap(Rigidbody body)
        {
            int count = Physics.OverlapBoxNonAlloc(transform.position, m_Size * 0.5f, s_Hits,
                transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (s_Hits[i].attachedRigidbody == body) return true;
            }
            return false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = m_GizmoColor;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, m_Size);
        }
    }
}

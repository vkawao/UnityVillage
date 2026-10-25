using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// The box of space on top of the counter. Polls for CounterItems inside it.
    /// An item counts as "placed" when it's inside, not held in a hand and roughly at rest.
    /// </summary>
    public class CounterZone : MonoBehaviour
    {
        [Tooltip("Size of the detection box (local space, centred on this object).")]
        [SerializeField] private Vector3 m_Size = new Vector3(1.5f, 0.8f, 0.7f);
        [SerializeField] private float m_PollInterval = 0.15f;
        [Tooltip("An item moving faster than this (m/s) isn't considered placed yet.")]
        [SerializeField] private float m_RestSpeed = 0.4f;

        private readonly List<CounterItem> m_Items = new List<CounterItem>();
        private readonly HashSet<CounterItem> m_Scratch = new HashSet<CounterItem>();
        private readonly Collider[] m_Hits = new Collider[64];
        private float m_NextPoll;

        /// <summary>Items currently inside the zone (held or not).</summary>
        public IReadOnlyList<CounterItem> Items => m_Items;

        /// <summary>Raised when the set of items in the zone changes.</summary>
        public event Action<CounterZone> ItemsChanged;

        private void Update()
        {
            if (Time.time < m_NextPoll) return;
            m_NextPoll = Time.time + m_PollInterval;
            Poll();
        }

        private void Poll()
        {
            m_Scratch.Clear();
            int count = Physics.OverlapBoxNonAlloc(transform.position, m_Size * 0.5f, m_Hits,
                transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody rb = m_Hits[i].attachedRigidbody;
                CounterItem item = rb != null ? rb.GetComponent<CounterItem>() : m_Hits[i].GetComponentInParent<CounterItem>();
                if (item != null && item.isActiveAndEnabled) m_Scratch.Add(item);
            }

            bool changed = m_Scratch.Count != m_Items.Count;
            if (!changed)
            {
                foreach (CounterItem item in m_Items)
                {
                    if (!m_Scratch.Contains(item)) { changed = true; break; }
                }
            }
            if (!changed) return;

            m_Items.Clear();
            m_Items.AddRange(m_Scratch);
            m_Items.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            ItemsChanged?.Invoke(this);
        }

        public bool Contains(CounterItem item) => item != null && m_Items.Contains(item);

        /// <summary>Inside the zone, let go of, and settled.</summary>
        public bool IsPlaced(CounterItem item)
        {
            if (!Contains(item) || GrabUtil.IsHeldByHand(item.Grab)) return false;
            return item.Body == null || item.Body.isKinematic || item.Body.linearVelocity.magnitude < m_RestSpeed;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, m_Size);
        }
    }
}

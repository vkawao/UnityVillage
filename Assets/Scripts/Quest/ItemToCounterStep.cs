using System.Collections.Generic;
using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// Action: bring an item to the cashier. Completes when any one of the candidate
    /// items is placed (let go of and settled) on the counter.
    /// </summary>
    public class ItemToCounterStep : QuestStep
    {
        [SerializeField] protected CounterZone m_Zone;

        [Tooltip("Any one of these on the counter completes the step.")]
        [SerializeField] private CounterItem[] m_Items;

        [Tooltip("Guide arrow points at the item itself until it's picked up (otherwise at the panel).")]
        [SerializeField] private bool m_PointAtItem;

        protected virtual IEnumerable<CounterItem> Candidates => m_Items;

        /// <summary>Item (or panel) until something is picked up, then the counter.</summary>
        public override Transform GuideTarget
        {
            get
            {
                foreach (CounterItem item in Candidates)
                {
                    if (item != null && m_Zone != null && GrabUtil.IsHeldByHand(item.Grab)) return m_Zone.transform;
                }
                if (m_PointAtItem)
                {
                    foreach (CounterItem item in Candidates)
                    {
                        if (item != null && item.isActiveAndEnabled) return item.transform;
                    }
                }
                return base.GuideTarget;
            }
        }

        protected override void OnBegin()
        {
            if (m_Zone == null) Debug.LogWarning("[Quest] ItemToCounterStep has no CounterZone.", this);
        }

        private void Update()
        {
            if (!IsActive || m_Zone == null) return;
            foreach (CounterItem item in Candidates)
            {
                if (item != null && m_Zone.IsPlaced(item))
                {
                    Complete();
                    return;
                }
            }
        }
    }
}

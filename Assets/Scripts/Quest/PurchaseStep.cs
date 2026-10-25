using System.Collections.Generic;
using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// Action: pay at the cashier. The Purchase button only unlocks once all the chosen
    /// gear is resting on the counter: every required item, at least one of the
    /// "any one of" items (the pedals) and the working amp. Pressing it makes everything
    /// on the counter vanish, plays a cash register sound, and completes (green fanfare
    /// via the step's Celebration).
    /// </summary>
    public class PurchaseStep : QuestStep
    {
        [SerializeField] private CashierUI m_Cashier;
        [SerializeField] private AmpTestStep m_AmpTest;
        [Tooltip("Each of these must be on the counter (e.g. the Les Paul).")]
        [SerializeField] private CounterItem[] m_Required;
        [Tooltip("At least one of these must be on the counter (e.g. the pedals).")]
        [SerializeField] private CounterItem[] m_AnyOneOf;
        [Tooltip("How the 'any one of' group is named in the missing list.")]
        [SerializeField] private string m_AnyOneOfLabel = "an effect pedal";
        [SerializeField, Range(0f, 1f)] private float m_RegisterVolume = 1f;
        [Tooltip("Optional: small sparkle where each item vanishes.")]
        [SerializeField] private QuestCelebration m_VanishSparkle;

        private readonly List<string> m_Missing = new List<string>();
        private Transform m_FirstMissing;
        private bool m_Ready;

        /// <summary>Points at the first item that still has to go on the counter, else the cashier.</summary>
        public override Transform GuideTarget => IsActive && m_FirstMissing != null ? m_FirstMissing : base.GuideTarget;

        protected override void OnBegin()
        {
            if (m_Cashier == null)
            {
                Debug.LogWarning("[Quest] PurchaseStep has no CashierUI.", this);
                return;
            }
            m_Cashier.Purchased += OnPurchased;
            m_Ready = false;
            m_Cashier.SetPurchaseEnabled(false);
            Refresh(true);
        }

        protected override void OnEnd()
        {
            if (m_Cashier == null) return;
            m_Cashier.Purchased -= OnPurchased;
        }

        private void Update()
        {
            if (IsActive && m_Cashier != null) Refresh(false);
        }

        private void Refresh(bool force)
        {
            CounterZone zone = m_Cashier.Zone;
            m_Missing.Clear();
            m_FirstMissing = null;

            if (m_Required != null)
            {
                foreach (CounterItem item in m_Required) Check(zone, item, item != null ? item.DisplayName : null);
            }

            if (m_AnyOneOf != null && m_AnyOneOf.Length > 0)
            {
                bool any = false;
                CounterItem fallback = null;
                foreach (CounterItem item in m_AnyOneOf)
                {
                    if (item == null || !item.isActiveAndEnabled) continue;
                    fallback ??= item;
                    if (zone != null && zone.IsPlaced(item)) { any = true; break; }
                }
                if (!any) AddMissing(m_AnyOneOfLabel, HeldOrFirst(m_AnyOneOf, fallback));
            }

            AmpUnit amp = m_AmpTest != null ? m_AmpTest.WorkingAmp : null;
            if (amp != null) Check(zone, amp.GetComponent<CounterItem>(), "the working amp");

            bool ready = m_Missing.Count == 0;
            if (ready != m_Ready || force)
            {
                m_Ready = ready;
                m_Cashier.SetPurchaseEnabled(ready);
            }

            SetProgress(ready
                ? "<color=#6EE787>Everything's on the counter. Press <b>Purchase</b>!</color>"
                : $"Put on the counter first: <color=#E6E6EA>{string.Join(", ", m_Missing)}</color>");
        }

        private void Check(CounterZone zone, CounterItem item, string label)
        {
            if (item == null || !item.isActiveAndEnabled) return;
            if (zone == null || !zone.IsPlaced(item)) AddMissing(label, item.transform);
        }

        private void AddMissing(string label, Transform where)
        {
            m_Missing.Add(label);
            if (m_FirstMissing == null) m_FirstMissing = where;
        }

        /// <summary>For the arrow: the pedal you're holding (it needs to go to the counter), else any pedal.</summary>
        private Transform HeldOrFirst(CounterItem[] items, CounterItem fallback)
        {
            foreach (CounterItem item in items)
            {
                if (item != null && GrabUtil.IsHeldByHand(item.Grab)) return item.transform;
            }
            return fallback != null ? fallback.transform : null;
        }

        private void OnPurchased()
        {
            if (!IsActive) return;
            Refresh(false);
            if (!m_Ready) return; // something was knocked off the counter in the same frame

            // Everything on the counter is bought, including any extra guitars put there.
            var gear = new List<CounterItem>(m_Cashier.Zone.Items);
            int paid = 0;
            foreach (CounterItem item in gear)
            {
                if (item == null || !item.gameObject.activeInHierarchy) continue;
                paid += item.Price;
                if (m_VanishSparkle != null) m_VanishSparkle.Sparkle(item.transform.position, 25);
                item.gameObject.SetActive(false);
            }

            AudioSource.PlayClipAtPoint(SynthAudio.CashRegister, m_Cashier.transform.position, m_RegisterVolume);
            m_Cashier.ShowThankYou(paid);
            Complete();
        }
    }
}

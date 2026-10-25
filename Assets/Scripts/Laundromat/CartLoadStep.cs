using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: bring a laundry cart to the washer. Once a cart stands in the zone in front of the booked
    /// washer, the wet shirts move out of the drum into it one by one (shirts can also be put in by hand).
    /// Completes when every shirt is in that cart.
    /// </summary>
    public class CartLoadStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string OffColor = "#8A8A92";
        private const string TextColor = "#E6E6EA";

        [SerializeField] private QueueStep m_Queue;
        [Tooltip("The carts (their laundry containers).")]
        [SerializeField] private LaundryContainer[] m_Carts;
        [SerializeField] private LaundryItem[] m_Items;
        [Tooltip("In front of the booked washer (follows the booking).")]
        [SerializeField] private ItemTrigger m_Zone;
        [Tooltip("Seconds between shirts moving from the washer into the cart.")]
        [SerializeField] private float m_LoadInterval = 0.25f;

        private int m_LastState = -1;
        private float m_NextLoad;

        /// <summary>The cart the shirts went into.</summary>
        public LaundryContainer ChosenCart { get; private set; }

        public override Transform GuideTarget
        {
            get
            {
                if (m_Zone == null || m_Carts == null) return base.GuideTarget;
                LaundryContainer here = CartInZone();
                if (here == null)
                {
                    foreach (LaundryContainer c in m_Carts)
                    {
                        var g = c != null ? c.GetComponent<XRGrabInteractable>() : null;
                        if (g != null && GrabUtil.IsHeldByHand(g)) return m_Zone.transform;
                    }
                    return NearestCart();
                }
                return here.transform;
            }
        }

        protected override void OnBegin()
        {
            m_LastState = -1;
            if (m_Carts == null) return;
            foreach (LaundryContainer c in m_Carts)
                if (c != null) c.Accepting = true;
        }

        private void Update()
        {
            if (!IsActive || m_Zone == null || m_Carts == null || m_Items == null) return;
            LaundryContainer here = CartInZone();
            int count = 0;
            foreach (LaundryContainer c in m_Carts) count = Mathf.Max(count, Count(c));
            int total = m_Items.Length;
            if (here != null && Count(here) < total && Time.time >= m_NextLoad) LoadOne(here);
            if (here != null && Count(here) >= total)
            {
                ChosenCart = here;
                SetProgress($"<color={OnColor}>All loaded!</color>");
                Complete();
                return;
            }

            int state = (here != null ? 1000 : 0) + count;
            if (state == m_LastState) return;
            m_LastState = state;
            string hint = here == null
                ? "Push a cart over to the \"Here!\" marker in front of your washer"
                : "Loading the shirts into the cart...";
            SetProgress(
                $"<color={(here != null ? OnColor : OffColor)}><b>CART</b> {(here != null ? "HERE" : "-")}</color>" +
                $"<space=1.2em><color={(count >= total ? OnColor : OffColor)}><b>SHIRTS</b> {count}/{total}</color>\n" +
                $"<color={TextColor}><i>{hint}</i></color>");
        }

        /// <summary>Move the next shirt that isn't in the cart (from the drum first) into it.</summary>
        private void LoadOne(LaundryContainer cart)
        {
            m_NextLoad = Time.time + m_LoadInterval;
            LaundryMachine washer = m_Queue != null ? m_Queue.Machine(false) : null;
            LaundryItem next = null;
            foreach (LaundryItem item in m_Items)
            {
                if (item == null || cart.Holds(item) || !item.gameObject.activeInHierarchy || GrabUtil.IsHeldByHand(item.Grab)) continue;
                if (washer != null && washer.Load != null && washer.Load.Holds(item)) { next = item; break; }
                if (next == null) next = item;
            }
            if (next != null) cart.Add(next);
        }

        private LaundryContainer CartInZone()
        {
            foreach (LaundryContainer c in m_Carts)
            {
                if (c == null) continue;
                Rigidbody body = c.GetComponent<Rigidbody>();
                if (body != null && m_Zone.Contains(body)) return c;
            }
            return null;
        }

        private Transform NearestCart()
        {
            Transform best = null;
            float bestD = float.MaxValue;
            foreach (LaundryContainer c in m_Carts)
            {
                if (c == null) continue;
                float d = Vector3.Distance(c.transform.position, m_Zone.transform.position);
                if (d < bestD) { bestD = d; best = c.transform; }
            }
            return best != null ? best : base.GuideTarget;
        }

        private int Count(LaundryContainer c)
        {
            if (c == null) return 0;
            int n = 0;
            foreach (LaundryItem item in m_Items)
                if (c.Holds(item)) n++;
            return n;
        }
    }
}

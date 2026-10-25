using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// Action: serve the order. The plated coffee and cake both have to be set down at the counter's
    /// serving spot (let go of, plates at rest). Completes with the celebration.
    /// </summary>
    public class ServeStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string OffColor = "#8A8A92";
        private const string TextColor = "#E6E6EA";

        [Tooltip("The plating step (gives the cup and the cake).")]
        [SerializeField] private PlateStep m_Plating;
        [SerializeField] private ItemTrigger m_Zone;
        [Tooltip("A plate moving faster than this (m/s) isn't set down yet.")]
        [SerializeField] private float m_RestSpeed = 0.3f;

        private int m_LastState = -1;

        public override Transform GuideTarget
        {
            get
            {
                if (m_Plating == null || m_Zone == null) return base.GuideTarget;
                foreach (PlateItem item in new[] { m_Plating.Cup, m_Plating.PlatedCake })
                {
                    if (item != null && !Served(item) && !IsCarried(item)) return item.transform;
                }
                return m_Zone.transform;
            }
        }

        protected override void OnBegin()
        {
            m_LastState = -1;
            if (m_Plating == null || m_Zone == null) Debug.LogWarning("[Quest] ServeStep is missing the plating step or the zone.", this);
        }

        private void Update()
        {
            if (!IsActive || m_Plating == null || m_Zone == null) return;
            PlateItem cup = m_Plating.Cup;
            PlateItem cake = m_Plating.PlatedCake;
            bool cupServed = Served(cup);
            bool cakeServed = Served(cake);
            if (cupServed && cakeServed)
            {
                SetProgress($"<color={OnColor}>Order up! Your first customer is happy.</color>");
                Complete();
                return;
            }

            int state = (cupServed ? 1 : 0) + (cakeServed ? 2 : 0);
            if (state == m_LastState) return;
            m_LastState = state;
            SetProgress(
                $"<color={(cupServed ? OnColor : OffColor)}><b>COFFEE</b> {(cupServed ? "SERVED" : "NOT YET")}</color>" +
                $"<space=1.2em><color={(cakeServed ? OnColor : OffColor)}><b>CAKE</b> {(cakeServed ? "SERVED" : "NOT YET")}</color>\n" +
                $"<color={TextColor}><i>Carry the plates over and set them down at the <b>Here!</b> sign</i></color>");
        }

        /// <summary>In the zone, not held, and (if on a plate) the plate isn't held or moving.</summary>
        private bool Served(PlateItem item)
        {
            if (item == null || item.Body == null || IsCarried(item)) return false;
            if (!m_Zone.Contains(item.Body)) return false;
            if (item.Plate == null) return item.Body.isKinematic || item.Body.linearVelocity.magnitude < m_RestSpeed;
            Rigidbody plateBody = item.Plate.GetComponent<Rigidbody>();
            return plateBody == null || plateBody.isKinematic || plateBody.linearVelocity.magnitude < m_RestSpeed;
        }

        private static bool IsCarried(PlateItem item) =>
            GrabUtil.IsHeldByHand(item.Grab) || (item.Plate != null && GrabUtil.IsHeldByHand(item.Plate.Grab));
    }
}

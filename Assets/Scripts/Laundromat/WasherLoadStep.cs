using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: load the booked washer. Get every shirt into its drum (tip the basket over the open door so
    /// they spill in, or let go of them in the door one by one) and pour in detergent (tip the bottle over the door). Then the door shuts by itself and the
    /// washer starts: the drum spins and the water sloshes.
    /// </summary>
    public class WasherLoadStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string OffColor = "#8A8A92";
        private const string TextColor = "#E6E6EA";

        [SerializeField] private QueueStep m_Queue;
        [SerializeField] private LaundryItem[] m_Items;

        [Header("Basket spill")]
        [SerializeField] private LaundryContainer m_Basket;
        [Tooltip("Tip the basket at least this far (degrees from upright) to spill.")]
        [SerializeField] private float m_SpillAngle = 65f;
        [Tooltip("The basket's middle must be this close to the door opening (m).")]
        [SerializeField] private float m_SpillReach = 0.55f;
        [Tooltip("Seconds between shirts tumbling in.")]
        [SerializeField] private float m_SpillInterval = 0.15f;

        [Header("Detergent")]
        [SerializeField] private XRGrabInteractable m_Bottle;
        [Tooltip("Centre of the bottle's cap; its up axis is the bottle's up.")]
        [SerializeField] private Transform m_Spout;
        [SerializeField] private StreamFx m_Stream;
        [SerializeField] private Color m_DetergentColor = new Color(0.3f, 0.75f, 0.45f);
        [Tooltip("Tip the bottle at least this far (degrees from upright) to pour.")]
        [SerializeField] private float m_PourAngle = 70f;
        [Tooltip("The spout must be this close to the door opening (m).")]
        [SerializeField] private float m_Reach = 0.35f;
        [SerializeField] private float m_PourTime = 2f;

        private float m_Poured;
        private float m_NextSpill;
        private AudioSource m_PourSound;
        private int m_LastState = -1;
        private bool m_Done;

        private LaundryMachine Washer => m_Queue != null ? m_Queue.Machine(false) : null;
        private bool DetergentDone => m_Poured >= m_PourTime;

        public override Transform GuideTarget
        {
            get
            {
                LaundryMachine w = Washer;
                if (w == null) return base.GuideTarget;
                Transform opening = w.Opening != null ? w.Opening.transform : w.transform;
                if (InCount() < Total())
                {
                    if (BasketHeld()) return opening;
                    if (m_Basket != null && m_Basket.Count > 0) return m_Basket.transform;
                    foreach (LaundryItem item in m_Items)
                        if (item != null && GrabUtil.IsHeldByHand(item.Grab)) return opening;
                    foreach (LaundryItem item in m_Items)
                        if (item != null && !InWasher(item)) return item.transform;
                }
                if (!DetergentDone && m_Bottle != null && !GrabUtil.IsHeldByHand(m_Bottle)) return m_Bottle.transform;
                return opening;
            }
        }

        protected override void OnBegin()
        {
            m_Poured = 0f;
            m_Done = false;
            m_LastState = -1;
            LaundryMachine w = Washer;
            if (w != null && w.Load != null) w.Load.Accepting = true;
            if (m_Spout != null)
            {
                m_PourSound = m_Spout.gameObject.GetComponent<AudioSource>();
                if (m_PourSound == null)
                {
                    m_PourSound = m_Spout.gameObject.AddComponent<AudioSource>();
                    m_PourSound.clip = SynthAudio.MilkPour;
                    m_PourSound.loop = true;
                    m_PourSound.spatialBlend = 1f;
                    m_PourSound.minDistance = 0.5f;
                    m_PourSound.maxDistance = 8f;
                }
            }
        }

        protected override void OnEnd()
        {
            if (m_Stream != null) m_Stream.Hide();
            if (m_PourSound != null) m_PourSound.Stop();
        }

        private void Update()
        {
            if (!IsActive || m_Done) return;
            LaundryMachine w = Washer;
            if (w == null) return;

            bool pouring = UpdatePour(w);
            bool spilling = UpdateSpill(w);
            int count = InCount(), total = Total();
            if (count >= total && DetergentDone)
            {
                m_Done = true;
                if (m_Stream != null) m_Stream.Hide();
                if (m_PourSound != null) m_PourSound.Stop();
                if (w.Load != null) w.Load.Accepting = false;
                w.SetDoor(false);
                w.Run(true);
                SetProgress($"<color={OnColor}>Washing! Listen to it slosh.</color>");
                Complete();
                return;
            }

            int pct = Mathf.RoundToInt(Mathf.Clamp01(m_Poured / m_PourTime) * 100f);
            int state = count * 1000 + pct * 4 + (pouring ? 1 : 0) + (spilling ? 2 : 0);
            if (state == m_LastState) return;
            m_LastState = state;
            string hint = count < total
                ? spilling ? "Spilling in..." : "Lift the basket and tip it over the open door to spill the shirts in"
                : pouring ? "Pouring..." : "Grab the green detergent bottle and tip it over the open door";
            bool shirtsOk = count >= total;
            SetProgress(
                $"<color={(shirtsOk ? OnColor : OffColor)}><b>SHIRTS</b> {count}/{total}</color>" +
                $"<space=1.2em><color={(DetergentDone ? OnColor : OffColor)}><b>DETERGENT</b> {pct}%</color>\n" +
                $"<color={TextColor}><i>{hint}</i></color>");
        }

        private bool BasketHeld()
        {
            var grab = m_Basket != null ? m_Basket.GetComponent<XRGrabInteractable>() : null;
            return grab != null && GrabUtil.IsHeldByHand(grab);
        }

        /// <summary>Basket held, tipped and near the door: shirts tumble from it into the drum one after another.</summary>
        private bool UpdateSpill(LaundryMachine w)
        {
            if (m_Basket == null || w.Load == null || w.Opening == null || m_Basket.Count == 0 || !BasketHeld()) return false;
            float tilt = Vector3.Angle(m_Basket.Floor.up, Vector3.up);
            Vector3 middle = m_Basket.Opening != null ? m_Basket.Opening.transform.position : m_Basket.transform.position;
            if (tilt < m_SpillAngle || Vector3.Distance(middle, w.Opening.transform.position) > m_SpillReach) return false;
            if (Time.time < m_NextSpill) return true;
            m_NextSpill = Time.time + m_SpillInterval;
            var items = m_Basket.Items;
            LaundryItem top = items[items.Count - 1];
            w.Load.Add(top);
            return true;
        }

        private bool UpdatePour(LaundryMachine w)
        {
            if (m_Bottle == null || m_Spout == null || w.Opening == null || DetergentDone) return false;
            bool held = GrabUtil.IsHeldByHand(m_Bottle);
            float tilt = Vector3.Angle(m_Spout.up, Vector3.up);
            Vector3 target = w.Opening.transform.position;
            bool near = Vector3.Distance(m_Spout.position, target) <= m_Reach;
            bool pouring = held && tilt >= m_PourAngle && near;
            if (pouring)
            {
                m_Poured += Time.deltaTime;
                if (m_Stream != null) m_Stream.Show(m_Spout.position, target, m_DetergentColor);
                if (m_PourSound != null && !m_PourSound.isPlaying) m_PourSound.Play();
                if (DetergentDone) SynthAudio.PlayAt(SynthSound.Ding, target, 0.5f);
            }
            else
            {
                if (m_Stream != null) m_Stream.Hide();
                if (m_PourSound != null && m_PourSound.isPlaying) m_PourSound.Stop();
            }
            return pouring;
        }

        private bool InWasher(LaundryItem item)
        {
            LaundryMachine w = Washer;
            return w != null && w.Load != null && w.Load.Holds(item);
        }

        private int InCount()
        {
            int n = 0;
            if (m_Items == null) return 0;
            foreach (LaundryItem item in m_Items)
                if (InWasher(item)) n++;
            return n;
        }

        private int Total() => m_Items != null ? m_Items.Length : 0;
    }
}

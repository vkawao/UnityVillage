using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: bring the plush you won to the prize counter. Completes when it's been let go of
    /// in the "Here!" zone on the counter and has settled there.
    /// </summary>
    public class PlushToCounterStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string TextColor = "#E6E6EA";
        private const string HintColor = "#8A8A92";

        [Tooltip("The claw game: the plush it gave out is the one to bring.")]
        [SerializeField] private ClawGameStep m_Claw;
        [SerializeField] private ItemTrigger m_Zone;
        [Tooltip("How long (s) it must rest in the zone.")]
        [SerializeField] private float m_SettleTime = 0.3f;

        private float m_InZone;

        private XRGrabInteractable Plush => m_Claw != null ? m_Claw.WonPlush : null;

        public override Transform GuideTarget
        {
            get
            {
                XRGrabInteractable p = Plush;
                if (p != null && !GrabUtil.IsHeldByHand(p)) return p.transform;
                return m_Zone != null ? m_Zone.transform : base.GuideTarget;
            }
        }

        protected override void OnBegin()
        {
            m_InZone = 0f;
            SetProgress($"<color={TextColor}>Carry your plush to the <b>prize counter</b> and put it down at the <b>Here!</b> sign</color>\n" +
                        $"<color={HintColor}><i>Let go of it on the counter</i></color>");
        }

        private void Update()
        {
            if (!IsActive || m_Zone == null) return;
            XRGrabInteractable p = Plush;
            if (p == null || GrabUtil.IsHeldByHand(p)) { m_InZone = 0f; return; }
            Rigidbody body = p.GetComponent<Rigidbody>();
            bool still = body == null || body.isKinematic || body.linearVelocity.magnitude < 0.1f;
            if (still && m_Zone.Contains(body)) m_InZone += Time.deltaTime;
            else m_InZone = 0f;
            if (m_InZone < m_SettleTime) return;

            SynthAudio.PlayAt(SynthSound.Ding, m_Zone.transform.position, 0.9f);
            SetProgress($"<color={OnColor}>Nice catch! What a cute prize.</color>");
            Complete();
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: throw the empty detergent bottle in the bin while the washer runs. Once it's in, the
    /// wash finishes: the drum winds down, the end-of-wash tune plays, the door opens and the shirts
    /// inside are wet (darker).
    /// </summary>
    public class FinishWashStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string TextColor = "#E6E6EA";

        [SerializeField] private QueueStep m_Queue;
        [SerializeField] private XRGrabInteractable m_Bottle;
        [SerializeField] private ItemTrigger m_Bin;
        [SerializeField] private LaundryItem[] m_Items;
        [Tooltip("Seconds the bottle must stay in the bin after letting go.")]
        [SerializeField] private float m_SettleTime = 0.25f;

        private float m_Inside;
        private bool m_Finishing;

        public override Transform GuideTarget
        {
            get
            {
                if (m_Bottle == null || m_Bin == null) return base.GuideTarget;
                if (m_Finishing) return m_Queue != null && m_Queue.Machine(false) != null ? m_Queue.Machine(false).transform : base.GuideTarget;
                return GrabUtil.IsHeldByHand(m_Bottle) ? m_Bin.transform : m_Bottle.transform;
            }
        }

        protected override void OnBegin()
        {
            m_Inside = 0f;
            m_Finishing = false;
            SetProgress($"<color={TextColor}>The detergent bottle is empty. Throw it in the <b>bin</b> by the entrance</color>\n" +
                        "<color=#8A8A92><i>The wash finishes once it's in</i></color>");
        }

        private void Update()
        {
            if (!IsActive || m_Finishing || m_Bottle == null || m_Bin == null) return;
            if (GrabUtil.IsHeldByHand(m_Bottle) || !m_Bin.Contains(m_Bottle.GetComponent<Rigidbody>()))
            {
                m_Inside = 0f;
                return;
            }
            m_Inside += Time.deltaTime;
            if (m_Inside >= m_SettleTime) StartCoroutine(FinishWash());
        }

        private IEnumerator FinishWash()
        {
            m_Finishing = true;
            SynthAudio.PlayAt(SynthSound.CanDrop, m_Bin.transform.position, 0.8f);
            SetProgress($"<color={TextColor}>Nice throw! The wash is finishing...</color>");
            LaundryMachine washer = m_Queue != null ? m_Queue.Machine(false) : null;
            if (washer != null)
            {
                washer.Run(false);
                for (float t = 0f; t < 6f && !washer.IsStill; t += Time.deltaTime) yield return null;
                if (m_Items != null)
                    foreach (LaundryItem item in m_Items)
                        if (item != null) item.SetWet(true);
                SynthAudio.PlayAt(SynthSound.WashDone, washer.transform.position + Vector3.up, 0.9f);
                yield return new WaitForSeconds(1.2f);
                washer.SetDoor(true);
            }
            SetProgress($"<color={OnColor}>Ding-ding! The wash is done.</color>");
            Complete();
        }
    }
}

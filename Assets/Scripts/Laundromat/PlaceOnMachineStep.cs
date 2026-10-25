using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: carry something (the basket) to the booked washer or dryer and set it down in a zone
    /// there (on top of the washer). The machine's door opens.
    /// </summary>
    public class PlaceOnMachineStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string TextColor = "#E6E6EA";

        [SerializeField] private QueueStep m_Queue;
        [SerializeField] private bool m_Dryer;
        [SerializeField] private XRGrabInteractable m_Item;
        [Tooltip("Where it must be set down (follows the booked machine).")]
        [SerializeField] private ItemTrigger m_Zone;
        [SerializeField] private string m_ItemName = "the basket";
        [SerializeField] private string m_Where = "on top of";
        [Tooltip("Seconds it must rest there after letting go.")]
        [SerializeField] private float m_SettleTime = 0.3f;

        private float m_Inside;

        private string MachineName => m_Queue == null ? (m_Dryer ? "the dryer" : "the washer")
            : $"{(m_Dryer ? "Dryer" : "Washer")} {(m_Dryer ? m_Queue.ChosenDryer : m_Queue.ChosenWasher) + 1}";

        public override Transform GuideTarget
        {
            get
            {
                if (m_Item == null || m_Zone == null) return base.GuideTarget;
                return GrabUtil.IsHeldByHand(m_Item) ? m_Zone.transform : m_Item.transform;
            }
        }

        protected override void OnBegin()
        {
            m_Inside = 0f;
            SetProgress($"<color={TextColor}>Set {m_ItemName} down {m_Where} <b>{MachineName}</b></color>\n" +
                        "<color=#8A8A92><i>Look for its number tag and the \"Here!\" marker</i></color>");
        }

        private void Update()
        {
            if (!IsActive || m_Item == null || m_Zone == null) return;
            if (GrabUtil.IsHeldByHand(m_Item) || !m_Zone.Contains(m_Item.GetComponent<Rigidbody>()))
            {
                m_Inside = 0f;
                return;
            }
            m_Inside += Time.deltaTime;
            if (m_Inside < m_SettleTime) return;

            LaundryMachine machine = m_Queue != null ? m_Queue.Machine(m_Dryer) : null;
            if (machine != null) machine.SetDoor(true);
            SetProgress($"<color={OnColor}>{MachineName} opens up!</color>");
            Complete();
        }
    }
}

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: push the loaded cart to the booked dryer. When it reaches the zone in front of it,
    /// the dryer's door opens.
    /// </summary>
    public class CartToMachineStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string TextColor = "#E6E6EA";

        [SerializeField] private QueueStep m_Queue;
        [SerializeField] private bool m_Dryer = true;
        [Tooltip("The step that loaded the cart (gives which cart).")]
        [SerializeField] private CartLoadStep m_CartStep;
        [Tooltip("In front of the booked machine (follows the booking).")]
        [SerializeField] private ItemTrigger m_Zone;

        private LaundryContainer Cart => m_CartStep != null ? m_CartStep.ChosenCart : null;

        private string MachineName => m_Queue == null ? "the dryer"
            : $"{(m_Dryer ? "Dryer" : "Washer")} {(m_Dryer ? m_Queue.ChosenDryer : m_Queue.ChosenWasher) + 1}";

        public override Transform GuideTarget
        {
            get
            {
                LaundryContainer cart = Cart;
                if (cart == null || m_Zone == null) return base.GuideTarget;
                var grab = cart.GetComponent<XRGrabInteractable>();
                return grab != null && GrabUtil.IsHeldByHand(grab) ? m_Zone.transform : cart.transform;
            }
        }

        protected override void OnBegin()
        {
            SetProgress($"<color={TextColor}>Push the cart to <b>{MachineName}</b></color>\n" +
                        "<color=#8A8A92><i>Grab the cart and walk; the \"Here!\" marker shows where</i></color>");
        }

        private void Update()
        {
            LaundryContainer cart = Cart;
            if (!IsActive || cart == null || m_Zone == null) return;
            if (!m_Zone.Contains(cart.GetComponent<Rigidbody>())) return;
            LaundryMachine machine = m_Queue != null ? m_Queue.Machine(m_Dryer) : null;
            if (machine != null) machine.SetDoor(true);
            SetProgress($"<color={OnColor}>{MachineName} opens up!</color>");
            Complete();
        }
    }
}

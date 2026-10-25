using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: put the wet shirts into the booked dryer. A shirt brought into its open door goes in
    /// (held or let go of). With all of them in, the door shuts by itself and the dryer hums away.
    /// Meanwhile the cart is locked in place in front of the dryer (not grabbable), so reaching into it
    /// grabs a shirt, never the cart.
    /// </summary>
    public class DryerLoadStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string TextColor = "#E6E6EA";

        [SerializeField] private QueueStep m_Queue;
        [SerializeField] private LaundryItem[] m_Items;
        [Tooltip("The step that loaded the cart (gives which cart).")]
        [SerializeField] private CartLoadStep m_CartStep;
        [Tooltip("Where the cart locks: the zone in front of the booked dryer.")]
        [SerializeField] private ItemTrigger m_CartSpot;

        private int m_In;
        private int m_LastCount = -1;

        private LaundryMachine Dryer => m_Queue != null ? m_Queue.Machine(true) : null;

        public override Transform GuideTarget
        {
            get
            {
                LaundryMachine d = Dryer;
                if (d == null || m_Items == null) return base.GuideTarget;
                Transform opening = d.Opening != null ? d.Opening.transform : d.transform;
                foreach (LaundryItem item in m_Items)
                    if (item != null && item.gameObject.activeInHierarchy && GrabUtil.IsHeldByHand(item.Grab)) return opening;
                foreach (LaundryItem item in m_Items)
                    if (item != null && item.gameObject.activeInHierarchy) return item.transform;
                return opening;
            }
        }

        protected override void OnBegin()
        {
            m_In = 0;
            m_LastCount = -1;
            LaundryContainer cart = m_CartStep != null ? m_CartStep.ChosenCart : null;
            if (cart != null) StartCoroutine(LockCart(cart));
        }

        /// <summary>Let go of the cart, make it ungrabbable and slide it onto the spot in front of the dryer.</summary>
        private IEnumerator LockCart(LaundryContainer cart)
        {
            var grab = cart.GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                if (grab.isSelected && grab.interactionManager != null) grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
                grab.enabled = false;
            }
            var body = cart.GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = true;
            if (m_CartSpot == null) yield break;
            Transform t = cart.transform;
            Vector3 from = t.position;
            Vector3 to = new Vector3(m_CartSpot.transform.position.x, from.y, m_CartSpot.transform.position.z);
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.35f)
            {
                t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }
            t.position = to;
            SynthAudio.PlayAt(SynthSound.Click, to, 0.6f);
        }

        private void Update()
        {
            LaundryMachine d = Dryer;
            if (!IsActive || d == null || d.Opening == null || m_Items == null) return;

            foreach (LaundryItem item in m_Items)
            {
                // Shirts still packed in the cart don't count, even with the cart pushed right up to the door.
                if (item == null || item.Container != null || !item.gameObject.activeInHierarchy || !d.Opening.Contains(item.Body)) continue;
                item.gameObject.SetActive(false); // into the drum
                SynthAudio.PlayAt(SynthSound.ClothFlop, d.Opening.transform.position, 0.8f);
                m_In++;
            }

            int total = m_Items.Length;
            if (m_In >= total)
            {
                d.SetDoor(false);
                d.Run(true);
                SetProgress($"<color={OnColor}>Drying! Your laundry is done. Great job!</color>");
                Complete();
                return;
            }
            if (m_In == m_LastCount) return;
            m_LastCount = m_In;
            SetProgress($"<color={TextColor}>Shirts in the dryer: <b>{m_In}/{total}</b></color>\n" +
                        "<color=#8A8A92><i>Take them from the cart and put them in through the open door</i></color>");
        }
    }
}

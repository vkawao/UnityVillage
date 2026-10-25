using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: put an item into a slot (mail into a mailbox, coin into a machine).
    /// Completes as soon as the item touches the slot volume, held or not; then plays a
    /// sound and (optionally) makes the item disappear, as if it went inside.
    /// </summary>
    public class InsertItemStep : QuestStep
    {
        [Tooltip("The item to insert. Leave empty to use the item chosen by Item Source.")]
        [SerializeField] private XRGrabInteractable m_Item;
        [Tooltip("Optional: a step that decides the item (implements IQuestItemSource).")]
        [SerializeField] private MonoBehaviour m_ItemSource;
        [SerializeField] private ItemTrigger m_Slot;
        [SerializeField] private SynthSound m_Sound = SynthSound.Click;
        [SerializeField, Range(0f, 1f)] private float m_Volume = 0.9f;
        [SerializeField] private bool m_HideItem = true;

        private XRGrabInteractable Item => QuestItems.Resolve(m_Item, m_ItemSource);

        /// <summary>The item until you pick it up, then the slot.</summary>
        public override Transform GuideTarget
        {
            get
            {
                XRGrabInteractable item = Item;
                if (item == null || m_Slot == null) return base.GuideTarget;
                if (GrabUtil.IsHeldByHand(item)) return m_Slot.transform;
                return item.gameObject.activeInHierarchy ? item.transform : base.GuideTarget;
            }
        }

        private void Update()
        {
            if (!IsActive || m_Slot == null) return;
            XRGrabInteractable item = Item;
            if (item == null) return;
            Rigidbody body = item.GetComponent<Rigidbody>();
            if (!m_Slot.Contains(body)) return;

            SynthAudio.PlayAt(m_Sound, m_Slot.transform.position, m_Volume);
            if (m_HideItem) item.gameObject.SetActive(false);
            Complete();
        }
    }
}

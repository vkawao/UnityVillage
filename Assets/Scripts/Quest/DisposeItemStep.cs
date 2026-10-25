using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: throw an item into the right bin. Completes when the item has been let go of
    /// inside the correct bin. Dropping it into a wrong bin plays a buzzer, shows a hint and
    /// pops the item back out in front of that bin.
    /// </summary>
    public class DisposeItemStep : QuestStep
    {
        [Tooltip("The item to throw away. Leave empty to use the item chosen by Item Source.")]
        [SerializeField] private XRGrabInteractable m_Item;
        [Tooltip("Optional: a step that decides the item (implements IQuestItemSource).")]
        [SerializeField] private MonoBehaviour m_ItemSource;
        [SerializeField] private ItemTrigger m_CorrectBin;
        [SerializeField] private ItemTrigger[] m_WrongBins;
        [Tooltip("Shown under the instruction after a wrong bin.")]
        [SerializeField] private string m_WrongHint = "Wrong bin! Try the other one.";
        [SerializeField] private SynthSound m_Sound = SynthSound.None;
        [Tooltip("Seconds the item must stay in the bin after letting go.")]
        [SerializeField] private float m_SettleTime = 0.25f;

        private float m_InsideTime;
        private float m_HintUntil;

        private XRGrabInteractable Item => QuestItems.Resolve(m_Item, m_ItemSource);

        /// <summary>The item while it's lying around, the right bin while you carry it.</summary>
        public override Transform GuideTarget
        {
            get
            {
                XRGrabInteractable item = Item;
                if (item == null || m_CorrectBin == null) return base.GuideTarget;
                if (GrabUtil.IsHeldByHand(item) || !item.gameObject.activeInHierarchy) return m_CorrectBin.transform;
                return m_CorrectBin.Contains(item.GetComponent<Rigidbody>()) ? m_CorrectBin.transform : item.transform;
            }
        }

        protected override void OnBegin()
        {
            m_InsideTime = 0f;
            SetProgress(string.Empty);
        }

        private void Update()
        {
            if (!IsActive || m_CorrectBin == null) return;
            XRGrabInteractable item = Item;
            if (item == null || !item.gameObject.activeInHierarchy) return;
            Rigidbody body = item.GetComponent<Rigidbody>();
            bool held = GrabUtil.IsHeldByHand(item);

            if (m_HintUntil > 0f && Time.time > m_HintUntil)
            {
                m_HintUntil = 0f;
                SetProgress(string.Empty);
            }

            if (!held && m_CorrectBin.Contains(body))
            {
                m_InsideTime += Time.deltaTime;
                if (m_InsideTime >= m_SettleTime)
                {
                    SynthAudio.PlayAt(m_Sound, m_CorrectBin.transform.position);
                    SetProgress(string.Empty);
                    Complete();
                }
                return;
            }
            m_InsideTime = 0f;

            if (held || m_WrongBins == null) return;
            foreach (ItemTrigger wrong in m_WrongBins)
            {
                if (wrong == null || !wrong.Contains(body)) continue;
                SynthAudio.PlayAt(SynthSound.Buzzer, wrong.transform.position, 0.8f);
                PopOut(body, wrong);
                SetProgress($"<color=#FF8A80>{m_WrongHint}</color>");
                m_HintUntil = Time.time + 4f;
                break;
            }
        }

        /// <summary>Put the item back on the ground in front of the bin (the trigger's -Z side faces the player).</summary>
        private static void PopOut(Rigidbody body, ItemTrigger bin)
        {
            Transform t = bin.transform;
            Vector3 front = -t.forward;
            Vector3 pos = t.position + front * (bin.Size.z * 0.5f + 0.35f) + Vector3.up * 0.15f;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.transform.position = pos;
            body.position = pos;
        }
    }
}

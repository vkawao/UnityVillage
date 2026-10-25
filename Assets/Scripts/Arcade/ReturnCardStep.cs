using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: return your arcade card. Push it into the grey card slot behind the counter (where it
    /// came from): it slides down into the slot and is gone, the register thanks you. The finale.
    /// </summary>
    public class ReturnCardStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string TextColor = "#E6E6EA";
        private const string HintColor = "#8A8A92";

        [SerializeField] private HipHolster m_Holster;
        [SerializeField] private XRGrabInteractable m_Card;
        [SerializeField] private ItemTrigger m_Slot;
        [Tooltip("The card's pose sticking out of the slot (the same one it came out at).")]
        [SerializeField] private Transform m_CardOut;
        [SerializeField] private float m_SlideDistance = 0.05f;
        [Tooltip("World direction the card came out of the slot; it goes back in the other way.")]
        [SerializeField] private Vector3 m_SlideDirection = Vector3.forward;
        [SerializeField] private float m_SlideTime = 0.6f;
        [Tooltip("Text on the register screen.")]
        [SerializeField] private TMP_Text m_Screen;

        private bool m_Done;

        public override Transform GuideTarget
        {
            get
            {
                if (m_Card == null) return base.GuideTarget;
                if (m_Holster != null && m_Holster.IsHolding(m_Card)) return m_Holster.transform;
                if (GrabUtil.IsHeldByHand(m_Card)) return m_Slot != null ? m_Slot.transform : base.GuideTarget;
                return m_Card.transform;
            }
        }

        protected override void OnBegin()
        {
            m_Done = false;
            bool inPocket = m_Holster != null && m_Card != null && m_Holster.IsHolding(m_Card);
            if (inPocket) m_Holster.Highlight = true;
            SetScreen("<b>THANKS!</b>\n<size=60%>Return your card here</size>");
            SetProgress($"<color={TextColor}>{(inPocket ? "Take your card from your <b>hip pocket</b> and push" : "Push")} it into the " +
                        "<b>grey card slot</b> behind the counter</color>\n" +
                        $"<color={HintColor}><i>The same slot it came out of, in front of the register</i></color>");
        }

        protected override void OnEnd()
        {
            if (m_Holster != null) m_Holster.Highlight = false;
        }

        private void Update()
        {
            if (!IsActive || m_Done || m_Card == null) return;
            bool held = GrabUtil.IsHeldByHand(m_Card);
            if (held && m_Holster != null) m_Holster.Highlight = false;
            if (held && m_Slot != null && m_Slot.Contains(m_Card.GetComponent<Rigidbody>())) StartCoroutine(SlideIn());
        }

        private IEnumerator SlideIn()
        {
            m_Done = true;
            if (m_Card.interactionManager != null) m_Card.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Card);
            m_Card.enabled = false;
            Rigidbody body = m_Card.GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = true;
            Transform t = m_Card.transform;
            Vector3 from = m_CardOut != null ? m_CardOut.position : t.position;
            Vector3 to = from - m_SlideDirection.normalized * m_SlideDistance;
            if (m_CardOut != null) t.SetPositionAndRotation(from, m_CardOut.rotation);
            SynthAudio.PlayAt(SynthSound.CardDispense, from, 0.9f);
            for (float k = 0f; k < m_SlideTime; k += Time.deltaTime)
            {
                t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k / m_SlideTime));
                yield return null;
            }
            m_Card.gameObject.SetActive(false);
            if (m_Holster != null) m_Holster.Revealed = false; // nothing left to carry
            SynthAudio.PlayAt(SynthSound.CardApproved, from, 0.9f);
            SetScreen($"<b><color={OnColor}>THANKS FOR PLAYING!</color></b>\n<size=60%>See you soon!</size>");
            SetProgress($"<color={OnColor}>Thanks for playing! See you next time at the arcade!</color>");
            Complete();
        }

        private void SetScreen(string text)
        {
            if (m_Screen != null) m_Screen.text = text;
        }
    }
}

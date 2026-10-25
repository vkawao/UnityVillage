using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: get coins from the change machine with your credit card. The card appears in the
    /// hip pocket; tapping it on the machine's grey pad pays, and the coins drop into the tray.
    /// The step finishes once the card is back in the pocket, where it's put away for good
    /// (so the pocket is free for the coins).
    /// </summary>
    public class CardChangeStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string TextColor = "#E6E6EA";

        [SerializeField] private HipHolster m_Holster;
        [SerializeField] private XRGrabInteractable m_Card;
        [SerializeField] private ItemTrigger m_Pad;
        [SerializeField] private XRGrabInteractable m_Coins;
        [Tooltip("Where the coins land: on the machine's change tray.")]
        [SerializeField] private Transform m_Outlet;
        [Tooltip("Optional: text on the machine's little screen.")]
        [SerializeField] private TMP_Text m_Screen;

        private bool m_Paid;
        private bool m_PickedUp;

        /// <summary>The pocket while the card is in it, the pad once it's in your hand; then the pocket again.</summary>
        public override Transform GuideTarget
        {
            get
            {
                if (m_Holster == null || m_Card == null) return base.GuideTarget;
                bool held = GrabUtil.IsHeldByHand(m_Card);
                if (!m_Paid)
                {
                    if (m_Holster.IsHolding(m_Card)) return m_Holster.transform;
                    return m_Pad != null && held ? m_Pad.transform : m_Card.transform;
                }
                return held ? m_Holster.transform : m_Card.transform;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            SetScreen("CHANGE\n<size=70%>Tap card for coins</size>");
        }

        protected override void OnBegin()
        {
            m_Paid = false;
            m_PickedUp = false;
            if (m_Holster != null && m_Card != null)
            {
                m_Holster.Revealed = true; // the pocket appears from this step on
                m_Holster.Store(m_Card);
                m_Holster.Highlight = true;
            }
            SetProgress($"<color={TextColor}>Your card is in your <b>hip pocket</b>. Look down at your hip!</color>");
        }

        protected override void OnEnd()
        {
            if (m_Holster != null) m_Holster.Highlight = false;
        }

        private void Update()
        {
            if (!IsActive || m_Card == null) return;
            bool held = GrabUtil.IsHeldByHand(m_Card);

            if (!m_Paid)
            {
                if (m_Holster != null && m_Holster.Highlight && held)
                {
                    m_Holster.Highlight = false;
                    SetProgress($"<color={TextColor}>Tap the card on the <b>grey pad</b> under CHANGE</color>");
                }
                // Only a card that's been in your hand counts: right after it's moved into the pocket,
                // physics can still report it where it was hidden.
                if (held) m_PickedUp = true;
                if (m_PickedUp && m_Pad != null && m_Pad.Contains(m_Card.GetComponent<Rigidbody>())) Pay();
                return;
            }

            if (m_Holster != null && m_Holster.IsHolding(m_Card))
            {
                m_Card.gameObject.SetActive(false); // put away: the pocket is free for coins now
                m_Holster.Highlight = false;
                SetProgress($"<color={OnColor}>Card put away. Grab your coins!</color>");
                Complete();
            }
        }

        private void Pay()
        {
            m_Paid = true;
            SynthAudio.PlayAt(SynthSound.CardApproved, m_Pad.transform.position, 0.9f);
            SetScreen("<color=#6EE787>APPROVED</color>\n<size=70%>Take your coins</size>");
            if (m_Coins != null) StartCoroutine(DropCoins());
            if (m_Holster != null) m_Holster.Highlight = true;
            SetProgress($"<color={OnColor}>Paid!</color> <color={TextColor}>Now put the card back in your <b>hip pocket</b></color>\n" +
                        "<color=#8A8A92><i>Your coins fit in the pocket too</i></color>");
        }

        private IEnumerator DropCoins()
        {
            yield return new WaitForSeconds(0.5f);
            if (m_Outlet == null) yield break;
            SynthAudio.PlayAt(SynthSound.ChangeOut, m_Outlet.position, 0.9f);
            yield return new WaitForSeconds(0.35f);
            GameObject go = m_Coins.gameObject;
            Vector3 spawn = m_Outlet.position + Vector3.up * 0.06f;
            go.transform.position = spawn;
            Rigidbody body = m_Coins.GetComponent<Rigidbody>();
            if (body != null) body.position = spawn; // so interpolation doesn't start from where it was hidden
            go.SetActive(true);
            if (body != null)
            {
                body.isKinematic = false;
                body.linearVelocity = Vector3.down * 0.3f;
                body.angularVelocity = Vector3.zero;
            }
        }

        private void SetScreen(string text)
        {
            if (m_Screen != null) m_Screen.text = text;
        }
    }
}

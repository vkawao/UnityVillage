using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: pay with the credit card from your hip pocket. The card appears in the hip
    /// holster; tapping it on the reader fails the first times ("card not read"), then
    /// succeeds: approval chirp, and the chosen snack drops into the tray (no rolling).
    /// </summary>
    public class CardPaymentStep : QuestStep
    {
        [SerializeField] private SmartMachineScreen m_Screen;
        [SerializeField] private SnackChoiceStep m_Snack;
        [SerializeField] private HipHolster m_Holster;
        [SerializeField] private XRGrabInteractable m_Card;
        [SerializeField] private ItemTrigger m_Reader;
        [Tooltip("Inside the tray, at the tray floor.")]
        [SerializeField] private Transform m_Outlet;
        [Tooltip("Taps that fail before the payment goes through.")]
        [SerializeField] private int m_FailedTaps = 2;
        [Tooltip("The card has to leave the reader and come back, and this many seconds must pass between taps.")]
        [SerializeField] private float m_TapCooldown = 0.6f;

        private int m_Taps;
        private bool m_WasInside;
        private float m_LastTap = -10f;

        /// <summary>The hip pocket while the card is in it, the reader once it's in your hand.</summary>
        public override Transform GuideTarget
        {
            get
            {
                if (m_Holster != null && m_Card != null && m_Holster.IsHolding(m_Card)) return m_Holster.transform;
                return m_Reader != null ? m_Reader.transform : base.GuideTarget;
            }
        }

        protected override void OnBegin()
        {
            m_Taps = 0;
            m_WasInside = false;
            if (m_Holster != null && m_Card != null)
            {
                m_Holster.Revealed = true; // the pocket appears from this step on
                m_Holster.Store(m_Card);
                m_Holster.Highlight = true;
            }
            if (m_Screen != null) m_Screen.SetStatus("Tap your card on the reader below", SmartMachineScreen.Neutral);
            SetProgress("<color=#E6E6EA>Your card is in your <b>hip pocket</b>. Look down at your hip!</color>");
        }

        protected override void OnEnd()
        {
            if (m_Holster != null) m_Holster.Highlight = false;
        }

        private void Update()
        {
            if (!IsActive || m_Card == null || m_Reader == null) return;

            if (m_Holster != null && m_Holster.Highlight && GrabUtil.IsHeldByHand(m_Card))
            {
                m_Holster.Highlight = false;
                SetProgress("Tap the card on the metal reader under the screen");
            }

            bool inside = m_Reader.Contains(m_Card.GetComponent<Rigidbody>());
            if (inside && !m_WasInside && Time.time - m_LastTap >= m_TapCooldown)
            {
                m_LastTap = Time.time;
                m_Taps++;
                if (m_Taps <= m_FailedTaps) Fail();
                else Approve();
            }
            m_WasInside = inside;
        }

        private void Fail()
        {
            SynthAudio.PlayAt(SynthSound.CardError, m_Reader.transform.position, 0.8f);
            if (m_Screen != null) m_Screen.SetStatus("Card not read. Please try again.", SmartMachineScreen.Bad);
            SetProgress($"<color=#FF8A80>Card not read...</color> Pull it away and tap again <color=#E6E6EA>({m_Taps}/{m_FailedTaps + 1})</color>");
        }

        private void Approve()
        {
            SynthAudio.PlayAt(SynthSound.CardApproved, m_Reader.transform.position, 0.9f);
            string snack = m_Snack != null ? m_Snack.ChosenName : "Snack";
            if (m_Screen != null) m_Screen.SetStatus($"Payment approved! Enjoy your {snack}.", SmartMachineScreen.Good);
            SetProgress("<color=#6EE787>Payment approved!</color>");
            if (m_Snack != null && m_Snack.ChosenItem != null) StartCoroutine(DropSnack(m_Snack.ChosenItem));
            Complete();
        }

        private IEnumerator DropSnack(XRGrabInteractable bag)
        {
            yield return new WaitForSeconds(0.4f);
            if (m_Outlet == null) yield break;
            GameObject go = bag.gameObject;
            Vector3 spawn = m_Outlet.position + Vector3.up * 0.18f;
            go.transform.position = spawn;
            Rigidbody body = bag.GetComponent<Rigidbody>();
            if (body != null) body.position = spawn; // so interpolation doesn't start from where it was hidden
            go.SetActive(true);
            if (body != null)
            {
                body.isKinematic = false;
                body.linearVelocity = Vector3.down * 0.5f;
                body.angularVelocity = Vector3.zero;
            }
            yield return new WaitForSeconds(0.25f);
            SynthAudio.PlayAt(SynthSound.SnackDrop, m_Outlet.position, 0.9f);
        }
    }
}

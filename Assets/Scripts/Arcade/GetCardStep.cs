using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: get your arcade card. Press "Get card" on the register's screen (behind the prize
    /// counter); the card slides up out of the grey card slot. Completes when you pick it up.
    /// </summary>
    public class GetCardStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string TextColor = "#E6E6EA";
        private const string HintColor = "#8A8A92";

        [SerializeField] private Button m_Button;
        [Tooltip("Text on the register screen.")]
        [SerializeField] private TMP_Text m_Screen;
        [SerializeField] private XRGrabInteractable m_Card;
        [Tooltip("Where the card lies once it's out (its pose). It slides into this from inside the slot.")]
        [SerializeField] private Transform m_CardOut;
        [SerializeField] private float m_SlideDistance = 0.06f;
        [Tooltip("World direction the card comes out of the slot (flat, toward the staff side).")]
        [SerializeField] private Vector3 m_SlideDirection = Vector3.forward;
        [SerializeField] private float m_SlideTime = 0.7f;

        private bool m_Pressed;
        private bool m_Out;

        /// <summary>The screen's button until it's pressed, then the card.</summary>
        public override Transform GuideTarget
        {
            get
            {
                if (!m_Pressed && m_Button != null) return m_Button.transform;
                return m_Card != null ? m_Card.transform : base.GuideTarget;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (m_Button != null) m_Button.gameObject.SetActive(false);
            SetScreen("<b>ARCADE</b>\n<size=60%>Welcome!</size>");
        }

        protected override void OnBegin()
        {
            m_Pressed = false;
            m_Out = false;
            if (m_Button != null)
            {
                m_Button.gameObject.SetActive(true);
                m_Button.interactable = true;
                m_Button.onClick.AddListener(OnPress);
            }
            SetScreen("<b>ARCADE CARD</b>\n<size=60%>Press the button</size>");
            SetProgress($"<color={TextColor}>Go behind the counter and press <b>Get card</b> on the register screen</color>\n" +
                        $"<color={HintColor}><i>Point at the button and pull the trigger</i></color>");
        }

        protected override void OnEnd()
        {
            if (m_Button != null) m_Button.onClick.RemoveListener(OnPress);
        }

        private void OnPress()
        {
            if (!IsActive || m_Pressed) return;
            m_Pressed = true;
            if (m_Button != null) m_Button.gameObject.SetActive(false);
            SetScreen("<b>PRINTING...</b>");
            SetProgress($"<color={TextColor}>Your card is coming out of the <b>grey slot</b>...</color>");
            StartCoroutine(SlideOut());
        }

        private IEnumerator SlideOut()
        {
            if (m_Card == null || m_CardOut == null) yield break;
            SynthAudio.PlayAt(SynthSound.CardDispense, m_CardOut.position, 0.9f);
            GameObject go = m_Card.gameObject;
            Rigidbody body = m_Card.GetComponent<Rigidbody>();
            Vector3 to = m_CardOut.position;
            Vector3 from = to - m_SlideDirection.normalized * m_SlideDistance; // starts mostly inside the slot
            go.transform.SetPositionAndRotation(from, m_CardOut.rotation);
            var free = m_Card.GetComponent<FreeAfterGrab>();
            if (free != null) free.Present();
            else if (body != null) body.isKinematic = true;
            if (body != null) body.position = from;
            go.SetActive(true);
            m_Card.enabled = false; // can't be grabbed while it's still sliding out

            for (float t = 0f; t < m_SlideTime; t += Time.deltaTime)
            {
                go.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / m_SlideTime));
                yield return null;
            }
            go.transform.position = to;
            if (body != null) body.position = to;
            m_Card.enabled = true;
            m_Out = true;
            SetScreen($"<b><color={OnColor}>READY!</color></b>\n<size=60%>Take your card</size>");
            SetProgress($"<color={OnColor}>Here it is!</color> <color={TextColor}>Grab your arcade card</color>");
        }

        private void Update()
        {
            if (!IsActive || !m_Out || m_Card == null) return;
            if (GrabUtil.IsHeldByHand(m_Card))
            {
                SetScreen("<b>HAVE FUN!</b>");
                SetProgress($"<color={OnColor}>Got it! Let's play some games.</color>");
                Complete();
            }
        }

        private void SetScreen(string text)
        {
            if (m_Screen != null) m_Screen.text = text;
        }
    }
}

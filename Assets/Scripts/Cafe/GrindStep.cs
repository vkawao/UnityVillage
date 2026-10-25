using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRVillage
{
    /// <summary>
    /// Action: grind the beans. The portafilter has to sit under the grinder (in its socket);
    /// then every full turn of the crank fills the basket a bit more. Completes after N turns.
    /// </summary>
    public class GrindStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string OffColor = "#8A8A92";
        private const string TextColor = "#E6E6EA";

        [SerializeField] private XRGrabInteractable m_Portafilter;
        [SerializeField] private XRSocketInteractor m_GrinderSocket;
        [SerializeField] private CrankHandle m_Crank;
        [SerializeField] private int m_Turns = 3;
        [Tooltip("Coffee grounds in the basket; grows as you grind.")]
        [SerializeField] private Transform m_Grounds;
        [Tooltip("Sparkles at the grounds when done (optional).")]
        [SerializeField] private QuestCelebration m_Sparkle;

        private Vector3 m_GroundsScale;
        private float m_Degrees;
        private bool m_WasDocked;

        public bool PortafilterDocked
        {
            get
            {
                if (m_GrinderSocket == null || !m_GrinderSocket.hasSelection) return false;
                var held = m_GrinderSocket.interactablesSelected;
                for (int i = 0; i < held.Count; i++)
                {
                    if (ReferenceEquals(held[i], m_Portafilter)) return true;
                }
                return false;
            }
        }

        public override Transform GuideTarget
        {
            get
            {
                if (PortafilterDocked && m_Crank != null) return m_Crank.transform;
                if (m_Portafilter != null && GrabUtil.IsHeldByHand(m_Portafilter) && m_GrinderSocket != null)
                    return m_GrinderSocket.transform;
                return m_Portafilter != null ? m_Portafilter.transform : base.GuideTarget;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (m_Grounds != null)
            {
                m_GroundsScale = m_Grounds.localScale;
                m_Grounds.gameObject.SetActive(false);
            }
        }

        protected override void OnBegin()
        {
            if (m_Portafilter == null || m_GrinderSocket == null || m_Crank == null)
            {
                Debug.LogWarning("[Quest] GrindStep is missing the portafilter, grinder socket or crank.", this);
                return;
            }
            m_Degrees = 0f;
            m_Crank.Turned += OnTurned;
            m_WasDocked = PortafilterDocked;
            UpdateProgress();
        }

        protected override void OnEnd()
        {
            if (m_Crank != null) m_Crank.Turned -= OnTurned;
        }

        private void Update()
        {
            if (!IsActive) return;
            bool docked = PortafilterDocked;
            if (docked == m_WasDocked) return;
            m_WasDocked = docked;
            UpdateProgress();
        }

        private void OnTurned(float degrees)
        {
            if (!IsActive || !PortafilterDocked) return; // grinding into nothing doesn't count
            int before = (int)(m_Degrees / 360f);
            m_Degrees += degrees;
            ShowGrounds(Mathf.Clamp01(m_Degrees / (m_Turns * 360f)));

            if (m_Degrees >= m_Turns * 360f)
            {
                if (m_Sparkle != null && m_Grounds != null) m_Sparkle.Sparkle(m_Grounds.position, 40);
                SynthAudio.PlayAt(SynthSound.Ding, m_Crank.transform.position, 0.7f);
                SetProgress($"<color={OnColor}>Freshly ground! Smells great.</color>");
                Complete();
                return;
            }
            if ((int)(m_Degrees / 360f) != before) UpdateProgress();
        }

        private void ShowGrounds(float fill)
        {
            if (m_Grounds == null) return;
            m_Grounds.gameObject.SetActive(fill > 0f);
            m_Grounds.localScale = new Vector3(m_GroundsScale.x, m_GroundsScale.y * Mathf.Max(fill, 0.1f), m_GroundsScale.z);
        }

        private void UpdateProgress()
        {
            bool docked = PortafilterDocked;
            int turns = Mathf.Min((int)(m_Degrees / 360f), m_Turns);
            string hint = docked
                ? "Grab the crank on top of the grinder and turn it in circles"
                : "Take the portafilter out of the coffee machine and put it under the grinder";
            SetProgress(
                $"<color={(docked ? OnColor : OffColor)}><b>PORTAFILTER</b> {(docked ? "UNDER GRINDER" : "NOT THERE")}</color>" +
                $"<space=1.2em><color={(turns > 0 ? OnColor : OffColor)}><b>TURNS</b> {turns}/{m_Turns}</color>\n" +
                $"<color={TextColor}><i>{hint}</i></color>");
        }
    }
}

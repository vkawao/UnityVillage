using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRVillage
{
    /// <summary>
    /// Action: try out the gear. Picking up (or shaking) the guitar strums it:
    ///   amp off              -> thin unplugged strum from the guitar
    ///   amp on, effect off   -> clean strum from the amp
    ///   amp on, effect on    -> distorted lead lick from the amp
    /// Completes when the player presses the Done! button.
    /// </summary>
    public class TryGearStep : QuestStep
    {
        [SerializeField] private XRGrabInteractable m_Guitar;
        [SerializeField] private AmpTestStep m_AmpTest;
        [SerializeField] private EffectPedal[] m_Pedals;
        [SerializeField] private Button m_DoneButton;

        [Header("Strum Detection")]
        [Tooltip("Shaking the held guitar faster than this (m/s, relative to the head) counts as a strum.")]
        [SerializeField] private float m_ShakeSpeed = 2.2f;
        [SerializeField] private float m_StrumCooldown = 0.6f;
        [SerializeField, Range(0f, 1f)] private float m_UnpluggedVolume = 0.8f;

        private float m_LastStrumTime = -10f;
        private Transform m_Head;
        private Vector3 m_LastRelPos;
        private bool m_HasLastRelPos;

        private AmpUnit Amp => m_AmpTest != null ? m_AmpTest.WorkingAmp : null;

        protected override void OnBegin()
        {
            if (m_Guitar != null) m_Guitar.selectEntered.AddListener(OnGuitarGrabbed);
            if (m_DoneButton != null)
            {
                m_DoneButton.interactable = true;
                m_DoneButton.onClick.AddListener(OnDone);
            }
            UpdateProgress();
        }

        protected override void OnEnd()
        {
            if (m_Guitar != null) m_Guitar.selectEntered.RemoveListener(OnGuitarGrabbed);
            if (m_DoneButton != null) m_DoneButton.onClick.RemoveListener(OnDone);
        }

        private void OnDone()
        {
            if (IsActive) Complete();
        }

        private void OnGuitarGrabbed(SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor) return;
            Strum();
        }

        private void Update()
        {
            if (!IsActive) return;
            UpdateProgress();

            if (!GrabUtil.IsHeldByHand(m_Guitar))
            {
                m_HasLastRelPos = false;
                return;
            }

            if (m_Head == null)
            {
                Camera cam = Camera.main;
                if (cam == null) return;
                m_Head = cam.transform;
            }

            // Relative to the head so walking with the joystick doesn't count as shaking.
            Vector3 rel = m_Guitar.transform.position - m_Head.position;
            if (m_HasLastRelPos && Time.deltaTime > 0f)
            {
                float speed = (rel - m_LastRelPos).magnitude / Time.deltaTime;
                if (speed > m_ShakeSpeed) Strum();
            }
            m_LastRelPos = rel;
            m_HasLastRelPos = true;
        }

        [ContextMenu("Strum")]
        public void Strum()
        {
            if (Time.time - m_LastStrumTime < m_StrumCooldown) return;
            m_LastStrumTime = Time.time;

            AmpUnit amp = Amp;
            if (amp != null && amp.IsOn)
            {
                amp.PlayRiff(AnyPedalOn() ? SynthAudio.LeadLick : SynthAudio.CleanStrum);
            }
            else if (m_Guitar != null)
            {
                AudioSource.PlayClipAtPoint(SynthAudio.UnpluggedStrum, m_Guitar.transform.position, m_UnpluggedVolume);
            }
        }

        private bool AnyPedalOn()
        {
            if (m_Pedals == null) return false;
            foreach (EffectPedal p in m_Pedals)
            {
                if (p != null && p.isActiveAndEnabled && p.IsOn) return true;
            }
            return false;
        }

        private const string OnColor = "#6EE787";
        private const string OffColor = "#8A8A92";
        private const string TextColor = "#E6E6EA";

        /// <summary>
        /// Three tidy lines under "Try out your gear!":
        ///   how to play / AMP ON|OFF  EFFECT ON|OFF status chips / one hint for the next thing to try.
        /// </summary>
        private void UpdateProgress()
        {
            AmpUnit amp = Amp;
            bool ampOn = amp != null && amp.IsOn;
            bool fxOn = AnyPedalOn();

            string hint = !ampOn ? "Turn up the amp's volume knob to plug in"
                : !fxOn ? "Grab the pedal's switch for a distorted sound"
                : "Rock on! Press <b>Done!</b> when you're finished";

            SetProgress(
                $"<color={TextColor}>Pick up or shake the Les Paul to play it</color>\n" +
                $"{Chip("AMP", ampOn)}<space=1.2em>{Chip("EFFECT", fxOn)}\n" +
                $"<color={OffColor}><i>{hint}</i></color>");
        }

        private static string Chip(string label, bool on) =>
            $"<color={(on ? OnColor : OffColor)}><b>{label}</b> {(on ? "ON" : "OFF")}</color>";
    }
}

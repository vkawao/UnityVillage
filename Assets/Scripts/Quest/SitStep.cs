using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace VRVillage
{
    /// <summary>
    /// Action: sit down (e.g. on a bench), as a deliberate choice:
    ///   - press the "Sit down" button at the seat (ray + trigger, like any UI button), or
    ///   - teleport onto the seat's TeleportationAnchor with the teleport ray.
    /// Pressing the button blinks the view (fade out, move onto the seat facing forward,
    /// creak, fade in) so it reads as sitting down rather than a jump.
    /// The seat transform is where the player ends up (on the ground) and which way they face.
    /// Completes once the player's head is over the seat.
    /// </summary>
    public class SitStep : QuestStep
    {
        [Tooltip("Floor point at the seat; its forward is the direction the seated player faces.")]
        [SerializeField] private Transform m_Seat;
        [Tooltip("The \"Sit down\" button.")]
        [SerializeField] private Button m_SitButton;
        [Tooltip("Optional: blinks the view while moving the player.")]
        [SerializeField] private ScreenFader m_Fader;
        [SerializeField] private float m_FadeOut = 0.25f;
        [SerializeField] private float m_FadeIn = 0.35f;
        [SerializeField] private SynthSound m_SitSound = SynthSound.BenchCreak;
        [Tooltip("Head this close (m, horizontally) to the seat point counts as seated.")]
        [SerializeField] private float m_SeatedRadius = 0.35f;
        [Tooltip("Old behaviour: walking this close snaps you onto the seat. 0 = off (sit only on purpose).")]
        [SerializeField] private float m_SnapRadius = 0f;

        private Transform m_Head;
        private TeleportationProvider m_Provider;
        private float m_NextSnap;
        private bool m_Sitting;

        /// <summary>Points at the Sit down button (or the seat if there's no button).</summary>
        public override Transform GuideTarget =>
            m_SitButton != null ? m_SitButton.transform : m_Seat != null ? m_Seat : base.GuideTarget;

        protected override void OnBegin()
        {
            m_Sitting = false;
            if (m_SitButton != null)
            {
                m_SitButton.interactable = true;
                m_SitButton.onClick.AddListener(OnSitPressed);
            }
            SetProgress("Press <b>Sit down</b> at the bench (or teleport onto the seat)");
        }

        protected override void OnEnd()
        {
            if (m_SitButton != null) m_SitButton.onClick.RemoveListener(OnSitPressed);
        }

        private void OnSitPressed()
        {
            if (!IsActive || m_Sitting) return;
            StartCoroutine(SitDown());
        }

        private IEnumerator SitDown()
        {
            m_Sitting = true;
            if (m_SitButton != null) m_SitButton.interactable = false;
            SynthAudio.PlayAt(SynthSound.ButtonBeep, m_SitButton != null ? m_SitButton.transform.position : m_Seat.position, 0.6f);

            if (m_Fader != null) yield return m_Fader.FadeTo(1f, m_FadeOut);
            Teleport();
            // Teleports are applied by the locomotion system on a later frame.
            yield return null;
            yield return null;
            SynthAudio.PlayAt(m_SitSound, m_Seat.position + Vector3.up * 0.45f, 0.9f);
            if (m_Fader != null) yield return m_Fader.FadeTo(0f, m_FadeIn);

            m_Sitting = false;
            if (IsActive && m_SitButton != null) m_SitButton.interactable = true; // if the move didn't happen, let them retry
        }

        private void Update()
        {
            if (!IsActive || m_Seat == null) return;
            if (m_Head == null)
            {
                Camera cam = Camera.main;
                if (cam == null) return;
                m_Head = cam.transform;
            }

            Vector3 d = m_Head.position - m_Seat.position;
            d.y = 0f;
            float dist = d.magnitude;

            if (dist <= m_SeatedRadius)
            {
                // Arrived by the button or the teleport ray. Teleporting in directly still gets the creak.
                if (!m_Sitting) SynthAudio.PlayAt(m_SitSound, m_Seat.position + Vector3.up * 0.45f, 0.9f);
                SetProgress("<color=#6EE787>Ahh, nice and comfy.</color>");
                Complete();
                return;
            }

            if (m_SnapRadius > 0f && dist <= m_SnapRadius && !m_Sitting && Time.time >= m_NextSnap)
            {
                m_NextSnap = Time.time + 1.5f;
                StartCoroutine(SitDown());
            }
        }

        private void Teleport()
        {
            if (m_Provider == null) m_Provider = Object.FindFirstObjectByType<TeleportationProvider>();
            if (m_Provider == null)
            {
                Debug.LogWarning("[Quest] SitStep: no TeleportationProvider in the scene.", this);
                return;
            }
            m_Provider.QueueTeleportRequest(new TeleportRequest
            {
                destinationPosition = m_Seat.position,
                destinationRotation = m_Seat.rotation,
                matchOrientation = MatchOrientation.TargetUpAndForward,
                requestTime = Time.time,
            });
        }
    }
}

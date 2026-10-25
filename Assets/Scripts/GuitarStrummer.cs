using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Attached to guitars (RedStrat, LesPaul, Yamaha, etc.).
    /// Fulfills Action 2 (Grab Electric Guitar) and provides interactive audio feedback when strummed or picked up.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(Rigidbody))]
    public class GuitarStrummer : MonoBehaviour
    {
        [Header("Audio Settings")]
        [SerializeField] private AudioSource m_AudioSource;
        [SerializeField] private AudioClip[] m_StrumClips;

        [Tooltip("Master switch for all guitar sounds (grab + strum). Off for now; a later action can turn it on via SoundEnabled.")]
        [SerializeField] private bool m_SoundEnabled = false;

        [Header("Synthesizer Fallback")]
        [Tooltip("If no AudioClip is provided, synthesizes a rich acoustic chord on strum.")]
        [SerializeField] private bool m_UseSyntheticAudioIfEmpty = true;

        private XRGrabInteractable m_GrabInteractable;
        private Rigidbody m_Rigidbody;
        private float m_LastStrumTime;

        public bool SoundEnabled
        {
            get => m_SoundEnabled;
            set => m_SoundEnabled = value;
        }

        private void Awake()
        {
            m_GrabInteractable = GetComponent<XRGrabInteractable>();
            m_Rigidbody = GetComponent<Rigidbody>();

            if (m_AudioSource == null)
            {
                m_AudioSource = GetComponent<AudioSource>();
                if (m_AudioSource == null)
                {
                    m_AudioSource = gameObject.AddComponent<AudioSource>();
                    m_AudioSource.spatialBlend = 1.0f; // Full 3D spatial audio
                    m_AudioSource.minDistance = 0.5f;
                    m_AudioSource.maxDistance = 15f;
                    m_AudioSource.playOnAwake = false;
                }
            }

            m_GrabInteractable.selectEntered.AddListener(OnGrabbed);
        }

        private void OnDestroy()
        {
            if (m_GrabInteractable != null)
            {
                m_GrabInteractable.selectEntered.RemoveListener(OnGrabbed);
            }
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            // Docking into a rack socket also fires selectEntered; only react to hands.
            if (args.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor) return;

            // Soft chord resonance upon picking up
            PlayStrum(0.5f);
        }

        /// <summary>
        /// Call when a pick, hand, or controller hits the string collider.
        /// </summary>
        public void PlayStrum(float intensity = 1.0f)
        {
            if (!m_SoundEnabled) return;
            if (Time.time - m_LastStrumTime < 0.12f) return;
            m_LastStrumTime = Time.time;

            if (m_StrumClips != null && m_StrumClips.Length > 0)
            {
                int idx = Random.Range(0, m_StrumClips.Length);
                if (m_StrumClips[idx] != null)
                {
                    m_AudioSource.pitch = Random.Range(0.96f, 1.04f);
                    m_AudioSource.PlayOneShot(m_StrumClips[idx], Mathf.Clamp01(intensity));
                    return;
                }
            }

            if (m_UseSyntheticAudioIfEmpty)
            {
                PlaySyntheticChord(intensity);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Trigger strumming if a controller or finger passes through the string area
            if (other.gameObject.name.ToLower().Contains("hand") || 
                other.gameObject.name.ToLower().Contains("controller") ||
                other.gameObject.name.ToLower().Contains("interactor") ||
                other.CompareTag("Player") || other.CompareTag("GameController"))
            {
                PlayStrum(0.85f);
            }
        }

        /// <summary>
        /// Generates a quick procedural 6-string acoustic resonance if no WAV is assigned.
        /// </summary>
        private void PlaySyntheticChord(float intensity)
        {
            int sampleRate = 22050;
            float duration = 1.2f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            // E Major chord frequencies (E2, B2, E3, G#3, B3, E4)
            float[] freqs = new float[] { 82.41f, 123.47f, 164.81f, 207.65f, 246.94f, 329.63f };

            for (int s = 0; s < freqs.Length; s++)
            {
                float freq = freqs[s];
                int strumDelay = (int)(s * 0.025f * sampleRate); // Arpeggiated strum stroke
                for (int i = strumDelay; i < numSamples; i++)
                {
                    float t = (float)(i - strumDelay) / sampleRate;
                    float decay = Mathf.Exp(-t * (3.0f + s * 0.5f));
                    float osc = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f +
                                Mathf.Sin(4f * Mathf.PI * freq * t) * 0.3f +
                                Mathf.Sin(6f * Mathf.PI * freq * t) * 0.1f;
                    samples[i] += osc * decay * (0.15f * intensity);
                }
            }

            AudioClip synthClip = AudioClip.Create("SynthChord", numSamples, 1, sampleRate, false);
            synthClip.SetData(samples, 0);

            m_AudioSource.pitch = Random.Range(0.98f, 1.02f);
            m_AudioSource.PlayOneShot(synthClip, Mathf.Clamp01(intensity));
        }
    }
}

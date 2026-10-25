using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRVillage
{
    /// <summary>
    /// Attached to Wall Guitar Rack sockets.
    /// Manages socket snapping, alignment, and snap sound effects for guitars.
    /// </summary>
    [RequireComponent(typeof(XRSocketInteractor))]
    public class WallRackSocket : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioSource m_AudioSource;
        [SerializeField] private AudioClip m_DockSound;
        [SerializeField] private AudioClip m_UndockSound;

        [Tooltip("Play the dock/undock click. Off for now (no feedback on the rack action yet).")]
        [SerializeField] private bool m_SoundEnabled = false;

        private XRSocketInteractor m_Socket;

        private void Awake()
        {
            m_Socket = GetComponent<XRSocketInteractor>();

            if (m_AudioSource == null)
            {
                m_AudioSource = GetComponent<AudioSource>();
                if (m_AudioSource == null)
                {
                    m_AudioSource = gameObject.AddComponent<AudioSource>();
                    m_AudioSource.spatialBlend = 1.0f;
                    m_AudioSource.minDistance = 0.3f;
                    m_AudioSource.maxDistance = 10f;
                    m_AudioSource.playOnAwake = false;
                }
            }

            m_Socket.selectEntered.AddListener(OnObjectDocked);
            m_Socket.selectExited.AddListener(OnObjectUndocked);
        }

        private void OnDestroy()
        {
            if (m_Socket != null)
            {
                m_Socket.selectEntered.RemoveListener(OnObjectDocked);
                m_Socket.selectExited.RemoveListener(OnObjectUndocked);
            }
        }

        private void OnObjectDocked(SelectEnterEventArgs args)
        {
            if (!m_SoundEnabled) return;

            if (m_DockSound != null)
            {
                m_AudioSource.PlayOneShot(m_DockSound, 0.7f);
            }
            else
            {
                // Procedural wood tap click
                PlaySyntheticSnap(800f, 0.05f);
            }
        }

        private void OnObjectUndocked(SelectExitEventArgs args)
        {
            if (!m_SoundEnabled) return;

            if (m_UndockSound != null)
            {
                m_AudioSource.PlayOneShot(m_UndockSound, 0.6f);
            }
            else
            {
                PlaySyntheticSnap(500f, 0.04f);
            }
        }

        private void PlaySyntheticSnap(float freq, float duration)
        {
            int sampleRate = 22050;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 60f) * 0.3f;
            }
            AudioClip clip = AudioClip.Create("Snap", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            m_AudioSource.PlayOneShot(clip);
        }
    }
}

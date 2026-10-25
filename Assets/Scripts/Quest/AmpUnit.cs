using UnityEngine;
using Unity.VRTemplate;

namespace VRVillage
{
    /// <summary>
    /// One amp with a grabbable volume knob (XRKnob). Rolling the volume up turns it on
    /// (pilot light). While noise is enabled (amp test), a faulty amp buzzes and the
    /// working amp hums, both scaled by the volume. Also plays guitar riffs "through" the amp.
    /// </summary>
    public class AmpUnit : MonoBehaviour
    {
        [SerializeField] private XRKnob m_Knob;
        [SerializeField] private AudioSource m_AudioSource;
        [SerializeField] private Renderer m_PilotLight;

        [Tooltip("Knob value (0-1) above which the amp counts as on.")]
        [SerializeField] private float m_OnThreshold = 0.08f;
        [SerializeField, Range(0f, 1f)] private float m_MaxNoiseVolume = 0.8f;
        [SerializeField] private Color m_LightOn = new Color(1f, 0.15f, 0.1f);
        [SerializeField] private Color m_LightOff = new Color(0.18f, 0.03f, 0.03f);

        private MaterialPropertyBlock m_Block;
        private bool m_LightState;
        private bool m_LightInitialised;
        private bool m_NoisePlaying;

        public bool IsBroken { get; set; }
        public bool NoiseEnabled { get; set; }
        public float Volume => m_Knob != null ? m_Knob.value : 0f;
        public bool IsOn => Volume > m_OnThreshold;

        private void Awake()
        {
            if (m_AudioSource == null) m_AudioSource = GetComponent<AudioSource>();
            if (m_Knob != null) m_Knob.value = 0f;
        }

        private void Update()
        {
            bool on = IsOn;
            if (!m_LightInitialised || on != m_LightState) SetLight(on);

            if (m_AudioSource == null) return;
            if (NoiseEnabled && on)
            {
                AudioClip noise = IsBroken ? SynthAudio.AmpBuzz : SynthAudio.AmpHum;
                if (!m_NoisePlaying || m_AudioSource.clip != noise || !m_AudioSource.isPlaying)
                {
                    m_AudioSource.clip = noise;
                    m_AudioSource.loop = true;
                    m_AudioSource.Play();
                    m_NoisePlaying = true;
                }
                m_AudioSource.volume = Volume * m_MaxNoiseVolume;
            }
            else if (m_NoisePlaying)
            {
                StopNoise();
            }
        }

        /// <summary>Silence the buzz/hum (a riff in progress is left alone).</summary>
        public void StopNoise()
        {
            if (!m_NoisePlaying) return;
            m_NoisePlaying = false;
            if (m_AudioSource != null)
            {
                m_AudioSource.Stop();
                m_AudioSource.loop = false;
            }
        }

        public void ResetKnob()
        {
            if (m_Knob != null) m_Knob.value = 0f;
        }

        /// <summary>Play a clip from the amp's speaker, louder with more volume. Restarts any riff.</summary>
        public void PlayRiff(AudioClip clip)
        {
            if (m_AudioSource == null || clip == null) return;
            m_NoisePlaying = false;
            m_AudioSource.Stop();
            m_AudioSource.loop = false;
            m_AudioSource.clip = clip;
            m_AudioSource.volume = Mathf.Lerp(0.35f, 1f, Volume);
            m_AudioSource.Play();
        }

        private void SetLight(bool on)
        {
            m_LightState = on;
            m_LightInitialised = true;
            if (m_PilotLight == null) return;
            m_Block ??= new MaterialPropertyBlock();
            m_PilotLight.GetPropertyBlock(m_Block);
            m_Block.SetColor("_BaseColor", on ? m_LightOn : m_LightOff);
            m_PilotLight.SetPropertyBlock(m_Block);
        }
    }
}

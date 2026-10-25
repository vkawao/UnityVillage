using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// A ceiling fan that spins about the vertical axis through its own origin (modelled on the hub).
    /// Spins up and down smoothly, with a soft whirr that follows the speed.
    /// </summary>
    public class CeilingFan : MonoBehaviour
    {
        [Tooltip("Full speed, degrees per second.")]
        [SerializeField] private float m_MaxSpeed = 420f;
        [Tooltip("Seconds from standstill to full speed (and back).")]
        [SerializeField] private float m_SpinUpTime = 2.5f;
        [SerializeField, Range(0f, 1f)] private float m_Volume = 0.08f;

        private float m_Speed;
        private AudioSource m_Audio;

        public bool IsOn { get; private set; }

        public void SetOn(bool on)
        {
            IsOn = on;
            if (!on || m_Volume <= 0f) return;
            if (m_Audio == null)
            {
                m_Audio = gameObject.AddComponent<AudioSource>();
                m_Audio.clip = SynthAudio.FanWhirr;
                m_Audio.loop = true;
                m_Audio.spatialBlend = 1f;
                m_Audio.minDistance = 0.8f;
                m_Audio.maxDistance = 8f;
                m_Audio.volume = 0f;
            }
            if (!m_Audio.isPlaying) m_Audio.Play();
        }

        private void Update()
        {
            float target = IsOn ? m_MaxSpeed : 0f;
            m_Speed = Mathf.MoveTowards(m_Speed, target, m_MaxSpeed / Mathf.Max(0.01f, m_SpinUpTime) * Time.deltaTime);
            if (m_Speed > 0f) transform.Rotate(Vector3.up, m_Speed * Time.deltaTime, Space.World);

            if (m_Audio == null) return;
            float k = m_Speed / Mathf.Max(1f, m_MaxSpeed);
            m_Audio.volume = m_Volume * k;
            m_Audio.pitch = 0.6f + 0.4f * k;
            if (k <= 0f && m_Audio.isPlaying) m_Audio.Stop();
        }
    }
}

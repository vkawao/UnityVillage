using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// Sparkle burst + short fanfare. Place this object where the celebration should
    /// happen. Play() = full burst + fanfare here. Sparkle() = small silent burst anywhere.
    /// If no fanfare clip is assigned, a brass-like fanfare is synthesized at startup.
    /// </summary>
    public class QuestCelebration : MonoBehaviour
    {
        [SerializeField] private ParticleSystem m_Particles;
        [SerializeField] private int m_BurstCount = 160;

        [Header("Audio")]
        [SerializeField] private AudioSource m_AudioSource;
        [Tooltip("Optional. Leave empty to use the built-in synthesized fanfare.")]
        [SerializeField] private AudioClip m_FanfareClip;
        [SerializeField, Range(0f, 1f)] private float m_Volume = 0.8f;

        private AudioClip m_SynthFanfare;

        private void Awake()
        {
            if (m_AudioSource == null) m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource == null)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
                m_AudioSource.playOnAwake = false;
                m_AudioSource.spatialBlend = 0.5f; // partly 3D so it's clearly "over there" but always audible
                m_AudioSource.minDistance = 2f;
                m_AudioSource.maxDistance = 25f;
            }

            if (m_FanfareClip == null) m_SynthFanfare = BuildFanfare();
        }

        /// <summary>Full celebration at this object's position.</summary>
        [ContextMenu("Play")]
        public void Play()
        {
            Burst(transform.position, m_BurstCount);
            AudioClip clip = m_FanfareClip != null ? m_FanfareClip : m_SynthFanfare;
            if (clip != null && m_AudioSource != null) m_AudioSource.PlayOneShot(clip, m_Volume);
        }

        /// <summary>Small silent sparkle at any position (e.g. a guitar that was just tuned).</summary>
        public void Sparkle(Vector3 position, int count = 45)
        {
            Burst(position, count);
        }

        private void Burst(Vector3 position, int count)
        {
            if (m_Particles == null) return;
            m_Particles.transform.position = position;
            m_Particles.Emit(count);
        }

        // ------------------------------------------------------------------
        // Synthesized fanfare: G4-C5-E5 pickup, then a held C major chord with vibrato.
        // ------------------------------------------------------------------
        private static AudioClip BuildFanfare()
        {
            const int rate = 44100;
            const float length = 1.9f;
            int n = (int)(rate * length);
            float[] buf = new float[n];

            // (start time, duration, frequency)
            AddBrass(buf, rate, 0.00f, 0.13f, 392.00f, 0.9f, false); // G4
            AddBrass(buf, rate, 0.13f, 0.13f, 523.25f, 0.9f, false); // C5
            AddBrass(buf, rate, 0.26f, 0.13f, 659.25f, 0.9f, false); // E5
            AddBrass(buf, rate, 0.40f, 1.40f, 783.99f, 1.0f, true);  // G5 (melody, held)
            AddBrass(buf, rate, 0.40f, 1.40f, 523.25f, 0.6f, true);  // C5
            AddBrass(buf, rate, 0.40f, 1.40f, 659.25f, 0.6f, true);  // E5
            AddBrass(buf, rate, 0.40f, 1.40f, 261.63f, 0.5f, true);  // C4 (bass)

            Normalize(buf, 0.85f);
            AudioClip clip = AudioClip.Create("QuestFanfare", n, 1, rate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        private static void AddBrass(float[] buf, int rate, float start, float dur, float freq, float gain, bool vibrato)
        {
            int s0 = (int)(start * rate);
            int len = (int)((dur + 0.25f) * rate); // + release tail
            float release = 0.25f;
            for (int i = 0; i < len && s0 + i < buf.Length; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Clamp01(t / 0.025f);                       // attack
                if (t > dur) env *= Mathf.Clamp01(1f - (t - dur) / release); // release
                else env *= 1f - 0.25f * (t / Mathf.Max(dur, 0.01f));        // slight sag

                float f = freq;
                if (vibrato) f *= 1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 5.5f * t) * Mathf.Clamp01((t - 0.2f) * 3f);

                // Brass-ish: strong low harmonics, brighter during the attack.
                float bright = 1f + 1.5f * Mathf.Exp(-t * 12f);
                float v = 0f;
                for (int h = 1; h <= 7; h++)
                {
                    float amp = 1f / Mathf.Pow(h, 1.6f / bright);
                    v += amp * Mathf.Sin(2f * Mathf.PI * f * h * t);
                }
                buf[s0 + i] += v * env * gain * 0.2f;
            }
        }

        private static void Normalize(float[] buf, float peak)
        {
            float max = 0f;
            for (int i = 0; i < buf.Length; i++) max = Mathf.Max(max, Mathf.Abs(buf[i]));
            if (max < 1e-5f) return;
            float k = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
        }
    }
}

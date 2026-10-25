using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRVillage
{
    /// <summary>
    /// Tuning mini-game for one guitar. While active, every time the player picks the
    /// guitar up by hand OR gives it a quick shake while holding it, it "strums" a chord.
    /// The chord starts badly out of tune and gets closer to in-tune with each strum.
    /// On the final strum it rings clean, plays a chime, sparkles, and raises Tuned.
    /// All sound is synthesized (no audio files needed).
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class GuitarTuner : MonoBehaviour
    {
        [Header("Tuning")]
        [Tooltip("Strums needed to go from fully out of tune to in tune (including the first, worst one).")]
        [SerializeField, Min(2)] private int m_StrumsToTune = 5;
        [Tooltip("How far out of tune each string starts, in cents (100 cents = 1 semitone).")]
        [SerializeField] private float m_MaxDetuneCents = 70f;
        [Tooltip("0 = derive a stable random detune from the object's name.")]
        [SerializeField] private int m_Seed = 0;

        [Header("Strum Detection")]
        [Tooltip("Picking the guitar up by hand counts as a strum.")]
        [SerializeField] private bool m_StrumOnGrab = true;
        [Tooltip("Shaking the held guitar faster than this (m/s, relative to the head) counts as a strum.")]
        [SerializeField] private float m_ShakeSpeed = 2.2f;
        [SerializeField] private float m_StrumCooldown = 0.5f;

        [Header("Feedback")]
        [SerializeField, Range(0f, 1f)] private float m_Volume = 0.9f;
        [Tooltip("Optional: sparkles when this guitar becomes tuned.")]
        [SerializeField] private QuestCelebration m_Sparkle;

        // Open G major: G2 B2 D3 G3 B3 G4
        private static readonly float[] s_Strings = { 98.00f, 123.47f, 146.83f, 196.00f, 246.94f, 392.00f };

        private XRGrabInteractable m_Grab;
        private AudioSource m_AudioSource;
        private float[] m_DetuneCents;
        private int m_Strums;
        private bool m_Active;
        private float m_LastStrumTime = -10f;
        private Transform m_Head;
        private Vector3 m_LastRelPos;
        private bool m_HasLastRelPos;
        private static AudioClip s_Chime;

        public int Strums => m_Strums;
        public int StrumsToTune => m_StrumsToTune;
        public bool IsTuned => m_Strums >= m_StrumsToTune;

        public event Action<GuitarTuner> Strummed;
        public event Action<GuitarTuner> Tuned;

        private void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource == null)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
                m_AudioSource.playOnAwake = false;
                m_AudioSource.spatialBlend = 1f;
                m_AudioSource.minDistance = 0.6f;
                m_AudioSource.maxDistance = 15f;
            }

            var rng = new System.Random(m_Seed != 0 ? m_Seed : StableHash(name));
            m_DetuneCents = new float[s_Strings.Length];
            for (int i = 0; i < m_DetuneCents.Length; i++)
            {
                float mag = Mathf.Lerp(0.4f, 1f, (float)rng.NextDouble()) * m_MaxDetuneCents;
                m_DetuneCents[i] = rng.NextDouble() < 0.5 ? -mag : mag;
            }

            m_Grab.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDestroy()
        {
            if (m_Grab != null) m_Grab.selectEntered.RemoveListener(OnSelectEntered);
        }

        /// <summary>Turn the mini-game on/off (strums are ignored while off).</summary>
        public void SetTuningActive(bool active)
        {
            m_Active = active;
            m_HasLastRelPos = false;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (!m_Active || !m_StrumOnGrab) return;
            if (args.interactorObject is XRSocketInteractor) return; // docking on the rack isn't a pick-up
            Strum();
        }

        private void Update()
        {
            if (!m_Active || !IsHeldByHand())
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

            // Measure relative to the head so walking with the joystick doesn't count as shaking.
            Vector3 rel = transform.position - m_Head.position;
            if (m_HasLastRelPos && Time.deltaTime > 0f)
            {
                float speed = (rel - m_LastRelPos).magnitude / Time.deltaTime;
                if (speed > m_ShakeSpeed) Strum();
            }
            m_LastRelPos = rel;
            m_HasLastRelPos = true;
        }

        private bool IsHeldByHand()
        {
            if (!m_Grab.isSelected) return false;
            var selectors = m_Grab.interactorsSelecting;
            for (int i = 0; i < selectors.Count; i++)
            {
                if (!(selectors[i] is XRSocketInteractor)) return true; // a hand/controller is holding it
            }
            return false;
        }

        /// <summary>Play the next, slightly-more-in-tune chord.</summary>
        [ContextMenu("Strum")]
        public void Strum()
        {
            if (Time.time - m_LastStrumTime < m_StrumCooldown) return;
            m_LastStrumTime = Time.time;

            if (IsTuned)
            {
                PlayChord(0f); // already tuned: just ring clean
                return;
            }

            m_Strums++;
            // First strum = fully out of tune (1), last strum = in tune (0).
            float remaining = 1f - (float)(m_Strums - 1) / (m_StrumsToTune - 1);
            PlayChord(Mathf.Clamp01(remaining));
            Strummed?.Invoke(this);

            if (IsTuned)
            {
                if (s_Chime == null) s_Chime = BuildChime();
                m_AudioSource.PlayOneShot(s_Chime, m_Volume * 0.6f);
                if (m_Sparkle != null) m_Sparkle.Sparkle(transform.position + Vector3.up * 0.2f);
                Debug.Log($"[Tuner] {name} is in tune!");
                Tuned?.Invoke(this);
            }
        }

        private void PlayChord(float detuneAmount)
        {
            AudioClip clip = BuildChord(m_DetuneCents, detuneAmount);
            m_AudioSource.pitch = 1f;
            m_AudioSource.PlayOneShot(clip, m_Volume);
        }

        // ------------------------------------------------------------------
        // Synthesis
        // ------------------------------------------------------------------

        /// <summary>
        /// Plucked-string chord using decaying harmonics (higher harmonics die faster,
        /// like a real string). Detuned strings beat against each other, which is what
        /// makes it sound "out of tune".
        /// </summary>
        private static AudioClip BuildChord(float[] detuneCents, float amount)
        {
            const int rate = 22050;
            const float length = 2.0f;
            const int harmonics = 7;
            int n = (int)(rate * length);
            float[] buf = new float[n];

            for (int s = 0; s < s_Strings.Length; s++)
            {
                float cents = detuneCents[s] * amount;
                float f0 = s_Strings[s] * Mathf.Pow(2f, cents / 1200f);
                int startSample = (int)(s * 0.028f * rate); // downstroke, low string first
                float stringGain = s < 3 ? 1f : 0.8f;

                for (int h = 1; h <= harmonics; h++)
                {
                    float f = f0 * h;
                    if (f > rate * 0.45f) break;

                    // Pluck near the bridge: brighter tone, fewer missing harmonics.
                    float amp = Mathf.Abs(Mathf.Sin(h * Mathf.PI * 0.18f)) / h;
                    float decay = 1.3f + 0.9f * h;

                    // Oscillator by complex rotation (cheap, no per-sample Sin).
                    float w = 2f * Mathf.PI * f / rate;
                    float c = Mathf.Cos(w), sn = Mathf.Sin(w);
                    float x = 1f, y = 0f;
                    float envStep = Mathf.Exp(-decay / rate);
                    float env = amp * stringGain;

                    for (int i = startSample; i < n; i++)
                    {
                        float nx = x * c - y * sn;
                        y = x * sn + y * c;
                        x = nx;
                        int k = i - startSample;
                        float attack = k < 60 ? k / 60f : 1f;
                        buf[i] += y * env * attack;
                        env *= envStep;
                    }
                }
            }

            Normalize(buf, 0.8f);
            AudioClip clip = AudioClip.Create("TunerChord", n, 1, rate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        private static AudioClip BuildChime()
        {
            const int rate = 22050;
            const float length = 1.2f;
            int n = (int)(rate * length);
            float[] buf = new float[n];
            float[] freqs = { 1567.98f, 2349.32f, 3135.96f };
            float[] amps = { 1f, 0.55f, 0.3f };
            for (int p = 0; p < freqs.Length; p++)
            {
                int start = p == 0 ? 0 : (int)(0.09f * rate); // two-note "ding-ding"
                for (int i = start; i < n; i++)
                {
                    float t = (float)(i - start) / rate;
                    buf[i] += amps[p] * Mathf.Sin(2f * Mathf.PI * freqs[p] * t) * Mathf.Exp(-t * 4f);
                }
            }
            Normalize(buf, 0.7f);
            AudioClip clip = AudioClip.Create("TunerChime", n, 1, rate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        private static void Normalize(float[] buf, float peak)
        {
            float max = 0f;
            for (int i = 0; i < buf.Length; i++) max = Mathf.Max(max, Mathf.Abs(buf[i]));
            if (max < 1e-5f) return;
            float k = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
        }

        private static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (char ch in s) h = h * 31 + ch;
                return h == 0 ? 1 : h;
            }
        }
    }
}

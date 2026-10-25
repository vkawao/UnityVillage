using System;
using System.Collections;
using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// A neon sign that can be switched on: swaps its renderer between an unlit "off" look
    /// and the glowing model material, with a short flicker and a brief buzz as it lights.
    /// </summary>
    public class NeonSign : MonoBehaviour
    {
        [SerializeField] private Renderer m_Renderer;
        [Tooltip("Glowing material (the model's own neon).")]
        [SerializeField] private Material m_OnMaterial;
        [Tooltip("Dull glass-tube look while switched off.")]
        [SerializeField] private Material m_OffMaterial;
        [SerializeField] private bool m_StartOff = true;
        [SerializeField, Range(0f, 1f)] private float m_BuzzVolume = 0.35f;
        [Tooltip("Seconds the buzz plays once lit before fading out (0 = keep buzzing).")]
        [SerializeField] private float m_BuzzSeconds = 1f;

        private AudioSource m_Buzz;
        private Coroutine m_Flicker;

        public bool IsOn { get; private set; }

        public event Action<NeonSign> TurnedOn;

        private void Awake()
        {
            if (m_Renderer == null) m_Renderer = GetComponent<Renderer>();
            m_Buzz = gameObject.AddComponent<AudioSource>();
            m_Buzz.playOnAwake = false;
            m_Buzz.loop = true;
            m_Buzz.spatialBlend = 1f;
            m_Buzz.minDistance = 0.8f;
            m_Buzz.maxDistance = 12f;
            m_Buzz.clip = SynthAudio.NeonBuzz;
            IsOn = !m_StartOff;
            SetLit(IsOn);
        }

        public void TurnOn()
        {
            if (IsOn) return;
            IsOn = true;
            if (m_Flicker != null) StopCoroutine(m_Flicker);
            m_Flicker = StartCoroutine(Flicker());
            TurnedOn?.Invoke(this);
        }

        /// <summary>Stutters on like a real tube: a few blinks with crackles, then steady with the buzz.</summary>
        private IEnumerator Flicker()
        {
            float[] pattern = { 0.06f, 0.1f, 0.05f, 0.18f, 0.08f, 0.06f };
            for (int i = 0; i < pattern.Length; i++)
            {
                bool lit = i % 2 == 0;
                SetLit(lit);
                if (lit) SynthAudio.PlayAt(SynthSound.Click, transform.position, 0.6f);
                yield return new WaitForSeconds(pattern[i]);
            }
            SetLit(true);
            m_Buzz.volume = m_BuzzVolume;
            m_Buzz.Play();
            if (m_BuzzSeconds > 0f)
            {
                // a short buzz as it settles, then quiet (a constant hum next to the shop was too much)
                yield return new WaitForSeconds(m_BuzzSeconds);
                const float fade = 0.4f;
                for (float t = 0f; t < fade; t += Time.deltaTime)
                {
                    m_Buzz.volume = m_BuzzVolume * (1f - t / fade);
                    yield return null;
                }
                m_Buzz.Stop();
            }
            m_Flicker = null;
        }

        private void SetLit(bool lit)
        {
            Material m = lit ? m_OnMaterial : m_OffMaterial;
            if (m_Renderer == null || m == null) return;
            Material[] mats = m_Renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            m_Renderer.sharedMaterials = mats;
        }
    }
}

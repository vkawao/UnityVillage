using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: the rhythm game (maimai-style). One of the 8 yellow buttons around the round screen
    /// lights up; touch it with a hand (or point at it and press grip) and the next one lights.
    /// Every hit plays a note a step higher. Hit 8 to win.
    /// </summary>
    public class RhythmGameStep : ArcadeGameStep
    {
        [Header("Rhythm game")]
        [SerializeField] private XRSimpleInteractable[] m_Buttons;
        [Tooltip("Glow over each button (same order), shown while it's the one to hit.")]
        [SerializeField] private GameObject[] m_Glows;
        [SerializeField] private int m_HitsToWin = 8;
        [Tooltip("A hand closer than this (m) to the lit button counts as touching it.")]
        [SerializeField] private float m_Reach = 0.1f;
        [Tooltip("Pause (s) after a hit before the next button lights.")]
        [SerializeField] private float m_Gap = 0.25f;
        [Tooltip("Optional: small sparkle on each hit.")]
        [SerializeField] private QuestCelebration m_Sparkle;

        private int m_Hits;
        private int m_Lit = -1;
        private int m_Previous = -1;
        private float m_NextLight;
        private Vector3[] m_GlowScale;

        protected override string PlayingHint =>
            "Touch the <b>glowing button</b>! Each one lights up in turn.\n" +
            $"<color={HintColor}><i>Reach out with your hand, or point at it and press grip</i></color>";

        protected override Transform GameGuideTarget =>
            m_Lit >= 0 && m_Buttons != null && m_Lit < m_Buttons.Length && m_Buttons[m_Lit] != null ? m_Buttons[m_Lit].transform : null;

        protected override void Awake()
        {
            base.Awake();
            if (m_Glows == null) return;
            m_GlowScale = new Vector3[m_Glows.Length];
            for (int i = 0; i < m_Glows.Length; i++)
            {
                if (m_Glows[i] == null) continue;
                m_GlowScale[i] = m_Glows[i].transform.localScale;
                m_Glows[i].SetActive(false);
            }
        }

        protected override void OnGameStart()
        {
            m_Hits = 0;
            m_Lit = -1;
            m_Previous = -1;
            m_NextLight = Time.time;
            ShowScore("HITS", 0, m_HitsToWin);
        }

        protected override void OnEnd()
        {
            base.OnEnd();
            SetGlow(m_Lit, false);
            m_Lit = -1;
        }

        protected override void UpdateGame()
        {
            if (m_Buttons == null || m_Buttons.Length == 0) return;
            if (m_Lit < 0)
            {
                if (Time.time >= m_NextLight) LightNext();
                return;
            }

            // Lit button pulses.
            GameObject glow = m_Glows != null && m_Lit < m_Glows.Length ? m_Glows[m_Lit] : null;
            if (glow != null) glow.transform.localScale = m_GlowScale[m_Lit] * (1f + 0.12f * Mathf.Sin(Time.time * 12f));

            if (IsTouched(m_Buttons[m_Lit])) Hit();
        }

        private void LightNext()
        {
            int n = m_Buttons.Length;
            int pick = Random.Range(0, n);
            if (n > 1 && pick == m_Previous) pick = (pick + 1 + Random.Range(0, n - 1)) % n;
            m_Lit = pick;
            SetGlow(m_Lit, true);
        }

        /// <summary>Selected (grip while pointing), or a hand hovering within reach of it.</summary>
        private bool IsTouched(XRSimpleInteractable button)
        {
            if (button == null) return false;
            if (button.isSelected) return true;
            var hovering = button.interactorsHovering;
            for (int i = 0; i < hovering.Count; i++)
            {
                Vector3 hand = hovering[i].transform.position;
                foreach (Collider c in button.colliders)
                {
                    // Far rays hover too; only a hand that's actually at the button counts.
                    if (c != null && Vector3.Distance(c.ClosestPoint(hand), hand) <= m_Reach) return true;
                }
            }
            return false;
        }

        private void Hit()
        {
            Vector3 pos = m_Buttons[m_Lit].transform.position;
            AudioSource.PlayClipAtPoint(SynthAudio.RhythmNote(m_Hits), pos, 0.8f);
            if (m_Sparkle != null) m_Sparkle.Sparkle(pos, 18);
            SetGlow(m_Lit, false);
            m_Previous = m_Lit;
            m_Lit = -1;
            m_Hits++;
            ShowScore("HITS", m_Hits, m_HitsToWin);
            if (m_Hits >= m_HitsToWin)
            {
                Win($"<size=55%>{GameTitle}</size>\n<b><color={OnColor}>CLEAR!</color></b>\n<size=55%>{m_Hits}/{m_HitsToWin} HITS</size>",
                    $"<color={OnColor}>Perfect rhythm! {m_Hits} out of {m_HitsToWin}!</color>");
                return;
            }
            SetProgress($"<color={TextColor}>{PlayingHint}</color>");
            m_NextLight = Time.time + m_Gap;
        }

        private void SetGlow(int index, bool on)
        {
            if (m_Glows == null || index < 0 || index >= m_Glows.Length || m_Glows[index] == null) return;
            m_Glows[index].transform.localScale = m_GlowScale[index];
            m_Glows[index].SetActive(on);
        }
    }
}

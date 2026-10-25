using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRVillage
{
    /// <summary>
    /// Action: test the amps and pick the one that isn't faulty. One amp (random unless
    /// set) hums, the others buzz when their volume knob is turned up. Each amp has a
    /// button under it: wrong = buzzer + red button, right = all amps go quiet and the
    /// step completes (confetti + fanfare via the step's Celebration).
    /// </summary>
    public class AmpTestStep : QuestStep
    {
        [Tooltip("The amps, in the same order as the buttons.")]
        [SerializeField] private AmpUnit[] m_Amps;
        [Tooltip("One selection button per amp.")]
        [SerializeField] private Button[] m_Buttons;
        [Tooltip("Index of the working amp. -1 = pick one at random each play.")]
        [SerializeField] private int m_WorkingIndex = -1;

        [SerializeField] private Color m_WrongColor = new Color(0.85f, 0.15f, 0.15f);
        [SerializeField] private Color m_RightColor = new Color(0.16f, 0.65f, 0.3f);
        [SerializeField, Range(0f, 1f)] private float m_BuzzerVolume = 0.9f;

        private int m_Working;

        public AmpUnit WorkingAmp => m_Amps != null && m_Working >= 0 && m_Working < m_Amps.Length ? m_Amps[m_Working] : null;

        protected override void Awake()
        {
            base.Awake();
            int count = m_Amps != null ? m_Amps.Length : 0;
            m_Working = m_WorkingIndex >= 0 && m_WorkingIndex < count ? m_WorkingIndex : Random.Range(0, Mathf.Max(count, 1));
            for (int i = 0; i < count; i++)
            {
                if (m_Amps[i] != null) m_Amps[i].IsBroken = i != m_Working;
            }
        }

        protected override void OnBegin()
        {
            if (m_Amps == null || m_Amps.Length == 0)
            {
                Debug.LogWarning("[Quest] AmpTestStep has no amps.", this);
                return;
            }

            foreach (AmpUnit amp in m_Amps)
            {
                if (amp != null) amp.NoiseEnabled = true;
            }

            for (int i = 0; i < m_Buttons.Length; i++)
            {
                if (m_Buttons[i] == null) continue;
                int index = i;
                m_Buttons[i].onClick.AddListener(() => OnPicked(index));
            }

            SetProgress("Turn each amp's volume knob, then press the button under the working one");
        }

        protected override void OnEnd()
        {
            foreach (Button b in m_Buttons)
            {
                if (b == null) continue;
                b.onClick.RemoveAllListeners();
                b.interactable = false;
            }
            foreach (AmpUnit amp in m_Amps)
            {
                if (amp == null) continue;
                amp.NoiseEnabled = false;
                amp.StopNoise();
                amp.ResetKnob();
            }
        }

        private void OnPicked(int index)
        {
            if (!IsActive) return;
            Button button = m_Buttons[index];

            if (index == m_Working)
            {
                Paint(button, m_RightColor, "Working!");
                Debug.Log($"[Quest] Picked the working amp: {m_Amps[index].name}");
                Complete();
                return;
            }

            Paint(button, m_WrongColor, "Faulty");
            button.interactable = false;
            AudioSource.PlayClipAtPoint(SynthAudio.Buzzer, button.transform.position, m_BuzzerVolume);
        }

        private static void Paint(Button button, Color color, string label)
        {
            if (button.image != null) button.image.color = color;
            var text = button.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = label;
        }
    }
}

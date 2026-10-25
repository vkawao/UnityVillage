using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// Runs a house's actions in order. For each step it moves the floating
    /// QuestPanel to that step's anchor, shows "N. instruction", and waits for the
    /// step to complete before hiding the panel and moving on to the next one.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        [SerializeField] private QuestPanel m_Panel;

        [Tooltip("Actions in play order. Drag QuestStep objects here.")]
        [SerializeField] private List<QuestStep> m_Steps = new List<QuestStep>();

        [Tooltip("Seconds before the first instruction appears.")]
        [SerializeField] private float m_StartDelay = 1.0f;

        [Tooltip("Seconds between one panel hiding and the next one appearing.")]
        [SerializeField] private float m_DelayBetweenSteps = 0.75f;

        [Tooltip("Prefix each instruction with its number, e.g. \"1. \".")]
        [SerializeField] private bool m_AutoNumber = true;

        [Tooltip("Colour of the small progress line under the instruction.")]
        [SerializeField] private Color m_ProgressColor = new Color(0.95f, 0.75f, 0.3f);

        private int m_CurrentIndex = -1;

        public int CurrentIndex => m_CurrentIndex;
        public QuestStep CurrentStep =>
            m_CurrentIndex >= 0 && m_CurrentIndex < m_Steps.Count ? m_Steps[m_CurrentIndex] : null;

        /// <summary>Raised with the step index when a step starts.</summary>
        public event Action<int> StepStarted;
        /// <summary>Raised when every step is done.</summary>
        public event Action AllStepsCompleted;

        private void Start()
        {
            StartCoroutine(StartStepAfter(0, m_StartDelay));
        }

        private IEnumerator StartStepAfter(int index, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            m_CurrentIndex = index;

            if (index >= m_Steps.Count)
            {
                if (m_Panel != null) m_Panel.Hide();
                Debug.Log("[Quest] All steps completed.");
                AllStepsCompleted?.Invoke();
                yield break;
            }

            QuestStep step = m_Steps[index];
            if (step == null)
            {
                Debug.LogWarning($"[Quest] Step {index + 1} is empty, skipping.", this);
                StartCoroutine(StartStepAfter(index + 1, 0f));
                yield break;
            }

            if (m_Panel != null) m_Panel.Show(step.PanelAnchor, BuildLabel(index, step));

            Debug.Log($"[Quest] Started step {index + 1}: {step.name}");
            StepStarted?.Invoke(index);

            step.Completed += OnStepCompleted;
            step.ProgressChanged += OnStepProgressChanged;
            step.Begin(); // may complete immediately if the condition is already met
        }

        private string BuildLabel(int index, QuestStep step)
        {
            string label = m_AutoNumber ? $"{index + 1}. {step.Instruction}" : step.Instruction;
            if (!string.IsNullOrEmpty(step.Progress))
            {
                string hex = ColorUtility.ToHtmlStringRGB(m_ProgressColor);
                label += $"\n<size=65%><color=#{hex}>{step.Progress}</color></size>";
            }
            return label;
        }

        private void OnStepProgressChanged(QuestStep step)
        {
            int index = m_Steps.IndexOf(step);
            if (index == m_CurrentIndex && m_Panel != null)
                m_Panel.SetText(BuildLabel(index, step));
        }

        private void OnStepCompleted(QuestStep step)
        {
            step.Completed -= OnStepCompleted;
            step.ProgressChanged -= OnStepProgressChanged;
            if (m_Panel != null) m_Panel.Hide();
            StartCoroutine(StartStepAfter(m_CurrentIndex + 1, m_DelayBetweenSteps));
        }
    }
}

using System;
using System.Collections;
using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// Base class for one numbered action in a house's activity.
    /// The QuestManager calls Begin(); the step calls Complete() when the player finishes it.
    /// To add a new action, subclass this and call Complete() when your condition is met.
    /// </summary>
    public abstract class QuestStep : MonoBehaviour
    {
        [Header("Instruction Panel")]
        [Tooltip("Text shown on the floating panel. The number (\"1. \") is added automatically by the QuestManager.")]
        [TextArea(2, 5)]
        [SerializeField] private string m_Instruction = "Do the thing";

        [Tooltip("Where the floating panel appears for this step. Move/rotate this object to reposition the panel.")]
        [SerializeField] private Transform m_PanelAnchor;

        [Header("Completion Feedback (optional)")]
        [Tooltip("Sparkles + fanfare played when this step completes. Leave empty for no feedback.")]
        [SerializeField] private QuestCelebration m_Celebration;

        [Header("Shown Only During This Step (optional)")]
        [Tooltip("Objects (markers, buttons...) that are hidden until this step starts and hidden again when it completes.")]
        [SerializeField] private GameObject[] m_ShowWhileActive;
        [Tooltip("Seconds to keep those objects visible after completing (e.g. to show a green button).")]
        [SerializeField] private float m_HideDelay = 0f;

        [Header("Guide (arrow + task line)")]
        [Tooltip("Short version of the instruction for the guide's task line. Empty = use the full instruction.")]
        [SerializeField] private string m_ShortInstruction = "";
        [Tooltip("What the guide arrow points at. Empty = the panel anchor.")]
        [SerializeField] private Transform m_GuideTarget;

        public string Instruction => m_Instruction;
        public Transform PanelAnchor => m_PanelAnchor;
        public string ShortInstruction => string.IsNullOrEmpty(m_ShortInstruction) ? m_Instruction : m_ShortInstruction;

        /// <summary>Where the guide arrow should point right now.</summary>
        public virtual Transform GuideTarget => m_GuideTarget != null ? m_GuideTarget : m_PanelAnchor;
        public bool IsActive { get; private set; }
        public bool IsComplete { get; private set; }

        /// <summary>Small extra line under the instruction (e.g. "1/3 on the rack"). Empty = none.</summary>
        public string Progress { get; private set; } = string.Empty;

        /// <summary>Raised once, when the step is completed.</summary>
        public event Action<QuestStep> Completed;

        /// <summary>Raised when <see cref="Progress"/> changes.</summary>
        public event Action<QuestStep> ProgressChanged;

        protected virtual void Awake()
        {
            SetShown(false);
        }

        public void Begin()
        {
            if (IsActive || IsComplete) return;
            IsActive = true;
            SetShown(true);
            OnBegin();
        }

        /// <summary>Call from a subclass when the player has finished this action.</summary>
        protected void Complete()
        {
            if (!IsActive) return;
            IsActive = false;
            IsComplete = true;
            OnEnd();
            if (m_HideDelay > 0f && isActiveAndEnabled) StartCoroutine(HideAfter(m_HideDelay));
            else SetShown(false);
            if (m_Celebration != null) m_Celebration.Play();
            Debug.Log($"[Quest] Completed: {name}");
            Completed?.Invoke(this);
        }

        /// <summary>Update the small progress line shown under the instruction.</summary>
        protected void SetProgress(string progress)
        {
            progress ??= string.Empty;
            if (progress == Progress) return;
            Progress = progress;
            ProgressChanged?.Invoke(this);
        }

        private IEnumerator HideAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            SetShown(false);
        }

        private void SetShown(bool shown)
        {
            if (m_ShowWhileActive == null) return;
            foreach (GameObject go in m_ShowWhileActive)
                if (go != null) go.SetActive(shown);
        }

        /// <summary>Start listening for the completion condition.</summary>
        protected virtual void OnBegin() { }

        /// <summary>Stop listening (unsubscribe events, etc.).</summary>
        protected virtual void OnEnd() { }

        protected virtual void OnDestroy()
        {
            if (IsActive) OnEnd();
        }

        [ContextMenu("Debug: Force Complete")]
        private void DebugForceComplete()
        {
            if (IsActive) Complete();
        }
    }
}

using System.Collections;
using TMPro;
using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// A single world-space instruction panel that fades out, jumps to a new
    /// anchor, swaps its text and fades back in. The panel is static: it takes
    /// the anchor's position AND rotation, so rotate the anchor to aim it.
    /// (The panel's readable side faces the anchor's -Z / blue-arrow-backwards direction.)
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class QuestPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_Text;
        [SerializeField] private CanvasGroup m_CanvasGroup;

        [Header("Animation")]
        [SerializeField] private float m_FadeDuration = 0.35f;
        [Tooltip("Scale multiplier at the start of the pop-in.")]
        [SerializeField] private float m_PopFromScale = 0.85f;

        private Vector3 m_BaseScale;
        private Coroutine m_Routine;
        private bool m_Visible;
        private string m_CurrentText = string.Empty;
        private bool m_AwaitingMove; // true while fading out before jumping to a new anchor

        private void Awake()
        {
            if (m_CanvasGroup == null) m_CanvasGroup = GetComponent<CanvasGroup>();
            m_BaseScale = transform.localScale;
            m_CanvasGroup.alpha = 0f;
            m_CanvasGroup.interactable = false;
            m_CanvasGroup.blocksRaycasts = false;
            m_Visible = false;
        }

        /// <summary>Hide (if showing), move to <paramref name="anchor"/>, set text, show.</summary>
        public void Show(Transform anchor, string text)
        {
            Restart(ShowRoutine(anchor, text));
        }

        /// <summary>Change the text in place (no fade), e.g. to update a progress line.</summary>
        public void SetText(string text)
        {
            m_CurrentText = text;
            if (!m_AwaitingMove && m_Text != null) m_Text.text = text;
        }

        public void Hide()
        {
            Restart(FadeTo(0f));
        }

        private void Restart(IEnumerator routine)
        {
            m_AwaitingMove = false;
            if (m_Routine != null) StopCoroutine(m_Routine);
            m_Routine = StartCoroutine(routine);
        }

        private IEnumerator ShowRoutine(Transform anchor, string text)
        {
            m_CurrentText = text;
            m_AwaitingMove = true;
            if (m_Visible || m_CanvasGroup.alpha > 0f) yield return FadeTo(0f);

            if (anchor != null)
            {
                transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            }
            if (m_Text != null) m_Text.text = m_CurrentText; // may have been updated while fading
            m_AwaitingMove = false;

            yield return FadeTo(1f);
        }

        private IEnumerator FadeTo(float target)
        {
            float start = m_CanvasGroup.alpha;
            float t = 0f;
            while (t < m_FadeDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / m_FadeDuration));
                float a = Mathf.Lerp(start, target, k);
                m_CanvasGroup.alpha = a;
                transform.localScale = m_BaseScale * Mathf.Lerp(m_PopFromScale, 1f, a);
                yield return null;
            }
            m_CanvasGroup.alpha = target;
            transform.localScale = m_BaseScale * Mathf.Lerp(m_PopFromScale, 1f, target);
            m_Visible = target > 0f;
        }
    }
}

using System.Collections;
using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// Fades the player's view to black and back (a comfortable "blink" while moving them).
    /// A black world-space panel owned by the house is held just in front of the camera
    /// while it's visible; nothing is attached to the XR rig or the camera.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] private CanvasGroup m_Group;
        [Tooltip("The black panel (a world-space canvas).")]
        [SerializeField] private Transform m_Panel;
        [Tooltip("Metres in front of the eyes (must be beyond the camera's near clip plane).")]
        [SerializeField] private float m_Distance = 0.12f;

        private Transform m_Head;

        public float Alpha => m_Group != null ? m_Group.alpha : 0f;

        private void Awake()
        {
            if (m_Group != null) m_Group.alpha = 0f;
            if (m_Panel != null) m_Panel.gameObject.SetActive(false);
        }

        /// <summary>Fade to <paramref name="target"/> alpha (1 = black) over <paramref name="duration"/> seconds.</summary>
        public IEnumerator FadeTo(float target, float duration)
        {
            if (m_Group == null) yield break;
            if (m_Panel != null) m_Panel.gameObject.SetActive(true);
            Follow();
            float start = m_Group.alpha;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                m_Group.alpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            m_Group.alpha = target;
            if (target <= 0f && m_Panel != null) m_Panel.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (m_Panel != null && m_Panel.gameObject.activeSelf) Follow();
        }

        private void Follow()
        {
            if (m_Head == null)
            {
                Camera cam = Camera.main;
                if (cam == null) return;
                m_Head = cam.transform;
            }
            if (m_Panel == null) return;
            // Readable side (-Z) towards the eye.
            m_Panel.SetPositionAndRotation(m_Head.position + m_Head.forward * m_Distance,
                Quaternion.LookRotation(m_Head.forward, m_Head.up));
        }
    }
}

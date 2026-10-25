using TMPro;
using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// This house's own guidance, active only while the player's head is inside this
    /// house's area (a box around the shop), so it never shows up at other houses:
    ///   - a small task line that lazily follows the view ("5. Bring the Les Paul to the cashier")
    ///   - an arrow on it pointing toward the current step's target, hidden when the target is in view
    ///   - a soft ding played from the target when a new step starts
    /// Nothing is attached to the XR rig; this object only reads the main camera.
    /// </summary>
    public class QuestGuide : MonoBehaviour
    {
        [SerializeField] private QuestManager m_Manager;

        [Header("House Area")]
        [Tooltip("Centre/rotation of this house's area. The guide only works while the head is inside.")]
        [SerializeField] private Transform m_Area;
        [SerializeField] private Vector3 m_AreaSize = new Vector3(12f, 5f, 12f);

        [Header("HUD")]
        [SerializeField] private Transform m_Hud;
        [SerializeField] private CanvasGroup m_HudGroup;
        [SerializeField] private TMP_Text m_Text;
        [SerializeField] private RectTransform m_Arrow;
        [SerializeField] private CanvasGroup m_ArrowGroup;

        [Header("Placement")]
        [SerializeField] private float m_Distance = 0.8f;
        [Tooltip("How far below eye level the task line sits.")]
        [SerializeField] private float m_Drop = 0.32f;
        [Tooltip("Higher = follows the view faster.")]
        [SerializeField] private float m_FollowSpeed = 3f;
        [Tooltip("Arrow hides when the target is within this many degrees of where you're looking.")]
        [SerializeField] private float m_InViewAngle = 28f;
        [SerializeField] private float m_FadeSpeed = 4f;

        [Header("Ding")]
        [SerializeField] private bool m_PlayDing = true;
        [SerializeField, Range(0f, 1f)] private float m_DingVolume = 0.7f;
        [Tooltip("Delay so the ding lands as the panel appears.")]
        [SerializeField] private float m_DingDelay = 0.4f;

        private Transform m_Head;
        private bool m_Placed;
        private Vector2 m_ArrowBasePos;

        private void Awake()
        {
            if (m_HudGroup != null) m_HudGroup.alpha = 0f;
            if (m_ArrowGroup != null) m_ArrowGroup.alpha = 0f;
            if (m_Arrow != null) m_ArrowBasePos = m_Arrow.anchoredPosition;
        }

        private void OnEnable()
        {
            if (m_Manager != null) m_Manager.StepStarted += OnStepStarted;
        }

        private void OnDisable()
        {
            if (m_Manager != null) m_Manager.StepStarted -= OnStepStarted;
            CancelInvoke();
        }

        /// <summary>True while the player's head is inside this house's area.</summary>
        public bool PlayerInside()
        {
            if (!FindHead() || m_Area == null) return false;
            Vector3 p = m_Area.InverseTransformPoint(m_Head.position);
            Vector3 half = m_AreaSize * 0.5f;
            return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
        }

        private void OnStepStarted(int index)
        {
            if (m_PlayDing) Invoke(nameof(PlayDing), m_DingDelay);
        }

        private void PlayDing()
        {
            QuestStep step = m_Manager != null ? m_Manager.CurrentStep : null;
            if (step == null || !step.IsActive || !PlayerInside()) return;
            Transform target = step.GuideTarget;
            Vector3 at = target != null ? target.position : m_Head.position;
            AudioSource.PlayClipAtPoint(SynthAudio.Ding, at, m_DingVolume);
        }

        private bool FindHead()
        {
            if (m_Head != null) return true;
            Camera cam = Camera.main;
            if (cam == null) return false;
            m_Head = cam.transform;
            return true;
        }

        private void LateUpdate()
        {
            if (m_Hud == null || m_Manager == null || !FindHead()) return;

            QuestStep step = m_Manager.CurrentStep;
            bool show = step != null && step.IsActive && PlayerInside();

            float hudAlpha = Fade(m_HudGroup, show ? 1f : 0f);
            if (hudAlpha <= 0f && !show)
            {
                m_Placed = false; // snap into place next time instead of sliding in from far away
                return;
            }

            Place();
            if (step == null) return;

            if (m_Text != null)
            {
                string line = $"{m_Manager.CurrentIndex + 1}. {step.ShortInstruction}";
                if (m_Text.text != line) m_Text.text = line;
            }
            UpdateArrow(step.GuideTarget);
        }

        private void Place()
        {
            Vector3 forward = m_Head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = m_Head.up; // looking straight up/down
            forward.y = 0f;
            forward.Normalize();

            Vector3 desired = m_Head.position + forward * m_Distance + Vector3.down * m_Drop;
            if (!m_Placed)
            {
                m_Hud.position = desired;
                m_Placed = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-m_FollowSpeed * Time.deltaTime);
                m_Hud.position = Vector3.Lerp(m_Hud.position, desired, k);
            }

            // Readable side faces the player (canvas +Z points away from the eye).
            Vector3 look = m_Hud.position - m_Head.position;
            if (look.sqrMagnitude > 0.0001f) m_Hud.rotation = Quaternion.LookRotation(look, Vector3.up);
        }

        private void UpdateArrow(Transform target)
        {
            if (m_Arrow == null) return;
            if (target == null)
            {
                Fade(m_ArrowGroup, 0f);
                return;
            }

            Vector3 toTarget = target.position - m_Head.position;
            bool inView = Vector3.Angle(m_Head.forward, toTarget) < m_InViewAngle;
            Fade(m_ArrowGroup, inView ? 0f : 1f);

            // Direction in the HUD's plane. Behind you: just point left or right (turn that way).
            Vector3 d = m_Hud.InverseTransformDirection(toTarget);
            Vector2 dir = d.z < 0f ? new Vector2(d.x >= 0f ? 1f : -1f, 0f) : new Vector2(d.x, d.y);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir.Normalize();

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f; // sprite points up
            m_Arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            m_Arrow.anchoredPosition = m_ArrowBasePos + dir * (Mathf.Sin(Time.time * 6f) * 10f);
        }

        private float Fade(CanvasGroup group, float target)
        {
            if (group == null) return target;
            group.alpha = Mathf.MoveTowards(group.alpha, target, m_FadeSpeed * Time.deltaTime);
            return group.alpha;
        }

        private void OnDrawGizmosSelected()
        {
            if (m_Area == null) return;
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(m_Area.position, m_Area.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, m_AreaSize);
        }
    }
}

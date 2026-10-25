using System.Collections;
using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// A washer or dryer door, swung by script about the vertical axis through its own origin
    /// (modelled on the hinge). Not grabbable: the quest opens and closes it.
    /// </summary>
    public class MachineDoor : MonoBehaviour
    {
        [Tooltip("How far it opens, degrees about world up (the sign picks the direction).")]
        [SerializeField] private float m_OpenAngle = -100f;
        [SerializeField] private float m_Duration = 0.7f;
        [SerializeField, Range(0f, 1f)] private float m_Volume = 0.8f;

        private Quaternion m_ClosedLocal;
        private Coroutine m_Swing;

        public bool IsOpen { get; private set; }

        private void Awake() => m_ClosedLocal = transform.localRotation;

        public void SetOpen(bool open, bool sound = true)
        {
            if (open == IsOpen) return;
            IsOpen = open;
            Quaternion parent = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            Quaternion closedWorld = parent * m_ClosedLocal;
            Quaternion target = open
                ? Quaternion.Inverse(parent) * Quaternion.AngleAxis(m_OpenAngle, Vector3.up) * closedWorld
                : m_ClosedLocal;
            if (m_Swing != null) StopCoroutine(m_Swing);
            m_Swing = StartCoroutine(Swing(target));
            if (sound) SynthAudio.PlayAt(SynthSound.MachineDoor, transform.position, m_Volume);
        }

        private IEnumerator Swing(Quaternion target)
        {
            Quaternion from = transform.localRotation;
            for (float t = 0f; t < m_Duration; t += Time.deltaTime)
            {
                transform.localRotation = Quaternion.Slerp(from, target, Mathf.SmoothStep(0f, 1f, t / m_Duration));
                yield return null;
            }
            transform.localRotation = target;
            m_Swing = null;
        }
    }
}

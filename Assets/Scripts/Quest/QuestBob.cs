using UnityEngine;

namespace VRVillage
{
    /// <summary>Gently bobs an object up and down (e.g. a "Here!" marker).</summary>
    public class QuestBob : MonoBehaviour
    {
        [SerializeField] private float m_Amplitude = 0.04f;
        [SerializeField] private float m_Speed = 2.5f;

        private Vector3 m_Base;

        private void OnEnable() => m_Base = transform.localPosition;

        private void OnDisable() => transform.localPosition = m_Base;

        private void Update()
        {
            transform.localPosition = m_Base + Vector3.up * (Mathf.Sin(Time.time * m_Speed) * m_Amplitude);
        }
    }
}

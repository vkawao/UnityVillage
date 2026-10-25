using UnityEngine;

namespace VRVillage
{
    /// <summary>The air hockey puck clacks when it hits a pusher or the rail (louder for harder hits).</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PuckSound : MonoBehaviour
    {
        [SerializeField] private float m_MinSpeed = 0.15f;
        private float m_Next;

        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < m_MinSpeed || Time.time < m_Next) return;
            m_Next = Time.time + 0.06f;
            SynthAudio.PlayAt(SynthSound.PuckClack, transform.position, Mathf.Clamp01(0.25f + speed * 0.3f));
        }
    }
}

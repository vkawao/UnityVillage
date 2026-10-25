using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRVillage
{
    /// <summary>Plays a synthesized sound when something docks in this socket (not for its starting item).</summary>
    [RequireComponent(typeof(XRSocketInteractor))]
    public class SocketSound : MonoBehaviour
    {
        [SerializeField] private SynthSound m_Sound = SynthSound.MetalClack;
        [SerializeField, Range(0f, 1f)] private float m_Volume = 0.9f;

        private XRSocketInteractor m_Socket;
        private float m_ReadyTime = float.MaxValue;

        private void Awake()
        {
            m_Socket = GetComponent<XRSocketInteractor>();
            m_Socket.selectEntered.AddListener(OnDocked);
        }

        private void Start() => m_ReadyTime = Time.time + 0.5f; // the starting item docks silently

        private void OnDestroy()
        {
            if (m_Socket != null) m_Socket.selectEntered.RemoveListener(OnDocked);
        }

        private void OnDocked(SelectEnterEventArgs args)
        {
            if (Time.time < m_ReadyTime) return;
            SynthAudio.PlayAt(m_Sound, transform.position, m_Volume);
        }
    }
}

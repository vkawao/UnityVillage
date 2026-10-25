using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// A stomp box that toggles on/off. Toggle by grabbing its footswitch, or by
    /// pulling the trigger while holding the pedal. An LED shows the state.
    /// </summary>
    public class EffectPedal : MonoBehaviour
    {
        [SerializeField] private XRSimpleInteractable m_Footswitch;
        [SerializeField] private Renderer m_Led;
        [SerializeField] private Color m_LedOn = new Color(0.2f, 1f, 0.3f);
        [SerializeField] private Color m_LedOff = new Color(0.04f, 0.12f, 0.05f);
        [SerializeField, Range(0f, 1f)] private float m_ClickVolume = 0.7f;

        private XRGrabInteractable m_Grab;
        private MaterialPropertyBlock m_Block;

        public bool IsOn { get; private set; }

        public event Action<EffectPedal> Toggled;

        private void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            if (m_Footswitch != null) m_Footswitch.selectEntered.AddListener(OnFootswitch);
            if (m_Grab != null) m_Grab.activated.AddListener(OnActivated);
            SetLed();
        }

        private void OnDestroy()
        {
            if (m_Footswitch != null) m_Footswitch.selectEntered.RemoveListener(OnFootswitch);
            if (m_Grab != null) m_Grab.activated.RemoveListener(OnActivated);
        }

        private void OnFootswitch(SelectEnterEventArgs args) => Toggle();

        private void OnActivated(ActivateEventArgs args) => Toggle();

        [ContextMenu("Toggle")]
        public void Toggle()
        {
            IsOn = !IsOn;
            SetLed();
            AudioSource.PlayClipAtPoint(SynthAudio.Click, transform.position, m_ClickVolume);
            Toggled?.Invoke(this);
        }

        private void SetLed()
        {
            if (m_Led == null) return;
            m_Block ??= new MaterialPropertyBlock();
            m_Led.GetPropertyBlock(m_Block);
            m_Block.SetColor("_BaseColor", IsOn ? m_LedOn : m_LedOff);
            m_Led.SetPropertyBlock(m_Block);
        }
    }
}

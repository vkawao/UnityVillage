using System.Collections;
using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// The arcade's neon tubes (some material slots of the building) start dark and flicker on,
    /// with a buzz, the first time the door opens. Done with property blocks, so the shared
    /// materials are untouched.
    /// </summary>
    public class NeonFlicker : MonoBehaviour
    {
        [SerializeField] private Renderer m_Renderer;
        [Tooltip("Material slots of the renderer that are neon.")]
        [SerializeField] private int[] m_Slots;
        [SerializeField] private Color m_OffColor = new Color(0.12f, 0.12f, 0.14f);
        [Tooltip("Turns on when this door first opens.")]
        [SerializeField] private DoorMover m_Door;

        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock m_Block;
        private bool m_On;

        private void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            SetLit(false);
            if (m_Door != null) m_Door.StateChanged += OnDoor;
        }

        private void OnDestroy()
        {
            if (m_Door != null) m_Door.StateChanged -= OnDoor;
        }

        private void OnDoor(bool open)
        {
            if (open && !m_On) TurnOn();
        }

        public void TurnOn()
        {
            if (m_On) return;
            m_On = true;
            StartCoroutine(Flicker());
        }

        private IEnumerator Flicker()
        {
            if (m_Renderer != null) SynthAudio.PlayAt(SynthSound.NeonBuzz, m_Renderer.bounds.center, 0.7f);
            float[] steps = { 0.08f, 0.06f, 0.12f, 0.05f, 0.2f, 0.07f };
            for (int i = 0; i < steps.Length; i++)
            {
                SetLit(i % 2 == 0);
                yield return new WaitForSeconds(steps[i]);
            }
            SetLit(true);
        }

        private void SetLit(bool lit)
        {
            if (m_Renderer == null || m_Slots == null) return;
            foreach (int slot in m_Slots)
            {
                if (slot < 0 || slot >= m_Renderer.sharedMaterials.Length) continue;
                m_Block.Clear(); // an empty block = the material's own colour
                if (!lit) m_Block.SetColor(s_BaseColor, m_OffColor);
                m_Renderer.SetPropertyBlock(m_Block, slot);
            }
        }
    }
}

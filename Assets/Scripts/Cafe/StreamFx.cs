using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// A thin pouring stream (coffee from the machine, milk from the pitcher): a stretched cylinder
    /// between two points, with a little wobble so it reads as liquid.
    /// </summary>
    public class StreamFx : MonoBehaviour
    {
        [Tooltip("The cylinder that is stretched between the two points.")]
        [SerializeField] private Transform m_Cylinder;
        [SerializeField] private float m_Width = 0.008f;

        private Renderer m_Renderer;
        private MaterialPropertyBlock m_Block;

        private void Awake()
        {
            if (m_Cylinder != null) m_Renderer = m_Cylinder.GetComponent<Renderer>();
            m_Block = new MaterialPropertyBlock();
            Hide();
        }

        public void Show(Vector3 from, Vector3 to, Color color)
        {
            if (m_Cylinder == null) return;
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 0.005f)
            {
                Hide();
                return;
            }
            float width = m_Width * (1f + 0.15f * Mathf.Sin(Time.time * 37f));
            m_Cylinder.gameObject.SetActive(true);
            m_Cylinder.SetPositionAndRotation((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, d / length));
            m_Cylinder.localScale = new Vector3(width, length * 0.5f, width); // a cylinder mesh is 2 units tall
            if (m_Renderer != null)
            {
                m_Renderer.GetPropertyBlock(m_Block);
                m_Block.SetColor("_BaseColor", color);
                m_Renderer.SetPropertyBlock(m_Block);
            }
        }

        public void Hide()
        {
            if (m_Cylinder != null) m_Cylinder.gameObject.SetActive(false);
        }
    }
}

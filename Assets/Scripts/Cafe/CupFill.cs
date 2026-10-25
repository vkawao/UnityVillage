using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// The drink inside a cup: a flat disc that rises from the bottom (empty) to near the rim (full)
    /// and can change colour (coffee, then coffee with milk).
    /// </summary>
    public class CupFill : MonoBehaviour
    {
        [Tooltip("The drink's surface (a flat disc inside the cup).")]
        [SerializeField] private Transform m_Surface;
        [Tooltip("Surface local position when nearly empty / full (relative to its parent).")]
        [SerializeField] private Vector3 m_EmptyLocal;
        [SerializeField] private Vector3 m_FullLocal;

        private Renderer m_Renderer;
        private MaterialPropertyBlock m_Block;

        public float Fill { get; private set; }
        public Color Color { get; private set; } = new Color(0.2f, 0.11f, 0.05f);

        /// <summary>The drink's surface: where pours land.</summary>
        public Vector3 SurfacePoint => m_Surface != null && m_Surface.parent != null
            ? m_Surface.parent.TransformPoint(Vector3.Lerp(m_EmptyLocal, m_FullLocal, Fill))
            : transform.position;

        private void Awake()
        {
            if (m_Surface != null) m_Renderer = m_Surface.GetComponent<Renderer>();
            m_Block = new MaterialPropertyBlock();
            SetFill(0f);
        }

        public void SetFill(float fill)
        {
            Fill = Mathf.Clamp01(fill);
            if (m_Surface == null) return;
            m_Surface.gameObject.SetActive(Fill > 0.001f);
            m_Surface.localPosition = Vector3.Lerp(m_EmptyLocal, m_FullLocal, Fill);
        }

        public void SetColor(Color color)
        {
            Color = color;
            if (m_Renderer == null) return;
            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetColor("_BaseColor", color);
            m_Renderer.SetPropertyBlock(m_Block);
        }
    }
}

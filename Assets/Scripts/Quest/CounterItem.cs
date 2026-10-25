using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>Something for sale. Shows up on the cashier screen when it's on the counter.</summary>
    public class CounterItem : MonoBehaviour
    {
        [SerializeField] private string m_DisplayName = "Item";
        [SerializeField] private int m_Price = 100;

        private XRGrabInteractable m_Grab;
        private Rigidbody m_Body;

        public string DisplayName => m_DisplayName;
        public int Price => m_Price;
        public XRGrabInteractable Grab => m_Grab;
        public Rigidbody Body => m_Body;

        private void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Body = GetComponent<Rigidbody>();
        }
    }
}

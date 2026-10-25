using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// A piece of laundry (a shirt) that can be packed into a <see cref="LaundryContainer"/>.
    /// While packed it rides along as a smaller, kinematic child of the container; grabbing it
    /// takes it out again at full size. Its grab must not retain its parent (set by the setup),
    /// so dropping it never re-parents it into the container it was taken from.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class LaundryItem : MonoBehaviour
    {
        private XRGrabInteractable m_Grab;
        private Rigidbody m_Body;
        private Collider[] m_Colliders;
        private Transform m_HomeParent;
        private RigidbodyInterpolation m_Interpolation;
        private Renderer m_Renderer;
        private MaterialPropertyBlock m_Block;

        public XRGrabInteractable Grab => m_Grab;
        public Rigidbody Body => m_Body;
        public Collider[] Colliders => m_Colliders;
        public LaundryContainer Container { get; private set; }
        public bool IsWet { get; private set; }

        /// <summary>Local axis along the item's longest side.</summary>
        public Vector3 LongAxis { get; private set; } = Vector3.right;
        /// <summary>Local axis through the item's thinnest side, and its thickness (metres).</summary>
        public Vector3 ThinAxis { get; private set; } = Vector3.up;
        public float Thickness { get; private set; } = 0.02f;

        private void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Body = GetComponent<Rigidbody>();
            m_Colliders = GetComponentsInChildren<Collider>(true);
            m_HomeParent = transform.parent;
            MeasureShape();
            m_Grab.selectEntered.AddListener(OnGrabbed);
            m_Grab.selectExited.AddListener(OnReleased);
        }

        private void OnDestroy()
        {
            if (m_Grab == null) return;
            m_Grab.selectEntered.RemoveListener(OnGrabbed);
            m_Grab.selectExited.RemoveListener(OnReleased);
        }

        /// <summary>Wet clothes look darker (per renderer, the shared material is untouched).</summary>
        public void SetWet(bool wet)
        {
            IsWet = wet;
            if (m_Renderer == null) m_Renderer = GetComponent<Renderer>();
            if (m_Renderer == null) return;
            m_Block ??= new MaterialPropertyBlock();
            Material[] mats = m_Renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || !mats[i].HasProperty("_BaseColor")) continue;
                m_Renderer.GetPropertyBlock(m_Block, i);
                Color c = mats[i].GetColor("_BaseColor");
                m_Block.SetColor("_BaseColor", wet ? new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.6f, c.a) : c);
                m_Renderer.SetPropertyBlock(m_Block, i);
            }
        }

        private void MeasureShape()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            Vector3 size = Vector3.Scale(mf.sharedMesh.bounds.size, Abs(transform.lossyScale));
            int thin = 0, longest = 0;
            for (int i = 1; i < 3; i++)
            {
                if (size[i] < size[thin]) thin = i;
                if (size[i] > size[longest]) longest = i;
            }
            if (thin == longest) longest = (thin + 1) % 3;
            ThinAxis = Axis(thin);
            LongAxis = Axis(longest);
            Thickness = Mathf.Max(size[thin], 0.005f);
        }

        /// <summary>Called by a container: become its kinematic child at this world pose.</summary>
        internal void Pack(LaundryContainer container, Transform parent, Vector3 position, Quaternion rotation)
        {
            Container = container;
            if (m_Body != null)
            {
                m_Interpolation = m_Body.interpolation;
                m_Body.isKinematic = true;
                m_Body.interpolation = RigidbodyInterpolation.None; // follows its parent's transform
            }
            transform.SetParent(parent, true);
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Called by a container: no longer its child.</summary>
        internal void Unpack()
        {
            Container = null;
            transform.SetParent(m_HomeParent, true);
            if (m_Body != null) m_Body.interpolation = m_Interpolation;
        }

        /// <summary>Move to a new pose without a container (e.g. taken out of a machine), physics on.</summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            if (Container != null) Container.Remove(this);
            transform.SetPositionAndRotation(position, rotation);
            if (m_Body == null) return;
            m_Body.position = position;
            m_Body.rotation = rotation;
            m_Body.isKinematic = false;
            m_Body.linearVelocity = Vector3.zero;
            m_Body.angularVelocity = Vector3.zero;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (Container != null) Container.Remove(this);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            // The grab restores the kinematic state it saw when picked up (packed = kinematic).
            if (Container == null && m_Body != null && gameObject.activeInHierarchy) m_Body.isKinematic = false;
        }

        private static Vector3 Axis(int i) => i == 0 ? Vector3.right : (i == 1 ? Vector3.up : Vector3.forward);

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// A plate/saucer that holds one item. Set an item down on it and it settles in the middle,
    /// upright, and then rides along when you carry the plate. Grab the item to take it off.
    /// </summary>
    public class PlateHolder : MonoBehaviour
    {
        [Tooltip("Top centre of the plate (a child that stays level with the plate).")]
        [SerializeField] private Transform m_Top;
        [Tooltip("Items that can go on this plate.")]
        [SerializeField] private PlateItem[] m_Accepts;
        [Tooltip("An item's bottom within this distance (m, sideways) of the centre counts as on the plate.")]
        [SerializeField] private float m_Radius = 0.08f;
        [Tooltip("...and within this height above the plate top (m).")]
        [SerializeField] private float m_Height = 0.06f;

        private XRGrabInteractable m_Grab;
        private Collider[] m_OwnColliders;
        private Vector3 m_ItemLocalPos;
        private Quaternion m_ItemLocalRot;

        public PlateItem Item { get; private set; }
        public XRGrabInteractable Grab => m_Grab;
        public Transform Top => m_Top != null ? m_Top : transform;

        private void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_OwnColliders = GetComponentsInChildren<Collider>();
        }

        private void Update()
        {
            if (Item != null)
            {
                if (GrabUtil.IsHeldByHand(Item.Grab) || !Item.gameObject.activeInHierarchy) Release();
                return;
            }
            if (m_Accepts == null) return;
            foreach (PlateItem item in m_Accepts)
            {
                if (item == null || item.Plate != null || item.Grab.isSelected || !item.gameObject.activeInHierarchy) continue;
                Vector3 d = item.Base.position - Top.position;
                float up = Vector3.Dot(d, Top.up);
                Vector3 side = d - Top.up * up;
                if (side.magnitude <= m_Radius && up > -0.03f && up < m_Height)
                {
                    Hold(item);
                    return;
                }
            }
        }

        private void LateUpdate()
        {
            if (Item == null) return;
            Item.transform.SetPositionAndRotation(Top.TransformPoint(m_ItemLocalPos), Top.rotation * m_ItemLocalRot);
            if (Item.Body != null)
            {
                Item.Body.position = Item.transform.position;
                Item.Body.rotation = Item.transform.rotation;
            }
        }

        private void Hold(PlateItem item)
        {
            Item = item;
            item.Plate = this;
            if (item.Body != null)
            {
                item.Body.isKinematic = true;
                item.Body.linearVelocity = Vector3.zero;
                item.Body.angularVelocity = Vector3.zero;
            }
            SetIgnoreCollisions(item, true); // a kinematic item resting on the plate would shove it around

            // Upright, keeping the way it's facing, with its bottom on the plate's centre.
            Quaternion turn = item.transform.rotation * Quaternion.Inverse(item.Upright);
            Vector3 f = turn * Vector3.forward;
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            Quaternion rot = Quaternion.AngleAxis(yaw, Top.up) * item.Upright;
            item.transform.rotation = rot;
            Vector3 baseOffset = item.Base.position - item.transform.position;
            Vector3 pos = Top.position - baseOffset;
            m_ItemLocalPos = Top.InverseTransformPoint(pos);
            m_ItemLocalRot = Quaternion.Inverse(Top.rotation) * rot;
            LateUpdate();
            SynthAudio.PlayAt(SynthSound.PlateClink, Top.position, 0.8f);
        }

        private void Release()
        {
            PlateItem item = Item;
            Item = null;
            item.Plate = null;
            SetIgnoreCollisions(item, false);
        }

        private void SetIgnoreCollisions(PlateItem item, bool ignore)
        {
            if (m_OwnColliders == null) return;
            foreach (Collider a in item.GetComponentsInChildren<Collider>())
            {
                foreach (Collider b in m_OwnColliders)
                {
                    if (a != null && b != null) Physics.IgnoreCollision(a, b, ignore);
                }
            }
        }
    }
}

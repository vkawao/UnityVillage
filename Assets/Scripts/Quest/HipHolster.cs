using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRVillage
{
    /// <summary>
    /// A hip pocket that follows the player: it sits below and to the side of the head and
    /// turns with the body (smoothed; ignores looking down so glancing at it doesn't swing it).
    /// The ring visual, and the item stored in it, only show inside this house's area: outside it the
    /// stored item is hidden and can't be grabbed (every house's pocket sits at the same hip spot, so
    /// a card left in another house's pocket showed up as a second, useless card). It reappears when
    /// you come back. Nothing is attached to the XR rig.
    /// </summary>
    public class HipHolster : MonoBehaviour
    {
        [SerializeField] private HolsterSocket m_Socket;
        [Tooltip("Ring + label shown inside the house's area.")]
        [SerializeField] private GameObject m_Visual;
        [Tooltip("Optional: only show the visual while the player is inside this house.")]
        [SerializeField] private QuestGuide m_Guide;
        [Tooltip("Optional: glowing ball + label showing where to let go, shown while you hold an item it takes " +
                 "(or while highlighted). Turned to face you.")]
        [SerializeField] private GameObject m_DropZone;

        [Header("Placement (relative to the head)")]
        [SerializeField] private float m_Drop = 0.62f;
        [Tooltip("Positive = right hip.")]
        [SerializeField] private float m_Side = 0.2f;
        [SerializeField] private float m_Forward = 0.08f;
        [Tooltip("How fast the pocket turns to follow the body (higher = snappier).")]
        [SerializeField] private float m_TurnSpeed = 3f;
        [Tooltip("Looking further down than this (degrees) doesn't turn the pocket.")]
        [SerializeField] private float m_LookDownFreeze = 40f;

        [Header("Highlight")]
        [SerializeField] private float m_PulseSpeed = 4f;

        private Transform m_Head;
        private Vector3 m_BodyForward = Vector3.forward;
        private bool m_HasForward;
        private Vector3 m_VisualBaseScale = Vector3.one;
        private Vector3 m_DropZoneBaseScale = Vector3.one;
        private Component m_HiddenItem;
        private readonly System.Collections.Generic.List<Renderer> m_HiddenRenderers = new System.Collections.Generic.List<Renderer>();
        private readonly System.Collections.Generic.List<Collider> m_HiddenColliders = new System.Collections.Generic.List<Collider>();

        /// <summary>Pulse the ring to draw attention (e.g. "look at your hip").</summary>
        public bool Highlight { get; set; }

        private bool m_Revealed;

        /// <summary>
        /// Hidden and switched off until a step reveals it (e.g. the card step), so it neither
        /// shows nor grabs items before the player has been told about it.
        /// </summary>
        public bool Revealed
        {
            get => m_Revealed;
            set
            {
                m_Revealed = value;
                if (m_Socket != null) m_Socket.socketActive = value;
            }
        }

        public HolsterSocket Socket => m_Socket;

        private void Awake()
        {
            if (m_Visual != null)
            {
                m_VisualBaseScale = m_Visual.transform.localScale;
                m_Visual.SetActive(false);
            }
            if (m_DropZone != null)
            {
                m_DropZoneBaseScale = m_DropZone.transform.localScale;
                m_DropZone.SetActive(false);
            }
            Revealed = false;
        }

        /// <summary>Put an item into the pocket (activating it if needed).</summary>
        public void Store(XRGrabInteractable item)
        {
            if (item == null || m_Socket == null) return;
            Revealed = true;
            if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
            Follow(true);
            Transform attach = m_Socket.attachTransform != null ? m_Socket.attachTransform : m_Socket.transform;
            item.transform.SetPositionAndRotation(attach.position, attach.rotation);
            StartCoroutine(SelectNextFrame(item)); // a just-enabled interactable registers with the manager first
        }

        private System.Collections.IEnumerator SelectNextFrame(XRGrabInteractable item)
        {
            yield return null;
            if (item == null || m_Socket.interactionManager == null || GrabUtil.IsHeldByHand(item)) yield break;
            Transform attach = m_Socket.attachTransform != null ? m_Socket.attachTransform : m_Socket.transform;
            item.transform.SetPositionAndRotation(attach.position, attach.rotation);
            m_Socket.interactionManager.SelectEnter((IXRSelectInteractor)m_Socket, (IXRSelectInteractable)item);
        }

        public bool IsHolding(XRGrabInteractable item)
        {
            if (m_Socket == null || item == null || !m_Socket.hasSelection) return false;
            var held = m_Socket.interactablesSelected;
            for (int i = 0; i < held.Count; i++)
            {
                if (ReferenceEquals(held[i], item)) return true;
            }
            return false;
        }

        private bool FindHead()
        {
            if (m_Head != null) return true;
            Camera cam = Camera.main;
            if (cam == null) return false;
            m_Head = cam.transform;
            return true;
        }

        private void LateUpdate()
        {
            Follow(false);

            bool inside = m_Guide == null || m_Guide.PlayerInside();
            ShowStored(inside);
            bool show = m_Revealed && inside;
            if (m_Visual != null)
            {
                if (m_Visual.activeSelf != show) m_Visual.SetActive(show);
                float pulse = Highlight ? 1f + 0.18f * Mathf.Sin(Time.time * m_PulseSpeed * Mathf.PI) : 1f;
                m_Visual.transform.localScale = m_VisualBaseScale * pulse;
            }
            UpdateDropZone(show);
        }

        /// <summary>Hides the stored item (renderers + colliders it had on) outside the house; restores exactly those.</summary>
        private void ShowStored(bool show)
        {
            IXRSelectInteractable stored = m_Socket != null && m_Socket.hasSelection ? m_Socket.interactablesSelected[0] : null;
            Component item = stored as Component;
            if (m_HiddenItem != null && (show || item != m_HiddenItem))
            {
                foreach (Renderer r in m_HiddenRenderers) if (r != null) r.enabled = true;
                foreach (Collider c in m_HiddenColliders) if (c != null) c.enabled = true;
                m_HiddenRenderers.Clear();
                m_HiddenColliders.Clear();
                m_HiddenItem = null;
            }
            if (show || item == null || m_HiddenItem != null) return;
            m_HiddenItem = item;
            foreach (Renderer r in item.GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; m_HiddenRenderers.Add(r); }
            foreach (Collider c in item.GetComponentsInChildren<Collider>())
                if (c.enabled) { c.enabled = false; m_HiddenColliders.Add(c); }
        }

        private void UpdateDropZone(bool inHouse)
        {
            if (m_DropZone == null) return;
            bool show = inHouse && (Highlight || HoldingAcceptedItem());
            if (m_DropZone.activeSelf != show) m_DropZone.SetActive(show);
            if (!show || m_Head == null) return;
            float pulse = 1f + 0.1f * Mathf.Sin(Time.time * m_PulseSpeed * Mathf.PI);
            m_DropZone.transform.localScale = m_DropZoneBaseScale * pulse;
            Vector3 look = m_DropZone.transform.position - m_Head.position;
            if (look.sqrMagnitude > 0.0001f) m_DropZone.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
        }

        /// <summary>A hand holds something this pocket takes, and the pocket is free for it.</summary>
        private bool HoldingAcceptedItem()
        {
            if (m_Socket == null || m_Socket.hasSelection) return false;
            var items = m_Socket.AcceptedItems;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && GrabUtil.IsHeldByHand(items[i])) return true;
            }
            return false;
        }

        private void Follow(bool snap)
        {
            if (!FindHead()) return;

            Vector3 f = m_Head.forward;
            float pitchDown = Vector3.Angle(f, Vector3.down);
            Vector3 flat = new Vector3(f.x, 0f, f.z);
            bool lookingDown = pitchDown < 90f - m_LookDownFreeze;
            if (flat.sqrMagnitude > 0.0001f && (!lookingDown || !m_HasForward))
            {
                flat.Normalize();
                if (!m_HasForward || snap)
                {
                    m_BodyForward = flat;
                    m_HasForward = true;
                }
                else
                {
                    float k = 1f - Mathf.Exp(-m_TurnSpeed * Time.deltaTime);
                    m_BodyForward = Vector3.Slerp(m_BodyForward, flat, k).normalized;
                }
            }

            Vector3 right = Vector3.Cross(Vector3.up, m_BodyForward);
            Vector3 pos = m_Head.position + Vector3.down * m_Drop + right * m_Side + m_BodyForward * m_Forward;
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(m_BodyForward, Vector3.up));
        }
    }
}

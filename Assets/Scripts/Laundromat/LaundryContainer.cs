using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Something laundry is packed into (the basket, the cart). A shirt let go of inside its opening
    /// is packed: it lies flat on the pile (same size: resizing it would stick, because a grab keeps
    /// the size it was picked up at) and rides along when the container is carried. Grabbing a packed
    /// shirt takes it out again.
    /// </summary>
    public class LaundryContainer : MonoBehaviour
    {
        [Tooltip("The volume a shirt must be let go of in. Child of the container, so it moves with it.")]
        [SerializeField] private ItemTrigger m_Opening;
        [Tooltip("Centre of the inside floor. Its right/forward span the floor, up points out of the container.")]
        [SerializeField] private Transform m_Floor;
        [Tooltip("Only these items are packed.")]
        [SerializeField] private LaundryItem[] m_Accepts;
        [SerializeField] private SynthSound m_Sound = SynthSound.ClothFlop;
        [SerializeField, Range(0f, 1f)] private float m_Volume = 0.8f;
        [Tooltip("Colliders of the container itself, which packed shirts mustn't push against.")]
        [SerializeField] private Collider[] m_OwnColliders;
        [Tooltip("Packs shirts from the start (off for machine drums until it's their turn).")]
        [SerializeField] private bool m_Accepting = true;
        [Tooltip("Also take a shirt that's still held, as soon as it's inside the opening (the hand lets go of it).")]
        [SerializeField] private bool m_TakeWhileHeld;

        private readonly List<LaundryItem> m_Items = new List<LaundryItem>();
        private readonly System.Random m_Rng = new System.Random(7);

        public IReadOnlyList<LaundryItem> Items => m_Items;
        public int Count => m_Items.Count;
        public ItemTrigger Opening => m_Opening;
        /// <summary>The inside floor point; its up is the container's up (tilts when the container is tipped).</summary>
        public Transform Floor => m_Floor != null ? m_Floor : transform;

        /// <summary>False = shirts let go of in the opening aren't packed (e.g. before it's this container's turn).</summary>
        public bool Accepting
        {
            get => m_Accepting;
            set => m_Accepting = value;
        }

        public event Action<LaundryItem> Added;
        public event Action<LaundryItem> Removed;

        public bool Holds(LaundryItem item) => item != null && item.Container == this;

        private void Update()
        {
            if (!Accepting || m_Opening == null || m_Accepts == null) return;
            foreach (LaundryItem item in m_Accepts)
            {
                if (item == null || item.Container != null || !item.gameObject.activeInHierarchy) continue;
                bool held = item.Grab != null && item.Grab.isSelected;
                if (held && !m_TakeWhileHeld) continue; // only once it's let go of
                if (!m_Opening.Contains(item.Body)) continue;
                if (held && item.Grab.interactionManager != null)
                    item.Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)item.Grab);
                Add(item);
            }
        }

        /// <summary>Pack an item onto the pile (also usable from a step, e.g. to load clothes directly).</summary>
        public void Add(LaundryItem item, bool playSound = true)
        {
            if (item == null || item.Container == this) return;
            if (item.Container != null) item.Container.Remove(item);
            Transform floor = m_Floor != null ? m_Floor : transform;

            // Lying flat, its long side along the floor's long side.
            float yaw = (float)(m_Rng.NextDouble() * 2.0 - 1.0) * 12f;
            Vector3 along = Quaternion.AngleAxis(yaw, floor.up) * floor.right;
            Quaternion local = Quaternion.LookRotation(item.LongAxis, item.ThinAxis);
            Quaternion rot = Quaternion.LookRotation(along, floor.up) * Quaternion.Inverse(local);
            float height = PileHeight() + item.Thickness * 0.5f;

            item.Pack(this, floor, floor.position + floor.up * height, rot);
            m_Items.Add(item);
            IgnoreOwnColliders(item, true);
            if (playSound) SynthAudio.PlayAt(m_Sound, floor.position, m_Volume);
            Added?.Invoke(item);
        }

        /// <summary>Take an item out (no longer parented here). Others settle down.</summary>
        public void Remove(LaundryItem item)
        {
            if (item == null || !m_Items.Remove(item)) return;
            IgnoreOwnColliders(item, false);
            item.Unpack();
            Restack();
            Removed?.Invoke(item);
        }

        private float PileHeight()
        {
            float h = 0.003f;
            foreach (LaundryItem i in m_Items)
                if (i != null) h += i.Thickness;
            return h;
        }

        private void Restack()
        {
            Transform floor = m_Floor != null ? m_Floor : transform;
            float h = 0.003f;
            foreach (LaundryItem i in m_Items)
            {
                if (i == null) continue;
                float t = i.Thickness;
                Vector3 p = floor.InverseTransformPoint(i.transform.position);
                Vector3 target = floor.position + floor.up * (h + t * 0.5f);
                i.transform.position = target + (floor.TransformPoint(new Vector3(p.x, 0f, p.z)) - floor.position);
                h += t;
            }
        }

        private void IgnoreOwnColliders(LaundryItem item, bool ignore)
        {
            if (m_OwnColliders == null || item.Colliders == null) return;
            foreach (Collider a in item.Colliders)
            {
                if (a == null) continue;
                foreach (Collider b in m_OwnColliders)
                    if (b != null) Physics.IgnoreCollision(a, b, ignore);
            }
        }
    }
}

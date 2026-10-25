using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace RamenVR
{
    /// A place where one specific item can be put (e.g. the egg into the bowl).
    /// Finishing the placement completes a step on the shop's game manager.
    public class RamenSocket : XRSocketInteractor
    {
        [Header("Ramen")]
        [Tooltip("Name of the object that fits here, e.g. egg")]
        public string acceptedName;
        [Tooltip("Step completed when the item is placed (-1 = none)")]
        public int stepIndex = -1;
        [Tooltip("Steps that must be finished before this socket accepts the item")]
        public int[] requiredSteps;
        [Tooltip("Once placed, the item is glued here and can't be pulled out again")]
        public bool lockOnPlace = true;

        [Header("When the item is placed (optional)")]
        public GameObject[] showOnPlace;
        public GameObject[] hideOnPlace;
        [Tooltip("Hidden item that appears at 'revealAt' (e.g. the fish appears in the net)")]
        public Transform revealItem;
        public Transform revealAt;
        [Tooltip("Two spots that finish one step together (e.g. basket and egg tray on the cart)")]
        public RamenSocket partner;
        [Tooltip("Shown when this spot is filled but the partner spot is still empty")]
        public string waitHint = "";
        public bool Filled => done;

        bool done;

        public bool AcceptsItem(Transform t)
        {
            if (t == null || t.name != acceptedName) return false;
            var gm = RamenGameManager.For(this);
            return gm == null || gm.AllDone(requiredSteps);
        }

        [Tooltip("An unheld item this close (m) gets pulled in, e.g. dropped or fallen into the spot")]
        public float catchRadius = 0.3f;
        XRGrabInteractable target;

        void FixedUpdate()
        {
            if (!socketActive || hasSelection || interactionManager == null) return;
            if (target == null)
            {
                if (Time.frameCount % 30 != 0) return;
                float best = float.MaxValue;
                foreach (var g in FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None))
                {
                    if (g.name != acceptedName) continue;
                    float d = (g.transform.position - transform.position).sqrMagnitude;
                    if (d < best) { best = d; target = g; }
                }
                if (target == null) return;
            }
            if (!target.isActiveAndEnabled || target.isSelected || !AcceptsItem(target.transform)) return;
            Vector3 near = target.TryGetComponent<Collider>(out var col) ? col.ClosestPoint(transform.position) : target.transform.position;
            if ((near - transform.position).sqrMagnitude > catchRadius * catchRadius) return;
            interactionManager.SelectEnter((IXRSelectInteractor)this, (IXRSelectInteractable)target);
        }

        public override bool CanHover(IXRHoverInteractable interactable)
            => base.CanHover(interactable) && AcceptsItem(interactable.transform);

        public override bool CanSelect(IXRSelectInteractable interactable)
            => base.CanSelect(interactable) && (IsSelecting(interactable) || AcceptsItem(interactable.transform));

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            var item = args.interactableObject.transform;
            if (item.name != acceptedName) return;
            bool first = !done;
            done = true;
            if (stepIndex >= 0 && (partner == null || partner.Filled)) RamenGameManager.For(this)?.Complete(stepIndex);
            else if (first && partner != null && !partner.Filled && !string.IsNullOrEmpty(waitHint)) RamenGameManager.For(this)?.Hint(waitHint);
            if (first)
            {
                foreach (var g in hideOnPlace) if (g) g.SetActive(false);
                foreach (var g in showOnPlace) if (g) g.SetActive(true);
                if (revealItem) StartCoroutine(Reveal());
                HouseFx.Placed(this, transform.position);
            }
            if (lockOnPlace) StartCoroutine(Glue(args.interactableObject as XRGrabInteractable));
        }

        IEnumerator Reveal()
        {
            yield return new WaitForSeconds(0.2f);   // let the placed item settle on the spot first
            revealItem.gameObject.SetActive(true);
            var at = revealAt ? revealAt : transform;
            var grab = revealItem.GetComponent<XRGrabInteractable>();
            revealItem.TryGetComponent<Rigidbody>(out var rb);
            if (rb) rb.isKinematic = true;   // stays in the net until grabbed
            // keep it sitting in the middle of the net until the player grabs it
            while (revealItem && grab && !grab.isSelected)
            {
                Vector3 offset = grab.attachTransform ? revealItem.position - grab.attachTransform.position : Vector3.zero;
                Vector3 target = at.TryGetComponent<Renderer>(out var rend) ? rend.bounds.center : at.position;
                Vector3 pos = target + offset + Vector3.up * 0.02f;
                if (rb) rb.position = pos;
                revealItem.position = pos;
                yield return null;
            }
        }

        IEnumerator Glue(XRGrabInteractable grab)
        {
            yield return new WaitForEndOfFrame();
            if (grab == null) yield break;
            var item = grab.transform;
            Vector3 offset = grab.attachTransform ? item.position - grab.attachTransform.position : Vector3.zero;
            if (interactionManager != null && IsSelecting(grab)) interactionManager.SelectExit((IXRSelectInteractor)this, (IXRSelectInteractable)grab);
            // remove everything that makes it grabbable, then stick it to this spot
            foreach (var c in item.GetComponents<HoverTriggerClick>()) Destroy(c);
            foreach (var c in item.GetComponents<SnapAssist>()) Destroy(c);
            var rb = item.GetComponent<Rigidbody>();
            if (rb) { rb.isKinematic = true; rb.useGravity = false; rb.detectCollisions = false; rb.interpolation = RigidbodyInterpolation.None; }
            foreach (var c in item.GetComponents<DropOnRelease>()) Destroy(c);
            Destroy(grab);
            foreach (var col in item.GetComponents<Collider>()) Destroy(col);
            item.SetParent(transform, true);
            item.SetPositionAndRotation(transform.position + offset, transform.rotation);
            Vector3 local = item.localPosition;
            socketActive = false;
            // the released rigidbody can still push the item for a frame or two: put it back
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                if (item == null) yield break;
                item.localPosition = local;
                item.localRotation = Quaternion.identity;
                if (i == 0 && rb) Destroy(rb);   // the grab script is gone now, so the rigidbody can go too
            }
        }
    }
}

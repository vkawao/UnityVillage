using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RamenVR
{
    /// A button for any shop: press it (ray + trigger, or poke) to finish one step.
    /// It can also turn something (a sign, a lever), move something (a cart),
    /// start a Spinner (windmill) and show objects (a price on a screen).
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class StepButton : MonoBehaviour
    {
        [Header("Step")]
        public int stepIndex = -1;
        public int[] requiredSteps;
        public string notReadyHint = "";
        public bool showOrderPanel;

        [Header("Turn something (optional)")]
        public Transform rotateTarget;
        public Vector3 rotateAxis = Vector3.up;     // world axis
        public float rotateAngle = 180f;

        [Header("Move something (optional)")]
        public Transform moveTarget;
        public Vector3 moveOffset;                  // world offset

        [Header("Other (optional)")]
        public Spinner startSpinner;
        public HingeDoor[] openDoors;
        public GameObject[] showObjects;
        public float duration = 0.8f;
        public UnityEngine.Events.UnityEvent onPressed = new UnityEngine.Events.UnityEvent();

        bool used, busy;

        void Awake()
        {
            GetComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => Press());
            if (!TryGetComponent<HoverTriggerClick>(out var click)) click = gameObject.AddComponent<HoverTriggerClick>();
            click.onClick.AddListener(Press);
        }

        // rotateAxis / moveOffset were set up with the shop at rotation 0.
        // If the shop is turned or scaled in the village, turn them with it.
        void Start() { FollowShopRotation(); }

        void FollowShopRotation()
        {
            var gm = RamenGameManager.For(this);
            var root = gm != null && gm.shopRoot ? gm.shopRoot : null;
            if (root == null) return;
            rotateAxis = root.rotation * rotateAxis;
            moveOffset = root.TransformVector(moveOffset);
        }

        public void Press()
        {
            if (used || busy) return;
            var gm = RamenGameManager.For(this);
            if (gm != null && !gm.AllDone(requiredSteps))
            {
                if (!string.IsNullOrEmpty(notReadyHint)) gm.Hint(notReadyHint);
                return;
            }
            used = true;
            StartCoroutine(Run(gm));
        }

        IEnumerator Run(RamenGameManager gm)
        {
            busy = true;
            onPressed.Invoke();
            HouseFx.ButtonClick(this, transform.position);
            Vector3 s = transform.localScale;
            transform.localScale = s * 0.85f;
            if (startSpinner) startSpinner.spinning = true;
            if (openDoors != null) foreach (var d in openDoors) if (d) d.Open();
            foreach (var g in showObjects) if (g) g.SetActive(true);

            Quaternion r0 = rotateTarget ? rotateTarget.rotation : Quaternion.identity;
            Quaternion r1 = Quaternion.AngleAxis(rotateAngle, rotateAxis) * r0;
            Vector3 p0 = moveTarget ? moveTarget.position : Vector3.zero;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / duration);
                if (rotateTarget) rotateTarget.rotation = Quaternion.Slerp(r0, r1, k);
                if (moveTarget) moveTarget.position = p0 + moveOffset * k;
                if (t > 0.15f) transform.localScale = s;
                yield return null;
            }
            if (rotateTarget) rotateTarget.rotation = r1;
            if (moveTarget) moveTarget.position = p0 + moveOffset;
            transform.localScale = s;
            if (gm != null)
            {
                if (stepIndex >= 0) gm.Complete(stepIndex);
                if (showOrderPanel) gm.ShowOrderPanel();
            }
            busy = false;
        }
    }
}

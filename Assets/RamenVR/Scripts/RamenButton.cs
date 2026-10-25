using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RamenVR
{
    /// A physical button in the shop. Press it with the controller ray/trigger or by poking it.
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class RamenButton : MonoBehaviour
    {
        public enum Action { OpenDoor, ShowMenu, PourSoup }

        public Action action;
        [Tooltip("Steps that must be finished before this button works")]
        public int[] requiredSteps;
        public string notReadyHint = "";

        [Header("Targets (filled in by the setup tool)")]
        public HingeDoor door;
        public PotTilt pot;

        XRSimpleInteractable interactable;
        bool busy;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(_ => Press());
            if (!TryGetComponent<HoverTriggerClick>(out var click)) click = gameObject.AddComponent<HoverTriggerClick>();
            click.onClick.AddListener(Press);
        }

        public void Press()
        {
            if (busy) return;
            var gm = RamenGameManager.For(this);
            if (gm != null && !gm.AllDone(requiredSteps))
            {
                if (!string.IsNullOrEmpty(notReadyHint)) gm.Hint(notReadyHint);
                return;
            }
            StartCoroutine(PressAnim());

            switch (action)
            {
                case Action.OpenDoor:
                    if (door) door.Open(() => gm?.Complete(0));
                    else gm?.Complete(0);
                    break;
                case Action.ShowMenu:
                    gm?.ShowOrderPanel();
                    break;
                case Action.PourSoup:
                    if (pot) pot.Pour(() => gm?.Complete(7));
                    else gm?.Complete(7);
                    break;
            }
        }

        IEnumerator PressAnim()
        {
            busy = true;
            Vector3 s = transform.localScale;
            transform.localScale = s * 0.85f;
            yield return new WaitForSeconds(0.15f);
            transform.localScale = s;
            yield return new WaitForSeconds(0.35f);
            busy = false;
        }
    }
}

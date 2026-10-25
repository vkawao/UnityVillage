using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RamenVR
{
    /// Lets the START ORDER button be pressed with the ray (trigger) or by poking it with a hand.
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class StartOrderButton : MonoBehaviour
    {
        float last = -10f;

        void Awake()
        {
            GetComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => Press());
            if (!TryGetComponent<HoverTriggerClick>(out var click)) click = gameObject.AddComponent<HoverTriggerClick>();
            click.onClick.AddListener(Press);
        }

        void Press()
        {
            if (Time.time - last < 0.5f) return;   // one trigger pull = one press
            last = Time.time;
            var gm = RamenGameManager.For(this);
            if (gm == null) return;
            gm.StartOrder();
            if (gm.IsDone(1) && gm.TryGetComponent<FishOrder>(out var order)) order.Lock();   // fish count is fixed once the order is taken
        }
    }
}

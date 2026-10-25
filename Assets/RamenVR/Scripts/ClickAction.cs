using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RamenVR
{
    /// Small UI/world button that just runs an event when pressed (ray + trigger, or poke).
    /// Used for things that are not a step on their own, like the - / + fish buttons.
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class ClickAction : MonoBehaviour
    {
        public UnityEvent onPress = new UnityEvent();
        float last = -10f;

        void Awake()
        {
            GetComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => Press());
            if (!TryGetComponent<HoverTriggerClick>(out var click)) click = gameObject.AddComponent<HoverTriggerClick>();
            click.onClick.AddListener(Press);
        }

        public void Press()
        {
            if (Time.time - last < 0.3f) return;   // one trigger pull = one press
            last = Time.time;
            onPress.Invoke();
        }
    }
}

using UnityEngine;
using UnityEngine.Events;

namespace RamenVR
{
    /// Kept only so older objects still have their onClick hook.
    /// Pressing now goes through the normal XR "select" (trigger or grip on the controller ray,
    /// or poking with the hand), so this no longer listens to input itself. That way one
    /// trigger pull is exactly one press, and a ray resting on another button can't press it.
    public class HoverTriggerClick : MonoBehaviour
    {
        public UnityEvent onClick = new UnityEvent();
    }
}

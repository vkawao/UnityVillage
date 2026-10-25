using UnityEngine;

namespace RamenVR
{
    /// Finishes a step when the player stands (or teleports) close to this spot, e.g. sitting on the bench.
    public class ZoneStep : MonoBehaviour
    {
        public int stepIndex = -1;
        public int[] requiredSteps;
        public float radius = 0.8f;
        RamenGameManager gm;

        void Start() { gm = RamenGameManager.For(this); }

        void Update()
        {
            if (gm == null || Camera.main == null || gm.IsDone(stepIndex)) return;
            Vector3 d = Camera.main.transform.position - transform.position; d.y = 0;
            if (d.sqrMagnitude < radius * radius && gm.AllDone(requiredSteps)) gm.Complete(stepIndex);
        }
    }
}

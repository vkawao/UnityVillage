using UnityEngine;

namespace RamenVR
{
    /// Keeps turning an object (windmill blades, wheels) while spinning is on.
    public class Spinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.forward;   // world axis
        public float degreesPerSecond = 90f;
        public bool spinning;

        // set up with the shop at rotation 0: turn it with the shop in the village (ShopRotationApplied)
        void Start()
        {
            var gm = RamenGameManager.For(this);
            if (gm != null && gm.shopRoot) axis = gm.shopRoot.rotation * axis;
        }

        void Update()
        {
            if (spinning) transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.World);
        }
    }
}

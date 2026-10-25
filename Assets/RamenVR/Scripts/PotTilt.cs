using System;
using System.Collections;
using UnityEngine;

namespace RamenVR
{
    /// Tilts the soup pot toward the bowl, shows the soup inside the bowl, then tilts back.
    public class PotTilt : MonoBehaviour
    {
        public Vector3 tiltAxis = Vector3.forward;   // world axis, set by the setup tool
        public float tiltAngle = 35f;
        public float duration = 0.8f;
        public GameObject soupInBowl;                // hidden until poured

        bool poured;

        // set up with the shop at rotation 0: turn it with the shop in the village (ShopRotationApplied)
        void Start()
        {
            var gm = RamenGameManager.For(this);
            if (gm != null && gm.shopRoot) tiltAxis = gm.shopRoot.rotation * tiltAxis;
        }

        public void Pour(Action onDone = null)
        {
            if (poured) { onDone?.Invoke(); return; }
            poured = true;
            StartCoroutine(PourRoutine(onDone));
        }

        IEnumerator PourRoutine(Action onDone)
        {
            Quaternion start = transform.rotation;
            Quaternion tilted = Quaternion.AngleAxis(tiltAngle, tiltAxis) * start;

            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Slerp(start, tilted, t / duration);
                yield return null;
            }
            transform.rotation = tilted;
            if (soupInBowl) soupInBowl.SetActive(true);
            yield return new WaitForSeconds(0.6f);

            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Slerp(tilted, start, t / duration);
                yield return null;
            }
            transform.rotation = start;
            onDone?.Invoke();
        }
    }
}

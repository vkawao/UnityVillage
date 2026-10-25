using System;
using System.Collections;
using UnityEngine;

namespace RamenVR
{
    /// Slides an object (the shop door) by a world-space offset.
    public class SlideMover : MonoBehaviour
    {
        public Vector3 openOffset = new Vector3(-1.2f, 0f, 0f);
        public float duration = 1.2f;

        bool isOpen;

        // set up with the shop at rotation 0: turn it with the shop in the village (ShopRotationApplied)
        void Start()
        {
            var gm = RamenGameManager.For(this);
            if (gm != null && gm.shopRoot) openOffset = gm.shopRoot.TransformVector(openOffset);
        }

        public void Open(Action onDone = null)
        {
            if (isOpen) { onDone?.Invoke(); return; }
            isOpen = true;
            StartCoroutine(Move(onDone));
        }

        IEnumerator Move(Action onDone)
        {
            Vector3 start = transform.position;
            Vector3 end = start + openOffset;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0, 1, t / duration));
                yield return null;
            }
            transform.position = end;
            onDone?.Invoke();
        }
    }
}

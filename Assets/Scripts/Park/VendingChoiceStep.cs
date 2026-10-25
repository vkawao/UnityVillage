using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: pick a drink on the vending machine's buttons. The matching can drops into
    /// the tray (clunk), then after a moment rolls out of the slot toward the bench so the
    /// player has to follow it. Completes on the button press; the roll carries on after.
    /// </summary>
    public class VendingChoiceStep : QuestStep, IQuestItemSource
    {
        [Tooltip("One button per drink, same order as the cans.")]
        [SerializeField] private Button[] m_Buttons;
        [SerializeField] private XRGrabInteractable[] m_Cans;
        [Tooltip("Inside the tray, at the tray floor.")]
        [SerializeField] private Transform m_Outlet;
        [Tooltip("Where the can stops rolling (on the ground).")]
        [SerializeField] private Transform m_RollTarget;
        [SerializeField] private float m_RollDelay = 1.6f;
        [SerializeField] private float m_RollDuration = 2.4f;
        [SerializeField, Range(0f, 1f)] private float m_DimmedAlpha = 0.35f;

        public XRGrabInteractable ChosenItem { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            SetButtons(false, -1);
        }

        protected override void OnBegin()
        {
            for (int i = 0; i < m_Buttons.Length; i++)
            {
                if (m_Buttons[i] == null) continue;
                int index = i;
                m_Buttons[i].onClick.AddListener(() => OnPicked(index));
            }
            SetButtons(true, -1);
        }

        protected override void OnEnd()
        {
            foreach (Button b in m_Buttons)
            {
                if (b != null) b.onClick.RemoveAllListeners();
            }
        }

        private void OnPicked(int index)
        {
            if (!IsActive || index >= m_Cans.Length || m_Cans[index] == null) return;
            ChosenItem = m_Cans[index];
            SynthAudio.PlayAt(SynthSound.ButtonBeep, m_Buttons[index].transform.position, 0.7f);
            SetButtons(false, index);
            StartCoroutine(Dispense(ChosenItem));
            Complete();
        }

        /// <summary>Enable/disable the buttons; the chosen one stays bright, the rest dim.</summary>
        private void SetButtons(bool interactable, int chosen)
        {
            for (int i = 0; i < m_Buttons.Length; i++)
            {
                Button b = m_Buttons[i];
                if (b == null) continue;
                b.interactable = interactable;
                bool bright = interactable || i == chosen;
                if (b.image != null)
                {
                    Color c = b.image.color;
                    c.a = bright ? 1f : m_DimmedAlpha;
                    b.image.color = c;
                }
                var label = b.GetComponentInChildren<TMP_Text>();
                if (label != null) label.alpha = bright ? 1f : 0.5f;
            }
        }

        private IEnumerator Dispense(XRGrabInteractable can)
        {
            if (m_Outlet == null) yield break;
            GameObject go = can.gameObject;
            Rigidbody body = can.GetComponent<Rigidbody>();
            RigidbodyInterpolation interpolation = body != null ? body.interpolation : RigidbodyInterpolation.None;

            Vector3 dir = Vector3.forward;
            if (m_RollTarget != null)
            {
                dir = m_RollTarget.position - m_Outlet.position;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            }
            GetCanAxes(go, out Vector3 longAxisLocal, out float radius);
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            Quaternion lying = Quaternion.FromToRotation(go.transform.rotation * longAxisLocal, side) * go.transform.rotation;

            Vector3 start = m_Outlet.position + Vector3.up * radius;

            // Place it in the tray BEFORE showing it, and switch off interpolation while the script
            // moves it; otherwise the body is smoothed back toward where it was hidden.
            if (body != null)
            {
                body.interpolation = RigidbodyInterpolation.None;
                body.isKinematic = true;
            }
            can.enabled = false; // can't grab it mid-roll (grabbing a scripted body would leave it floating)
            MoveTo(go, body, start, lying);
            go.SetActive(true);
            SynthAudio.PlayAt(SynthSound.CanDrop, start, 0.9f);

            yield return new WaitForSeconds(m_RollDelay);

            if (m_RollTarget != null)
            {
                Vector3 end = m_RollTarget.position + Vector3.up * radius;
                float distance = Vector3.Distance(start, end);
                SynthAudio.PlayAt(SynthSound.CanRoll, start, 0.8f);
                for (float t = 0f; t < m_RollDuration; t += Time.deltaTime)
                {
                    float k = 1f - (1f - t / m_RollDuration) * (1f - t / m_RollDuration); // ease out
                    float angle = k * distance / Mathf.Max(radius, 0.01f) * Mathf.Rad2Deg;
                    MoveTo(go, body, Vector3.Lerp(start, end, k), Quaternion.AngleAxis(angle, side) * lying);
                    yield return null;
                }
                MoveTo(go, body, end, Quaternion.AngleAxis(distance / Mathf.Max(radius, 0.01f) * Mathf.Rad2Deg, side) * lying);
            }

            if (body != null)
            {
                body.interpolation = interpolation;
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            can.enabled = true;
        }

        /// <summary>Move the transform and the physics body together (no lag, no smoothing back).</summary>
        private static void MoveTo(GameObject go, Rigidbody body, Vector3 position, Quaternion rotation)
        {
            go.transform.SetPositionAndRotation(position, rotation);
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
            }
        }

        /// <summary>The can's long (height) axis in local space and its radius in metres.</summary>
        private static void GetCanAxes(GameObject go, out Vector3 longAxisLocal, out float radius)
        {
            Renderer r = go.GetComponentInChildren<Renderer>();
            Vector3 size = r != null ? Vector3.Scale(r.localBounds.size, go.transform.lossyScale) : new Vector3(0.066f, 0.12f, 0.066f);
            int axis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            longAxisLocal = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            float a = axis == 0 ? size.y : size.x;
            float b = axis == 2 ? size.y : size.z;
            radius = Mathf.Min(a, b) * 0.5f;
        }
    }
}

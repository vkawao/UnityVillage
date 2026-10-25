using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: the claw machine. Steer the claw with the LEFT / RIGHT / BACK / FRONT buttons on the
    /// control panel, then press DROP (or the red button). The claw goes down, always catches a plush
    /// (it drifts onto the nearest one), carries it to the chute and lets go; the machine spits the
    /// plush out of the flap at the front onto the floor. The action is won when the plush comes out.
    /// </summary>
    public class ClawGameStep : ArcadeGameStep
    {
        [Header("Claw machine")]
        [SerializeField] private Transform m_Claw;
        [Tooltip("Origin height minus the tips of the prongs (m).")]
        [SerializeField] private float m_ClawLength = 0.45f;
        [SerializeField] private XRGrabInteractable[] m_Plushes;
        [SerializeField] private Button m_Left;
        [SerializeField] private Button m_Right;
        [SerializeField] private Button m_Back;
        [SerializeField] private Button m_Front;
        [SerializeField] private Button m_Drop;
        [Tooltip("The machine's red button: touching it also drops the claw.")]
        [SerializeField] private XRSimpleInteractable m_DropButton;
        [Tooltip("Middle of the area the claw can move over (only its horizontal position is used).")]
        [SerializeField] private Transform m_Area;
        [Tooltip("World direction away from the player (into the machine).")]
        [SerializeField] private Vector3 m_BackDir = Vector3.forward;
        [SerializeField] private float m_HalfWidth = 0.35f;
        [SerializeField] private float m_HalfDepth = 0.35f;
        [Tooltip("How far (m) one button press moves the claw.")]
        [SerializeField] private float m_StepSize = 0.1f;
        [SerializeField] private float m_Speed = 0.35f;
        [Tooltip("Above the prize chute, where the claw lets go.")]
        [SerializeField] private Transform m_Chute;
        [Tooltip("The prize flap on the front (bottom middle): the won plush is spat out of it.")]
        [SerializeField] private Transform m_PrizeOut;
        [Tooltip("World direction out of the machine's front (the plush flies out this way onto the floor).")]
        [SerializeField] private Vector3 m_SpitDirection = Vector3.back;
        [SerializeField] private float m_SpitSpeed = 1.3f;
        [SerializeField] private float m_Reach = 0.1f;

        private Vector2 m_Target;   // (right, back) from the area's middle
        private Vector2 m_Pos;
        private Vector3 m_RestPos;
        private bool m_Busy;
        private XRGrabInteractable m_Won;

        /// <summary>The plush that came out of the machine (null until then).</summary>
        public XRGrabInteractable WonPlush => m_Won;

        private Vector3 Back => Vector3.ProjectOnPlane(m_BackDir, Vector3.up).normalized;
        private Vector3 Right => Vector3.Cross(Vector3.up, Back); // the player's right as they face the machine

        protected override string PlayingHint =>
            "Move the claw over a plush with the <b>arrow buttons</b>, then press <b>DROP</b>!\n" +
            $"<color={HintColor}><i>Point at a button and pull the trigger</i></color>";

        protected override Transform GameGuideTarget => m_Drop != null ? m_Drop.transform : m_Claw;

        protected override void Awake()
        {
            base.Awake();
            if (m_Claw != null) m_RestPos = m_Claw.position;
            // The plushes stay put in the machine until one is won.
            if (m_Plushes != null)
                foreach (XRGrabInteractable p in m_Plushes)
                    if (p != null) Lock(p, true);
            SetButtons(false);
        }

        protected override void OnGameStart()
        {
            m_Busy = false;
            m_Pos = m_Target = ToArea(m_Claw.position);
            SetScreen($"<size=55%>{GameTitle}</size>\n<b>MOVE + DROP</b>");
            SetButtons(true);
            Hook(m_Left, () => Nudge(-1f, 0f));
            Hook(m_Right, () => Nudge(1f, 0f));
            Hook(m_Back, () => Nudge(0f, 1f));
            Hook(m_Front, () => Nudge(0f, -1f));
            Hook(m_Drop, Drop);
        }

        protected override void OnEnd()
        {
            base.OnEnd();
            foreach (Button b in new[] { m_Left, m_Right, m_Back, m_Front, m_Drop })
                if (b != null) b.onClick.RemoveAllListeners();
            SetButtons(false);
        }

        private static void Hook(Button b, UnityEngine.Events.UnityAction action)
        {
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(action);
        }

        private void SetButtons(bool on)
        {
            foreach (Button b in new[] { m_Left, m_Right, m_Back, m_Front, m_Drop })
                if (b != null) b.interactable = on;
        }

        protected override void UpdateGame()
        {
            if (m_Claw == null || m_Busy) return;
            m_Pos = Vector2.MoveTowards(m_Pos, m_Target, m_Speed * Time.deltaTime);
            m_Claw.position = FromArea(m_Pos, m_RestPos.y);
            if (IsTouched(m_DropButton)) Drop();
        }

        private void Nudge(float right, float back)
        {
            if (!Playing || m_Busy) return;
            m_Target += new Vector2(right, back) * m_StepSize;
            m_Target.x = Mathf.Clamp(m_Target.x, -m_HalfWidth, m_HalfWidth);
            m_Target.y = Mathf.Clamp(m_Target.y, -m_HalfDepth, m_HalfDepth);
            SynthAudio.PlayAt(SynthSound.ClawWhirr, m_Claw.position, 0.6f);
        }

        private void Drop()
        {
            if (!Playing || m_Busy || m_Claw == null) return;
            XRGrabInteractable plush = NearestPlush();
            if (plush == null) return;
            m_Busy = true;
            SetButtons(false);
            SynthAudio.PlayAt(SynthSound.ButtonBeep, m_Claw.position, 0.6f);
            StartCoroutine(Grab(plush));
        }

        private IEnumerator Grab(XRGrabInteractable plush)
        {
            SetScreen($"<size=55%>{GameTitle}</size>\n<b>DROPPING...</b>");
            Renderer r = plush.GetComponent<Renderer>();
            float top = r != null ? r.bounds.max.y : plush.transform.position.y + 0.2f;
            Vector2 at = ToArea(r != null ? r.bounds.center : plush.transform.position);

            // Down, drifting onto the plush: this claw never misses.
            SynthAudio.PlayAt(SynthSound.ClawWhirr, m_Claw.position, 0.6f);
            Vector3 from = m_Claw.position;
            Vector3 down = FromArea(at, top + m_ClawLength - 0.06f);
            yield return MoveClaw(from, down, 1.2f, null, Vector3.zero);
            SynthAudio.PlayAt(SynthSound.Click, m_Claw.position, 0.8f);
            yield return new WaitForSeconds(0.3f);

            // Up with the plush, over to the chute.
            Vector3 offset = plush.transform.position - m_Claw.position;
            Vector3 up = FromArea(at, m_RestPos.y);
            SynthAudio.PlayAt(SynthSound.ClawWhirr, m_Claw.position, 0.6f);
            yield return MoveClaw(m_Claw.position, up, 1f, plush.transform, offset);
            if (m_Chute != null)
            {
                Vector3 over = FromArea(ToArea(m_Chute.position), m_RestPos.y);
                SynthAudio.PlayAt(SynthSound.ClawWhirr, m_Claw.position, 0.6f);
                yield return MoveClaw(m_Claw.position, over, 1.1f, plush.transform, offset);
            }

            // Let go: it falls into the chute...
            SynthAudio.PlayAt(SynthSound.Click, m_Claw.position, 0.8f);
            Vector3 p0 = plush.transform.position;
            float floor = m_Chute != null ? m_Chute.position.y - 0.3f : p0.y - 0.5f;
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                plush.transform.position = new Vector3(p0.x, Mathf.Lerp(p0.y, floor, t * t / 0.16f), p0.z);
                yield return null;
            }
            plush.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.4f);

            // ...and the machine spits it out of the prize flap onto the floor.
            plush.gameObject.SetActive(true);
            Lock(plush, false);
            Rigidbody body = plush.GetComponent<Rigidbody>();
            if (m_PrizeOut != null)
            {
                Vector3 spit = Vector3.ProjectOnPlane(m_SpitDirection, Vector3.up).normalized;
                Vector3 toCenter = r != null ? plush.transform.position - r.bounds.center : Vector3.zero;
                float ahead = r != null ? Mathf.Abs(Vector3.Dot(r.bounds.extents, spit)) : 0.1f;
                float halfHeight = r != null ? r.bounds.extents.y : 0.12f;
                Vector3 center = m_PrizeOut.position + spit * (ahead + 0.03f) + Vector3.up * (halfHeight + 0.02f); // just clear of the front
                plush.transform.position = center + toCenter;
                if (body != null)
                {
                    body.position = plush.transform.position;
                    body.isKinematic = false;
                    body.useGravity = true;
                    body.linearVelocity = spit * m_SpitSpeed + Vector3.up * 0.6f;
                    body.angularVelocity = Random.insideUnitSphere * 3f; // tumbles a little
                }
                SynthAudio.PlayAt(SynthSound.SnackDrop, m_PrizeOut.position, 0.9f);
            }
            m_Won = plush;
            Win($"<size=55%>{GameTitle}</size>\n<b><color={OnColor}>YOU WON!</color></b>",
                $"<color={OnColor}>You won a plush!</color> <color={TextColor}>The machine spat it out - pick it up from the <b>floor</b>.</color>");

            // The claw goes home.
            yield return MoveClaw(m_Claw.position, m_RestPos, 1.2f, null, Vector3.zero);
        }

        private IEnumerator MoveClaw(Vector3 from, Vector3 to, float time, Transform carry, Vector3 offset)
        {
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                m_Claw.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time));
                if (carry != null) carry.position = m_Claw.position + offset;
                yield return null;
            }
            m_Claw.position = to;
            if (carry != null) carry.position = to + offset;
        }

        private XRGrabInteractable NearestPlush()
        {
            XRGrabInteractable best = null;
            float bestD = float.MaxValue;
            Vector2 claw = ToArea(m_Claw.position);
            foreach (XRGrabInteractable p in m_Plushes)
            {
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                float d = Vector2.Distance(ToArea(p.transform.position), claw);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>Frozen and not grabbable (in the machine), or a normal item presented in the flap.</summary>
        private static void Lock(XRGrabInteractable plush, bool locked)
        {
            Rigidbody body = plush.GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = true; // stays put until grabbed (FreeAfterGrab frees it)
            plush.enabled = !locked;
        }

        private Vector2 ToArea(Vector3 world)
        {
            Vector3 d = world - (m_Area != null ? m_Area.position : m_RestPos);
            return new Vector2(Vector3.Dot(d, Right), Vector3.Dot(d, Back));
        }

        private Vector3 FromArea(Vector2 a, float y)
        {
            Vector3 c = m_Area != null ? m_Area.position : m_RestPos;
            Vector3 p = c + Right * a.x + Back * a.y;
            p.y = y;
            return p;
        }

        private bool IsTouched(XRSimpleInteractable button)
        {
            if (button == null) return false;
            if (button.isSelected) return true;
            var hovering = button.interactorsHovering;
            for (int i = 0; i < hovering.Count; i++)
            {
                Vector3 hand = hovering[i].transform.position;
                foreach (Collider c in button.colliders)
                    if (c != null && Vector3.Distance(c.ClosestPoint(hand), hand) <= m_Reach) return true; // far rays hover too
            }
            return false;
        }
    }
}

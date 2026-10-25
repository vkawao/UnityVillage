using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: shoot hoops. Throw the basketballs at the hoop; a ball dropping down through the rim
    /// scores (swish), and the ball comes back to the ramp. Score 3 to win.
    /// A throw aimed roughly at the hoop gets a gentle aim assist, so a fair throw goes in.
    /// Balls that end up anywhere else roll back to the ramp after a moment.
    /// </summary>
    public class HoopsGameStep : ArcadeGameStep
    {
        [Header("Hoops")]
        [SerializeField] private XRGrabInteractable[] m_Balls;
        [Tooltip("Where each ball rests on the ramp (same order).")]
        [SerializeField] private Transform[] m_Spots;
        [Tooltip("Centre of the rim (its top).")]
        [SerializeField] private Transform m_Hoop;
        [SerializeField] private float m_HoopRadius = 0.2f;
        [SerializeField] private int m_BasketsToWin = 3;
        [Tooltip("A throw within this angle (degrees, seen from above) of the hoop is helped into it.")]
        [SerializeField] private float m_AssistAngle = 30f;
        [Tooltip("Slowest throw (m/s) that gets the aim assist.")]
        [SerializeField] private float m_AssistMinSpeed = 1.2f;
        [Tooltip("A ball left alone this long (s) away from its spot rolls back to the ramp.")]
        [SerializeField] private float m_ReturnAfter = 3f;

        private int m_Baskets;
        private Vector3[] m_LastPos;
        private float[] m_IdleTime;
        private bool[] m_Returning;

        protected override string PlayingHint =>
            "Grab a <b>basketball</b> from the ramp and throw it into the <b>hoop</b>!\n" +
            $"<color={HintColor}><i>Swing your arm toward the hoop and let go</i></color>";

        protected override Transform GameGuideTarget
        {
            get
            {
                if (m_Balls != null)
                    foreach (XRGrabInteractable b in m_Balls)
                        if (b != null && GrabUtil.IsHeldByHand(b)) return m_Hoop;
                return m_Balls != null && m_Balls.Length > 0 && m_Balls[0] != null ? m_Balls[0].transform : m_Hoop;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            int n = m_Balls != null ? m_Balls.Length : 0;
            m_LastPos = new Vector3[n];
            m_IdleTime = new float[n];
            m_Returning = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (m_Balls[i] == null) continue;
                m_Balls[i].selectExited.AddListener(OnReleased);
            }
        }

        private void Start()
        {
            // Balls rest on the ramp (frozen) until they're picked up.
            for (int i = 0; m_Balls != null && i < m_Balls.Length; i++) PutBack(i, false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (m_Balls == null) return;
            foreach (XRGrabInteractable b in m_Balls)
                if (b != null) b.selectExited.RemoveListener(OnReleased);
        }

        protected override void OnGameStart()
        {
            m_Baskets = 0;
            ShowScore("BASKETS", 0, m_BasketsToWin);
            for (int i = 0; i < m_Balls.Length; i++)
                if (m_Balls[i] != null) m_LastPos[i] = m_Balls[i].transform.position;
        }

        protected override void Update()
        {
            base.Update();
            if (m_Balls == null) return;
            // Ball return works all the time (also before and after the game), so balls never get lost.
            for (int i = 0; i < m_Balls.Length; i++) UpdateReturn(i);
        }

        protected override void UpdateGame()
        {
            if (m_Hoop == null) return;
            Vector3 hoop = m_Hoop.position;
            for (int i = 0; i < m_Balls.Length; i++)
            {
                XRGrabInteractable b = m_Balls[i];
                if (b == null || m_Returning[i]) continue;
                Vector3 p = b.transform.position, prev = m_LastPos[i];
                m_LastPos[i] = p;
                if (GrabUtil.IsHeldByHand(b)) continue;
                // Dropping down through the rim: above it last frame, at/below it now, inside the ring.
                bool crossed = prev.y > hoop.y && p.y <= hoop.y;
                Vector3 mid = Vector3.Lerp(prev, p, Mathf.InverseLerp(prev.y, p.y, hoop.y));
                float off = new Vector2(mid.x - hoop.x, mid.z - hoop.z).magnitude;
                if (crossed && off <= m_HoopRadius) Score(i);
            }
        }

        private void Score(int i)
        {
            m_Baskets++;
            SynthAudio.PlayAt(SynthSound.NetSwish, m_Hoop.position, 0.9f);
            SynthAudio.PlayAt(SynthSound.Ding, m_Hoop.position, 0.6f);
            ShowScore("BASKETS", m_Baskets, m_BasketsToWin);
            StartCoroutine(ReturnSoon(i, 0.8f));
            if (m_Baskets >= m_BasketsToWin)
            {
                Win($"<size=55%>{GameTitle}</size>\n<b><color={OnColor}>YOU WIN!</color></b>\n<size=55%>{m_Baskets} BASKETS</size>",
                    $"<color={OnColor}>Nothing but net! {m_Baskets} baskets!</color>");
                return;
            }
            SetProgress($"<color={OnColor}>Swish!</color> <color={TextColor}>{m_Baskets}/{m_BasketsToWin} - keep shooting!</color>");
        }

        /// <summary>Aim assist: a throw heading roughly at the hoop gets an arc that drops into it.</summary>
        private void OnReleased(SelectExitEventArgs args)
        {
            if (!Playing || m_Hoop == null) return;
            var grab = args.interactableObject as XRGrabInteractable;
            if (grab == null || grab.isSelected) return;
            Rigidbody body = grab.GetComponent<Rigidbody>();
            if (body != null) StartCoroutine(AssistThrow(body));
        }

        private IEnumerator AssistThrow(Rigidbody body)
        {
            yield return null; // the grab applies the throw velocity at the end of the release frame
            if (body == null || body.isKinematic || !Playing) yield break;
            Vector3 v = body.linearVelocity;
            Vector3 to = m_Hoop.position + Vector3.up * 0.05f - body.position;
            Vector3 flatV = new Vector3(v.x, 0f, v.z), flatTo = new Vector3(to.x, 0f, to.z);
            if (v.magnitude < m_AssistMinSpeed || flatV.sqrMagnitude < 0.01f || Vector3.Angle(flatV, flatTo) > m_AssistAngle) yield break;

            float d = flatTo.magnitude;
            float t = Mathf.Clamp(0.45f + 0.1f * d, 0.5f, 0.8f); // flight time: a nice arc, under the machine's roof
            Vector3 g = Physics.gravity;
            body.linearVelocity = (to - 0.5f * g * t * t) / t;
            body.angularVelocity = Vector3.zero;
        }

        private void UpdateReturn(int i)
        {
            XRGrabInteractable b = m_Balls[i];
            if (b == null || m_Returning[i] || m_Spots == null || i >= m_Spots.Length || m_Spots[i] == null) return;
            Rigidbody body = b.GetComponent<Rigidbody>();
            bool resting = body == null || body.isKinematic || body.linearVelocity.magnitude < 0.05f;
            bool away = Vector3.Distance(b.transform.position, m_Spots[i].position) > 0.3f;
            if (b.isSelected || !resting || !away || body != null && body.isKinematic)
            {
                m_IdleTime[i] = 0f;
                return;
            }
            m_IdleTime[i] += Time.deltaTime;
            if (m_IdleTime[i] >= m_ReturnAfter) StartCoroutine(ReturnSoon(i, 0f));
        }

        private IEnumerator ReturnSoon(int i, float delay)
        {
            m_Returning[i] = true;
            yield return new WaitForSeconds(delay);
            PutBack(i, true);
            m_IdleTime[i] = 0f;
            m_Returning[i] = false;
        }

        /// <summary>The ball rolls back down the return: it reappears on its spot on the ramp, frozen until grabbed.</summary>
        private void PutBack(int i, bool sound)
        {
            XRGrabInteractable b = m_Balls[i];
            if (b == null || m_Spots == null || i >= m_Spots.Length || m_Spots[i] == null || b.isSelected) return;
            Rigidbody body = b.GetComponent<Rigidbody>();
            var free = b.GetComponent<FreeAfterGrab>();
            if (free != null) free.Present();
            else if (body != null) body.isKinematic = true;
            b.transform.SetPositionAndRotation(m_Spots[i].position, m_Spots[i].rotation);
            if (body != null)
            {
                body.position = m_Spots[i].position;
                body.rotation = m_Spots[i].rotation;
            }
            m_LastPos[i] = m_Spots[i].position;
            if (sound) SynthAudio.PlayAt(SynthSound.BallBounce, m_Spots[i].position, 0.7f);
        }
    }
}

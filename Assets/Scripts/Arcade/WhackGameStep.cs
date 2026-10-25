using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: whack-a-mole. Moles pop up out of their holes one at a time (moved by script);
    /// bonk one with the hammer's head before it ducks back down. Hit 5 to win.
    /// </summary>
    public class WhackGameStep : ArcadeGameStep
    {
        [Header("Whack-a-mole")]
        [SerializeField] private XRGrabInteractable m_Hammer;
        [Tooltip("A point in the middle of the hammer's head.")]
        [SerializeField] private Transform m_HammerHead;
        [SerializeField] private Transform[] m_Moles;
        [Tooltip("World height of a mole's origin when hiding / popped up.")]
        [SerializeField] private float m_DownY;
        [SerializeField] private float m_UpY;
        [SerializeField] private int m_HitsToWin = 5;
        [Tooltip("Seconds a mole stays up before it ducks again.")]
        [SerializeField] private float m_UpTime = 2f;
        [Tooltip("The head must come this close (m) to the mole.")]
        [SerializeField] private float m_Reach = 0.05f;
        [Tooltip("Slowest swing (m/s) that counts as a whack.")]
        [SerializeField] private float m_MinSwing = 0.25f;
        [SerializeField] private QuestCelebration m_Sparkle;

        private int m_Hits;
        private int m_Up = -1;
        private int m_Previous = -1;
        private float m_DownAt;
        private float m_NextPop;
        private Vector3 m_LastHead;
        private float m_HeadSpeed;
        private Renderer[] m_MoleRenderers;
        private Coroutine[] m_Moving;

        protected override string PlayingHint =>
            "Grab the <b>hammer</b> and bonk the mole that pops up!\n" +
            $"<color={HintColor}><i>Hit it on the head before it hides</i></color>";

        protected override Transform GameGuideTarget
        {
            get
            {
                if (m_Hammer != null && !GrabUtil.IsHeldByHand(m_Hammer)) return m_Hammer.transform;
                return m_Up >= 0 ? m_Moles[m_Up] : null;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            int n = m_Moles != null ? m_Moles.Length : 0;
            m_MoleRenderers = new Renderer[n];
            m_Moving = new Coroutine[n];
            for (int i = 0; i < n; i++)
            {
                if (m_Moles[i] == null) continue;
                m_MoleRenderers[i] = m_Moles[i].GetComponent<Renderer>();
                SetHeight(i, m_DownY); // all hidden until the game starts
            }
        }

        protected override void OnGameStart()
        {
            m_Hits = 0;
            m_Up = -1;
            m_Previous = -1;
            m_NextPop = Time.time + 0.3f;
            if (m_HammerHead != null) m_LastHead = m_HammerHead.position;
            ShowScore("MOLES", 0, m_HitsToWin);
        }

        protected override void OnEnd()
        {
            base.OnEnd();
            if (m_Up >= 0) Move(m_Up, m_DownY, 0.15f);
            m_Up = -1;
        }

        protected override void UpdateGame()
        {
            if (m_Moles == null || m_Moles.Length == 0) return;

            if (m_HammerHead != null)
            {
                Vector3 head = m_HammerHead.position;
                float speed = (head - m_LastHead).magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
                m_HeadSpeed = Mathf.Max(speed, m_HeadSpeed * 0.8f); // hold a swing's speed for a few frames
                m_LastHead = head;
            }

            if (m_Up < 0)
            {
                if (Time.time >= m_NextPop) Pop();
                return;
            }
            if (Time.time >= m_DownAt)
            {
                Move(m_Up, m_DownY, 0.15f); // too slow: it hides, another one pops up
                m_Previous = m_Up;
                m_Up = -1;
                m_NextPop = Time.time + 0.4f;
                return;
            }
            if (IsWhacked(m_Up)) Whack();
        }

        private void Pop()
        {
            int n = m_Moles.Length;
            int pick = Random.Range(0, n);
            if (n > 1 && pick == m_Previous) pick = (pick + 1 + Random.Range(0, n - 1)) % n;
            m_Up = pick;
            m_DownAt = Time.time + m_UpTime;
            Move(pick, m_UpY, 0.12f);
            SynthAudio.PlayAt(SynthSound.MolePop, m_Moles[pick].position, 0.6f);
        }

        private bool IsWhacked(int i)
        {
            if (m_HammerHead == null || m_Hammer == null || !GrabUtil.IsHeldByHand(m_Hammer)) return false;
            Renderer r = m_MoleRenderers[i];
            if (r == null) return false;
            Vector3 head = m_HammerHead.position;
            return Vector3.Distance(r.bounds.ClosestPoint(head), head) <= m_Reach && m_HeadSpeed >= m_MinSwing;
        }

        private void Whack()
        {
            int i = m_Up;
            Vector3 top = m_MoleRenderers[i] != null ? m_MoleRenderers[i].bounds.center : m_Moles[i].position;
            SynthAudio.PlayAt(SynthSound.Bonk, top, 0.9f);
            if (m_Sparkle != null) m_Sparkle.Sparkle(top, 20);
            Move(i, m_DownY, 0.08f);
            m_Previous = i;
            m_Up = -1;
            m_Hits++;
            ShowScore("MOLES", m_Hits, m_HitsToWin);
            if (m_Hits >= m_HitsToWin)
            {
                Win($"<size=55%>{GameTitle}</size>\n<b><color={OnColor}>YOU WIN!</color></b>\n<size=55%>{m_Hits} MOLES</size>",
                    $"<color={OnColor}>Bonk! You got {m_Hits} moles!</color>");
                return;
            }
            m_NextPop = Time.time + 0.5f;
        }

        private void Move(int i, float y, float time)
        {
            if (m_Moving[i] != null) StopCoroutine(m_Moving[i]);
            m_Moving[i] = StartCoroutine(MoveTo(i, y, time));
        }

        private IEnumerator MoveTo(int i, float y, float time)
        {
            float from = m_Moles[i].position.y;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                SetHeight(i, Mathf.Lerp(from, y, Mathf.SmoothStep(0f, 1f, t / time)));
                yield return null;
            }
            SetHeight(i, y);
            m_Moving[i] = null;
        }

        private void SetHeight(int i, float y)
        {
            Vector3 p = m_Moles[i].position;
            m_Moles[i].position = new Vector3(p.x, y, p.z);
        }
    }
}

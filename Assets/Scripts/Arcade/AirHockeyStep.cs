using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: air hockey. Grab the red pusher and hit the puck into the goal at the far end of
    /// the table. The puck slides on the table (it can't leave it: its height is locked), bounces off
    /// the rails, and drops into the goal's gap. One goal wins; the score is on a board over the table.
    /// </summary>
    public class AirHockeyStep : ArcadeGameStep
    {
        [Header("Air hockey")]
        [SerializeField] private Rigidbody m_Puck;
        [Tooltip("Where the puck is put down at the start (and after it leaves the table).")]
        [SerializeField] private Transform m_PuckStart;
        [SerializeField] private XRGrabInteractable m_Pusher;
        [Tooltip("Middle of the goal mouth; its forward points away from the player, into the goal.")]
        [SerializeField] private Transform m_Goal;
        [SerializeField] private float m_GoalHalfWidth = 0.15f;
        [SerializeField] private int m_GoalsToWin = 1;
        [Tooltip("A puck further than this (m) from its start has left the table and comes back.")]
        [SerializeField] private float m_LostDistance = 1.6f;

        private int m_Goals;
        private bool m_Scoring;

        protected override string PlayingHint =>
            "Grab the <b>red pusher</b> and hit the puck into the <b>goal at the far end</b>!\n" +
            $"<color={HintColor}><i>Slide the pusher along the table and whack the puck</i></color>";

        protected override Transform GameGuideTarget =>
            m_Pusher != null && !GrabUtil.IsHeldByHand(m_Pusher) ? m_Pusher.transform : m_Goal;

        protected override void OnGameStart()
        {
            m_Goals = 0;
            m_Scoring = false;
            ShowScore("GOALS", 0, m_GoalsToWin);
            ResetPuck();
        }

        protected override void UpdateGame()
        {
            if (m_Puck == null || m_Goal == null || m_Scoring) return;
            Vector3 d = m_Puck.position - m_Goal.position;
            Vector3 into = Vector3.ProjectOnPlane(m_Goal.forward, Vector3.up).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, into);
            if (Vector3.Dot(d, into) > 0f && Mathf.Abs(Vector3.Dot(d, across)) <= m_GoalHalfWidth)
            {
                StartCoroutine(Goal());
                return;
            }
            if (m_PuckStart != null && Vector3.Distance(m_Puck.position, m_PuckStart.position) > m_LostDistance) ResetPuck();
        }

        private IEnumerator Goal()
        {
            m_Scoring = true;
            m_Goals++;
            SynthAudio.PlayAt(SynthSound.GoalHorn, m_Goal.position, 0.8f);
            ShowScore("GOALS", m_Goals, m_GoalsToWin);
            if (m_Goals >= m_GoalsToWin)
            {
                Win($"<size=55%>{GameTitle}</size>\n<b><color={OnColor}>GOAL!</color></b>\n<size=55%>YOU WIN!</size>",
                    $"<color={OnColor}>GOAL! What a shot!</color>");
            }
            yield return new WaitForSeconds(1.2f);
            ResetPuck(); // a new puck pops up in the middle (also after the win, for anyone who wants to keep playing)
            m_Scoring = false;
        }

        private void ResetPuck()
        {
            if (m_Puck == null || m_PuckStart == null) return;
            m_Puck.linearVelocity = Vector3.zero;
            m_Puck.angularVelocity = Vector3.zero;
            m_Puck.position = m_PuckStart.position;
            m_Puck.transform.position = m_PuckStart.position;
        }
    }
}

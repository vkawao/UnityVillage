using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: press a wall switch. A hand within reach counts, and so does pointing at it and
    /// pressing grip (handy in the simulator). The button pushes in, clicks, and the fans start.
    /// </summary>
    public class SwitchStep : QuestStep
    {
        [SerializeField] private XRSimpleInteractable m_Button;
        [Tooltip("World direction the button moves when pressed (into the switch plate).")]
        [SerializeField] private Vector3 m_PushDirection = Vector3.forward;
        [SerializeField] private float m_PushDepth = 0.006f;
        [SerializeField] private CeilingFan[] m_Fans;
        [Tooltip("A hand closer than this (m) to the button counts as pressing it.")]
        [SerializeField] private float m_Reach = 0.15f;

        protected override void OnBegin()
        {
            if (m_Button == null)
            {
                Debug.LogWarning("[Quest] SwitchStep has no button.", this);
                return;
            }
            m_Button.selectEntered.AddListener(OnSelect);
            m_Button.hoverEntered.AddListener(OnHover);
            SetProgress("<color=#E6E6EA>Press the orange button on the switch</color>\n" +
                        "<color=#8A8A92><i>or point at it and press grip</i></color>");
        }

        protected override void OnEnd()
        {
            if (m_Button == null) return;
            m_Button.selectEntered.RemoveListener(OnSelect);
            m_Button.hoverEntered.RemoveListener(OnHover);
        }

        private void OnSelect(SelectEnterEventArgs args) => Press();

        private void OnHover(HoverEnterEventArgs args)
        {
            // Far rays hover too; only a hand that's actually at the button counts.
            Vector3 hand = args.interactorObject.transform.position;
            foreach (Collider c in m_Button.colliders)
            {
                if (c != null && Vector3.Distance(c.ClosestPoint(hand), hand) <= m_Reach)
                {
                    Press();
                    return;
                }
            }
        }

        private void Press()
        {
            if (!IsActive) return;
            SynthAudio.PlayAt(SynthSound.Click, m_Button.transform.position, 0.9f);
            StartCoroutine(PushIn(m_Button.transform));
            if (m_Fans != null)
            {
                foreach (CeilingFan fan in m_Fans)
                    if (fan != null) fan.SetOn(true);
            }
            SetProgress("<color=#6EE787>Ahh, a nice breeze!</color>");
            Complete();
        }

        private IEnumerator PushIn(Transform button)
        {
            Vector3 from = button.position;
            Vector3 to = from + m_PushDirection.normalized * m_PushDepth;
            for (float t = 0f; t < 0.08f; t += Time.deltaTime)
            {
                button.position = Vector3.Lerp(from, to, t / 0.08f);
                yield return null;
            }
            button.position = to; // stays pressed: the fans are on
        }
    }
}

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: switch on a neon sign by touching it. A hand/controller coming within reach counts,
    /// and so does pointing at it and pressing grip (handy in the simulator).
    /// </summary>
    public class TouchSignStep : QuestStep
    {
        [SerializeField] private NeonSign m_Sign;
        [SerializeField] private XRSimpleInteractable m_Touch;
        [Tooltip("A hand closer than this (m) to the sign counts as touching it.")]
        [SerializeField] private float m_Reach = 0.25f;

        protected override void OnBegin()
        {
            if (m_Sign == null || m_Touch == null)
            {
                Debug.LogWarning("[Quest] TouchSignStep is missing its sign or interactable.", this);
                return;
            }
            if (m_Sign.IsOn)
            {
                Complete();
                return;
            }
            m_Touch.selectEntered.AddListener(OnSelect);
            m_Touch.hoverEntered.AddListener(OnHover);
            SetProgress("<color=#E6E6EA>Reach out and touch the sign</color>\n" +
                        "<color=#8A8A92><i>or point at it and press grip</i></color>");
        }

        protected override void OnEnd()
        {
            if (m_Touch == null) return;
            m_Touch.selectEntered.RemoveListener(OnSelect);
            m_Touch.hoverEntered.RemoveListener(OnHover);
        }

        private void OnSelect(SelectEnterEventArgs args) => SwitchOn();

        private void OnHover(HoverEnterEventArgs args)
        {
            // Far rays hover too; only a hand that's actually at the sign counts.
            Vector3 hand = args.interactorObject.transform.position;
            foreach (Collider c in m_Touch.colliders)
            {
                if (c != null && Vector3.Distance(c.ClosestPoint(hand), hand) <= m_Reach)
                {
                    SwitchOn();
                    return;
                }
            }
        }

        private void SwitchOn()
        {
            if (!IsActive) return;
            m_Sign.TurnOn();
            SetProgress("<color=#6EE787>We're open!</color>");
            Complete();
        }
    }
}

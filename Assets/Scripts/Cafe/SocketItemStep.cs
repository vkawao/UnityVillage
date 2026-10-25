using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRVillage
{
    /// <summary>
    /// Action: put an item into a socket (e.g. lock the portafilter into the coffee machine, or put
    /// any one of the cups under it). Completes when the socket holds one of the items; that item
    /// becomes <see cref="ChosenItem"/> for later steps. Sound comes from the socket's <see cref="SocketSound"/>.
    /// </summary>
    public class SocketItemStep : QuestStep, IQuestItemSource
    {
        [SerializeField] private XRSocketInteractor m_Socket;
        [Tooltip("Any one of these in the socket completes the step.")]
        [SerializeField] private XRGrabInteractable[] m_Items;
        [Tooltip("Hint line under the instruction.")]
        [SerializeField] private string m_Hint = "";
        [Tooltip("Shown (green) when done.")]
        [SerializeField] private string m_DoneText = "Done!";

        public XRGrabInteractable ChosenItem { get; private set; }

        public override Transform GuideTarget
        {
            get
            {
                if (m_Items == null) return base.GuideTarget;
                foreach (XRGrabInteractable item in m_Items)
                {
                    if (item != null && GrabUtil.IsHeldByHand(item) && m_Socket != null) return m_Socket.transform;
                }
                foreach (XRGrabInteractable item in m_Items)
                {
                    if (item != null && item.isActiveAndEnabled) return item.transform;
                }
                return base.GuideTarget;
            }
        }

        protected override void OnBegin()
        {
            if (m_Socket == null || m_Items == null || m_Items.Length == 0)
            {
                Debug.LogWarning("[Quest] SocketItemStep is missing its socket or items.", this);
                return;
            }
            SetProgress(string.IsNullOrEmpty(m_Hint) ? string.Empty : $"<color=#E6E6EA><i>{m_Hint}</i></color>");
        }

        private void Update()
        {
            if (!IsActive || m_Socket == null || !m_Socket.hasSelection) return;
            var held = m_Socket.interactablesSelected;
            for (int i = 0; i < held.Count; i++)
            {
                foreach (XRGrabInteractable item in m_Items)
                {
                    if (item == null || !ReferenceEquals(held[i], item)) continue;
                    ChosenItem = item;
                    SetProgress($"<color=#6EE787>{m_DoneText}</color>");
                    Complete();
                    return;
                }
            }
        }
    }
}

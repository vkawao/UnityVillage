using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRVillage
{
    /// <summary>
    /// Action: put specific guitars onto a specific rack.
    /// Completes when every required guitar is docked in one of the listed sockets
    /// at the same time. Other guitars on the rack are ignored.
    /// </summary>
    public class RackGuitarsStep : QuestStep
    {
        [Tooltip("Sockets that count as \"the rack\" for this step.")]
        [SerializeField] private XRSocketInteractor[] m_RackSockets;

        [Tooltip("Guitars that must all be on the rack.")]
        [SerializeField] private XRGrabInteractable[] m_RequiredGuitars;

        protected override void OnBegin()
        {
            if (m_RackSockets == null || m_RackSockets.Length == 0 ||
                m_RequiredGuitars == null || m_RequiredGuitars.Length == 0)
            {
                Debug.LogWarning("[Quest] RackGuitarsStep is missing sockets or guitars.", this);
                return;
            }

            // In case the guitars were already racked before this step started.
            if (AllRequiredDocked()) Complete();
        }

        private void Update()
        {
            if (IsActive && AllRequiredDocked()) Complete();
        }

        private bool AllRequiredDocked()
        {
            int total = 0, docked = 0;
            foreach (XRGrabInteractable guitar in m_RequiredGuitars)
            {
                if (guitar == null) continue;
                total++;
                if (IsDocked(guitar)) docked++;
            }
            SetProgress($"{docked}/{total} on the rack");
            return docked == total;
        }

        private bool IsDocked(XRGrabInteractable guitar)
        {
            foreach (XRSocketInteractor socket in m_RackSockets)
            {
                if (socket == null || !socket.hasSelection) continue;
                List<IXRSelectInteractable> held = socket.interactablesSelected;
                for (int i = 0; i < held.Count; i++)
                {
                    if (ReferenceEquals(held[i], guitar)) return true;
                }
            }
            return false;
        }
    }
}

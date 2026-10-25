using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// Action: "Enter the store". Completes when the storefront door opens.
    /// </summary>
    public class DoorOpenStep : QuestStep
    {
        [SerializeField] private DoorMover m_Door;

        protected override void OnBegin()
        {
            if (m_Door == null)
            {
                Debug.LogWarning("[Quest] DoorOpenStep has no DoorMover assigned.", this);
                return;
            }

            if (m_Door.IsOpen)
            {
                Complete();
                return;
            }

            m_Door.StateChanged += OnDoorStateChanged;
        }

        protected override void OnEnd()
        {
            if (m_Door != null) m_Door.StateChanged -= OnDoorStateChanged;
        }

        private void OnDoorStateChanged(bool isOpen)
        {
            if (isOpen) Complete();
        }
    }
}

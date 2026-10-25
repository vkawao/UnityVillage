using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// Action: pack laundry into a container (shirts into the basket). Counts the listed items
    /// packed in it; completes when all of them (or <c>m_Required</c>) are in.
    /// </summary>
    public class FillContainerStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string TextColor = "#E6E6EA";

        [SerializeField] private LaundryContainer m_Container;
        [SerializeField] private LaundryItem[] m_Items;
        [Tooltip("How many must be in. 0 = all of them.")]
        [SerializeField] private int m_Required;
        [Tooltip("Plural noun for the progress line, e.g. \"Shirts\".")]
        [SerializeField] private string m_ItemName = "Shirts";
        [Tooltip("Where they go, e.g. \"the basket\".")]
        [SerializeField] private string m_ContainerName = "the basket";
        [SerializeField] private string m_Hint = "Pick them up and let go over it";

        private int m_LastCount = -1;

        private int Required => m_Required > 0 ? m_Required : (m_Items != null ? m_Items.Length : 0);

        /// <summary>The next shirt to pick up; the container while you're holding one.</summary>
        public override Transform GuideTarget
        {
            get
            {
                if (m_Container == null || m_Items == null) return base.GuideTarget;
                Transform next = null;
                foreach (LaundryItem item in m_Items)
                {
                    if (item == null || m_Container.Holds(item) || !item.gameObject.activeInHierarchy) continue;
                    if (GrabUtil.IsHeldByHand(item.Grab)) return m_Container.transform;
                    if (next == null) next = item.transform;
                }
                return next != null ? next : m_Container.transform;
            }
        }

        protected override void OnBegin()
        {
            m_LastCount = -1;
            if (m_Container != null) m_Container.Accepting = true;
        }

        private void Update()
        {
            if (!IsActive || m_Container == null || m_Items == null) return;
            int count = 0;
            foreach (LaundryItem item in m_Items)
                if (m_Container.Holds(item)) count++;
            if (count == m_LastCount) return;
            m_LastCount = count;

            int need = Required;
            if (count >= need)
            {
                SetProgress($"<color={OnColor}>All {m_ItemName.ToLowerInvariant()} in {m_ContainerName}!</color>");
                Complete();
                return;
            }
            SetProgress($"<color={TextColor}>{m_ItemName} in {m_ContainerName}: <b>{count}/{need}</b></color>\n" +
                        $"<color=#8A8A92><i>{m_Hint}</i></color>");
        }
    }
}

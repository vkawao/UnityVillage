using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: choose a snack on the smart machine's screen. Completes on the press;
    /// the bag itself only drops once the payment step succeeds.
    /// </summary>
    public class SnackChoiceStep : QuestStep, IQuestItemSource
    {
        [SerializeField] private SmartMachineScreen m_Screen;
        [Tooltip("One bag per screen button, same order.")]
        [SerializeField] private XRGrabInteractable[] m_Bags;
        [SerializeField] private string[] m_Names = { "Lays Original", "Lays Sour Cream", "Lays Seaweed" };

        public XRGrabInteractable ChosenItem { get; private set; }
        public string ChosenName { get; private set; } = string.Empty;

        protected override void OnBegin()
        {
            if (m_Screen == null) return;
            var buttons = m_Screen.Buttons;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                int index = i;
                buttons[i].onClick.AddListener(() => OnPicked(index));
            }
            m_Screen.SetButtons(true, -1);
            m_Screen.SetStatus("Choose a snack", SmartMachineScreen.Neutral);
        }

        protected override void OnEnd()
        {
            if (m_Screen == null) return;
            foreach (var b in m_Screen.Buttons)
            {
                if (b != null) b.onClick.RemoveAllListeners();
            }
        }

        private void OnPicked(int index)
        {
            if (!IsActive || m_Bags == null || index >= m_Bags.Length) return;
            ChosenItem = m_Bags[index];
            ChosenName = index < m_Names.Length ? m_Names[index] : "Snack";
            SynthAudio.PlayAt(SynthSound.ButtonBeep, m_Screen.transform.position, 0.7f);
            m_Screen.SetButtons(false, index);
            m_Screen.SetStatus($"{ChosenName} selected", SmartMachineScreen.Good);
            Complete();
        }
    }
}

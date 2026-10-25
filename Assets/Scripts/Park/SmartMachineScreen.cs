using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRVillage
{
    /// <summary>
    /// The smart machine's touch screen (overlaid on the model's teal screen): three snack
    /// buttons and a status line that the snack and payment steps update.
    /// </summary>
    public class SmartMachineScreen : MonoBehaviour
    {
        [SerializeField] private Button[] m_Buttons;
        [SerializeField] private TMP_Text m_Status;
        [SerializeField, Range(0f, 1f)] private float m_DimmedAlpha = 0.35f;

        public Button[] Buttons => m_Buttons;

        public static readonly Color Neutral = new Color(0.92f, 0.95f, 0.95f);
        public static readonly Color Good = new Color(0.45f, 0.95f, 0.55f);
        public static readonly Color Bad = new Color(1f, 0.45f, 0.4f);

        private void Awake()
        {
            SetStatus("Welcome! Fresh snacks inside.", Neutral);
            SetButtons(false, -1);
        }

        public void SetStatus(string text, Color color)
        {
            if (m_Status == null) return;
            m_Status.text = text;
            m_Status.color = color;
        }

        /// <summary>Enable/disable the buttons; the chosen one (if any) stays bright.</summary>
        public void SetButtons(bool interactable, int chosen)
        {
            if (m_Buttons == null) return;
            for (int i = 0; i < m_Buttons.Length; i++)
            {
                Button b = m_Buttons[i];
                if (b == null) continue;
                b.interactable = interactable;
                bool bright = interactable || i == chosen;
                if (b.image != null)
                {
                    Color c = b.image.color;
                    c.a = bright ? 1f : m_DimmedAlpha;
                    b.image.color = c;
                }
                var label = b.GetComponentInChildren<TMP_Text>();
                if (label != null) label.alpha = bright ? 1f : 0.5f;
            }
        }
    }
}

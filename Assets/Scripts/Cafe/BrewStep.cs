using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRVillage
{
    /// <summary>
    /// Action: choose a drink on the coffee machine's screen. Needs the portafilter locked in and a cup
    /// underneath; then the pump hums, coffee streams into the cup and fills it. Completes when the
    /// shot is done. The chosen drink decides the colour once milk goes in (next step).
    /// </summary>
    public class BrewStep : QuestStep, IQuestItemSource
    {
        private const string OnColor = "#6EE787";
        private const string OffColor = "#8A8A92";
        private const string TextColor = "#E6E6EA";

        [Tooltip("One button per drink, same order as the names.")]
        [SerializeField] private Button[] m_Buttons;
        [SerializeField] private string[] m_DrinkNames = { "Espresso", "Latte", "Cappuccino" };
        [Tooltip("Colour of each drink once milk is in (same order as the names).")]
        [SerializeField] private Color[] m_MilkColors =
        {
            new Color(0.45f, 0.28f, 0.15f), new Color(0.78f, 0.6f, 0.42f), new Color(0.9f, 0.82f, 0.68f),
        };
        [SerializeField] private XRSocketInteractor m_CupSocket;
        [SerializeField] private XRSocketInteractor m_PortafilterSocket;
        [Tooltip("Under the portafilter's basket: where the coffee comes out.")]
        [SerializeField] private Transform m_Spout;
        [SerializeField] private StreamFx m_Stream;
        [SerializeField] private Color m_CoffeeColor = new Color(0.2f, 0.11f, 0.05f);
        [SerializeField] private float m_BrewTime = 3f;
        [SerializeField, Range(0f, 1f)] private float m_ShotFill = 0.55f;
        [SerializeField, Range(0f, 1f)] private float m_DimmedAlpha = 0.35f;

        private AudioSource m_Pump;
        private bool m_Brewing;
        private string m_Warning;

        public int ChosenDrink { get; private set; } = -1;
        public string DrinkName => ChosenDrink >= 0 && ChosenDrink < m_DrinkNames.Length ? m_DrinkNames[ChosenDrink] : "coffee";
        public Color MilkColor => ChosenDrink >= 0 && ChosenDrink < m_MilkColors.Length ? m_MilkColors[ChosenDrink] : m_MilkColors[1];

        /// <summary>The cup the coffee went into.</summary>
        public XRGrabInteractable ChosenItem { get; private set; }

        public override Transform GuideTarget
        {
            get
            {
                if (DockedCup() == null && m_CupSocket != null) return m_CupSocket.transform;
                return m_Buttons != null && m_Buttons.Length > 0 && m_Buttons[0] != null ? m_Buttons[0].transform.parent : base.GuideTarget;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            SetButtons(false, -1);
        }

        protected override void OnBegin()
        {
            for (int i = 0; i < m_Buttons.Length; i++)
            {
                if (m_Buttons[i] == null) continue;
                int index = i;
                m_Buttons[i].onClick.AddListener(() => OnPicked(index));
            }
            SetButtons(true, -1);
            UpdateProgress();
        }

        protected override void OnEnd()
        {
            foreach (Button b in m_Buttons)
            {
                if (b != null) b.onClick.RemoveAllListeners();
            }
        }

        private void Update()
        {
            if (IsActive && !m_Brewing) UpdateProgress();
        }

        private void OnPicked(int index)
        {
            if (!IsActive || m_Brewing) return;
            XRGrabInteractable cup = DockedCup();
            if (!PortafilterIn() || cup == null)
            {
                SynthAudio.PlayAt(SynthSound.Buzzer, m_Buttons[index].transform.position, 0.6f);
                m_Warning = !PortafilterIn() ? "Lock the portafilter in first!" : "Put a cup under the machine first!";
                UpdateProgress();
                return;
            }
            ChosenDrink = index;
            ChosenItem = cup;
            SynthAudio.PlayAt(SynthSound.ButtonBeep, m_Buttons[index].transform.position, 0.7f);
            SetButtons(false, index);
            StartCoroutine(Brew(cup.GetComponent<CupFill>()));
        }

        private IEnumerator Brew(CupFill cup)
        {
            m_Brewing = true;
            SetProgress($"<color={TextColor}>Brewing your <b>{DrinkName}</b>...</color>");
            if (m_Pump == null && m_Spout != null)
            {
                m_Pump = m_Spout.gameObject.AddComponent<AudioSource>();
                m_Pump.clip = SynthAudio.EspressoPump;
                m_Pump.loop = true;
                m_Pump.spatialBlend = 1f;
                m_Pump.minDistance = 0.8f;
                m_Pump.maxDistance = 10f;
                m_Pump.volume = 0.7f;
            }
            if (m_Pump != null) m_Pump.Play();
            if (cup != null) cup.SetColor(m_CoffeeColor);

            yield return new WaitForSeconds(0.4f); // pump builds pressure before the first drops
            for (float t = 0f; t < m_BrewTime; t += Time.deltaTime)
            {
                float k = t / m_BrewTime;
                if (cup != null)
                {
                    cup.SetFill(k * m_ShotFill);
                    if (m_Stream != null && m_Spout != null) m_Stream.Show(m_Spout.position, cup.SurfacePoint, m_CoffeeColor);
                }
                yield return null;
            }
            if (cup != null) cup.SetFill(m_ShotFill);
            if (m_Stream != null) m_Stream.Hide();
            if (m_Pump != null) m_Pump.Stop();
            SynthAudio.PlayAt(SynthSound.Ding, m_Spout != null ? m_Spout.position : transform.position, 0.6f);
            m_Brewing = false;
            SetProgress($"<color={OnColor}>Your {DrinkName} shot is ready!</color>");
            Complete();
        }

        private void UpdateProgress()
        {
            bool porta = PortafilterIn();
            bool cup = DockedCup() != null;
            string hint = m_Warning != null && (!porta || !cup)
                ? $"<color=#FF8A80>{m_Warning}</color>"
                : $"<color={TextColor}><i>Press a drink on the white screen of the machine</i></color>";
            SetProgress(
                $"<color={(porta ? OnColor : OffColor)}><b>PORTAFILTER</b> {(porta ? "IN" : "MISSING")}</color>" +
                $"<space=1.2em><color={(cup ? OnColor : OffColor)}><b>CUP</b> {(cup ? "READY" : "MISSING")}</color>\n" + hint);
        }

        private bool PortafilterIn() => m_PortafilterSocket == null || m_PortafilterSocket.hasSelection;

        private XRGrabInteractable DockedCup()
        {
            if (m_CupSocket == null || !m_CupSocket.hasSelection) return null;
            return m_CupSocket.interactablesSelected[0] as XRGrabInteractable;
        }

        /// <summary>Enable/disable the buttons; the chosen one stays bright, the rest dim.</summary>
        private void SetButtons(bool interactable, int chosen)
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

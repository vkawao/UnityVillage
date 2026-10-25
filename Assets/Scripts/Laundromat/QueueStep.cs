using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: book a washer and a dryer at the queue machine. Pick one of each on its screen
    /// (you can change your mind), then put a coin in its grey coin slot. The number tags on the
    /// chosen machines turn green; later steps read <see cref="ChosenWasher"/> / <see cref="ChosenDryer"/>.
    /// </summary>
    public class QueueStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string OffColor = "#8A8A92";
        private const string TextColor = "#E6E6EA";
        private static readonly Color s_Picked = new Color(0.3f, 0.85f, 0.45f);

        [Header("Screen")]
        [SerializeField] private Button[] m_WasherButtons;
        [SerializeField] private Button[] m_DryerButtons;
        [SerializeField] private TMP_Text m_Status;
        [SerializeField] private Transform m_Screen;
        [SerializeField, Range(0f, 1f)] private float m_DimmedAlpha = 0.45f;

        [Header("Number tags on the machines (same order as the buttons)")]
        [SerializeField] private Image[] m_WasherTags;
        [SerializeField] private Image[] m_DryerTags;

        [Header("Machines (same order as the buttons)")]
        [SerializeField] private LaundryMachine[] m_Washers;
        [SerializeField] private LaundryMachine[] m_Dryers;

        [Header("Coin")]
        [SerializeField] private XRGrabInteractable m_Coin;
        [SerializeField] private ItemTrigger m_CoinSlot;

        private Color[] m_WasherColors, m_DryerColors, m_WasherTagColors, m_DryerTagColors;
        private string m_Warning;
        private float m_WarnUntil;
        private bool m_Paid;

        /// <summary>Index of the booked washer / dryer (-1 until booked).</summary>
        public int ChosenWasher { get; private set; } = -1;
        public int ChosenDryer { get; private set; } = -1;

        private bool BothChosen => ChosenWasher >= 0 && ChosenDryer >= 0;

        /// <summary>The booked washer or dryer (number 1 until booked, e.g. when testing a later step on its own).</summary>
        public LaundryMachine Machine(bool dryer)
        {
            LaundryMachine[] list = dryer ? m_Dryers : m_Washers;
            if (list == null || list.Length == 0) return null;
            int i = dryer ? ChosenDryer : ChosenWasher;
            return list[Mathf.Clamp(i, 0, list.Length - 1)];
        }

        /// <summary>The screen until both are chosen, then the coins, then the slot once you hold them.</summary>
        public override Transform GuideTarget
        {
            get
            {
                if (!BothChosen || m_Coin == null || m_CoinSlot == null) return m_Screen != null ? m_Screen : base.GuideTarget;
                if (GrabUtil.IsHeldByHand(m_Coin) || !m_Coin.gameObject.activeInHierarchy) return m_CoinSlot.transform;
                return m_Coin.transform;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            m_WasherColors = ButtonColors(m_WasherButtons);
            m_DryerColors = ButtonColors(m_DryerButtons);
            m_WasherTagColors = TagColors(m_WasherTags);
            m_DryerTagColors = TagColors(m_DryerTags);
            SetStatus("Welcome! Book a washer and a dryer here.", TextColor);
            Paint(m_WasherButtons, m_WasherColors, false, -1);
            Paint(m_DryerButtons, m_DryerColors, false, -1);
        }

        protected override void OnBegin()
        {
            ChosenWasher = ChosenDryer = -1;
            m_Paid = false;
            Listen(m_WasherButtons, i => { ChosenWasher = i; OnPicked(); });
            Listen(m_DryerButtons, i => { ChosenDryer = i; OnPicked(); });
            Paint(m_WasherButtons, m_WasherColors, true, -1);
            Paint(m_DryerButtons, m_DryerColors, true, -1);
            SetStatus("Choose a washer and a dryer", TextColor);
            UpdateProgress();
        }

        protected override void OnEnd()
        {
            Unlisten(m_WasherButtons);
            Unlisten(m_DryerButtons);
        }

        private void Update()
        {
            if (!IsActive || m_Paid) return;
            if (m_Warning != null && Time.time > m_WarnUntil)
            {
                m_Warning = null;
                UpdateProgress();
            }
            if (m_Coin == null || m_CoinSlot == null || !m_CoinSlot.Contains(m_Coin.GetComponent<Rigidbody>())) return;
            if (!BothChosen)
            {
                if (m_Warning == null) SynthAudio.PlayAt(SynthSound.Buzzer, m_CoinSlot.transform.position, 0.5f);
                Warn(ChosenWasher < 0 ? "Choose a washer on the screen first!" : "Choose a dryer on the screen first!");
                return;
            }
            StartCoroutine(Pay());
        }

        private void OnPicked()
        {
            if (!IsActive || m_Paid) return;
            SynthAudio.PlayAt(SynthSound.ButtonBeep, m_Screen != null ? m_Screen.position : transform.position, 0.7f);
            Paint(m_WasherButtons, m_WasherColors, true, ChosenWasher);
            Paint(m_DryerButtons, m_DryerColors, true, ChosenDryer);
            HighlightTags(false);
            SetStatus(BothChosen ? $"Washer {ChosenWasher + 1} + Dryer {ChosenDryer + 1}: insert a coin below"
                                 : ChosenWasher < 0 ? "Now choose a washer" : "Now choose a dryer", TextColor);
            m_Warning = null;
            UpdateProgress();
        }

        private IEnumerator Pay()
        {
            m_Paid = true;
            m_Coin.gameObject.SetActive(false);
            SynthAudio.PlayAt(SynthSound.CoinDrop, m_CoinSlot.transform.position, 0.9f);
            Paint(m_WasherButtons, m_WasherColors, false, ChosenWasher);
            Paint(m_DryerButtons, m_DryerColors, false, ChosenDryer);
            SetProgress($"<color={TextColor}>Booking...</color>");
            yield return new WaitForSeconds(0.6f);
            SynthAudio.PlayAt(SynthSound.QueueConfirm, m_Screen != null ? m_Screen.position : transform.position, 0.8f);
            SetStatus($"Booked! Washer {ChosenWasher + 1} and Dryer {ChosenDryer + 1} are yours.", OnColor);
            HighlightTags(true);
            SetProgress($"<color={OnColor}>Washer {ChosenWasher + 1} and Dryer {ChosenDryer + 1} are booked for you!</color>");
            Complete();
        }

        private void UpdateProgress()
        {
            bool w = ChosenWasher >= 0, d = ChosenDryer >= 0;
            string hint = m_Warning != null
                ? $"<color=#FF8A80>{m_Warning}</color>"
                : BothChosen
                    ? $"<color={TextColor}><i>Put a coin in the grey slot under the screen</i></color>"
                    : $"<color={TextColor}><i>Press a number on the queue machine's screen</i></color>";
            SetProgress(
                $"<color={(w ? OnColor : OffColor)}><b>WASHER</b> {(w ? (ChosenWasher + 1).ToString() : "-")}</color>" +
                $"<space=1.2em><color={(d ? OnColor : OffColor)}><b>DRYER</b> {(d ? (ChosenDryer + 1).ToString() : "-")}</color>" +
                $"<space=1.2em><color={OffColor}><b>COIN</b> -</color>\n" + hint);
        }

        private void Warn(string text)
        {
            m_Warning = text;
            m_WarnUntil = Time.time + 2.5f;
            UpdateProgress();
        }

        private void HighlightTags(bool booked)
        {
            Tint(m_WasherTags, m_WasherTagColors, ChosenWasher, booked);
            Tint(m_DryerTags, m_DryerTagColors, ChosenDryer, booked);
        }

        private static void Tint(Image[] tags, Color[] normal, int chosen, bool booked)
        {
            if (tags == null) return;
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i] == null) continue;
                bool on = i == chosen;
                tags[i].color = on && booked ? s_Picked : normal[i];
                tags[i].transform.localScale = Vector3.one * (on ? 1.25f : 1f);
            }
        }

        private void SetStatus(string text, string color)
        {
            if (m_Status == null) return;
            m_Status.text = $"<color={color}>{text}</color>";
        }

        private delegate void Picked(int index);

        private static void Listen(Button[] buttons, Picked onPicked)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                int index = i;
                buttons[i].onClick.AddListener(() => onPicked(index));
            }
        }

        private static void Unlisten(Button[] buttons)
        {
            if (buttons == null) return;
            foreach (Button b in buttons)
                if (b != null) b.onClick.RemoveAllListeners();
        }

        /// <summary>Enable/disable; the chosen one turns green, the rest dim once something is chosen.</summary>
        private void Paint(Button[] buttons, Color[] colors, bool interactable, int chosen)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                Button b = buttons[i];
                if (b == null) continue;
                b.interactable = interactable;
                bool bright = chosen < 0 ? interactable : i == chosen;
                if (b.image != null)
                {
                    Color c = i == chosen ? s_Picked : colors[i];
                    c.a = bright ? 1f : m_DimmedAlpha;
                    b.image.color = c;
                }
                var label = b.GetComponentInChildren<TMP_Text>();
                if (label != null) label.alpha = bright ? 1f : 0.6f;
            }
        }

        private static Color[] ButtonColors(Button[] buttons)
        {
            var colors = new Color[buttons != null ? buttons.Length : 0];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = buttons[i] != null && buttons[i].image != null ? buttons[i].image.color : Color.white;
            return colors;
        }

        private static Color[] TagColors(Image[] tags)
        {
            var colors = new Color[tags != null ? tags.Length : 0];
            for (int i = 0; i < colors.Length; i++) colors[i] = tags[i] != null ? tags[i].color : Color.white;
            return colors;
        }
    }
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Base for the arcade's games. Every game starts the same way: tap your arcade card on the
    /// machine's grey card reader. The card then pops back into your hip pocket (hands free to
    /// play), the screen counts in, and the game itself runs (subclass). Its screen shows the
    /// score live; <see cref="Win"/> finishes the action.
    /// </summary>
    public abstract class ArcadeGameStep : QuestStep
    {
        protected const string OnColor = "#6EE787";
        protected const string TextColor = "#E6E6EA";
        protected const string HintColor = "#8A8A92";
        protected const string ScoreColor = "#FFD34D";

        [Header("Card reader")]
        [SerializeField] private HipHolster m_Holster;
        [SerializeField] private XRGrabInteractable m_Card;
        [SerializeField] private ItemTrigger m_Reader;
        [Tooltip("Where the reader is, in words, for the panel (e.g. \"lower right of the front\").")]
        [SerializeField] private string m_ReaderWhere = "on the front";

        [Header("Machine")]
        [SerializeField] private string m_GameTitle = "GAME";
        [Tooltip("Score screen over the machine's own screen.")]
        [SerializeField] private TMP_Text m_Screen;
        [Tooltip("Where game sounds play from.")]
        [SerializeField] private Transform m_Machine;

        private enum Phase { WaitingForCard, Starting, Playing, Won }
        private Phase m_Phase;

        protected bool Playing => IsActive && m_Phase == Phase.Playing;
        protected Transform Machine => m_Machine != null ? m_Machine : transform;
        protected string GameTitle => m_GameTitle;

        public override Transform GuideTarget
        {
            get
            {
                if (m_Phase == Phase.Playing)
                {
                    Transform t = GameGuideTarget;
                    if (t != null) return t;
                }
                if (m_Phase != Phase.WaitingForCard || m_Card == null) return Machine;
                if (m_Holster != null && m_Holster.IsHolding(m_Card)) return m_Holster.transform;
                if (GrabUtil.IsHeldByHand(m_Card)) return m_Reader != null ? m_Reader.transform : Machine;
                return m_Card.transform;
            }
        }

        /// <summary>What the guide arrow points at while playing (e.g. the lit button). Null = the machine.</summary>
        protected virtual Transform GameGuideTarget => null;

        /// <summary>Instruction line while playing (shown under the panel text).</summary>
        protected abstract string PlayingHint { get; }

        protected override void Awake()
        {
            base.Awake();
            SetScreen($"<b>{m_GameTitle}</b>\n<size=55%>Tap card to play</size>");
        }

        protected override void OnBegin()
        {
            m_Phase = Phase.WaitingForCard;
            if (m_Holster != null && m_Card != null && m_Holster.IsHolding(m_Card)) m_Holster.Highlight = true;
            SetScreen($"<b>{m_GameTitle}</b>\n<size=55%><color={ScoreColor}>Tap card to play</color></size>");
            ShowCardHint();
        }

        protected override void OnEnd()
        {
            if (m_Holster != null) m_Holster.Highlight = false;
        }

        private void ShowCardHint()
        {
            bool inPocket = m_Holster != null && m_Card != null && m_Holster.IsHolding(m_Card);
            string where = inPocket ? "Take your card from your <b>hip pocket</b>, then tap" : "Tap your card";
            SetProgress($"<color={TextColor}>{where} it on the <b>grey card reader</b> {m_ReaderWhere}</color>");
        }

        protected virtual void Update()
        {
            if (!IsActive) return;
            if (m_Phase == Phase.Playing)
            {
                UpdateGame();
                return;
            }
            if (m_Phase != Phase.WaitingForCard || m_Card == null) return;

            bool held = GrabUtil.IsHeldByHand(m_Card);
            if (m_Holster != null && m_Holster.Highlight && held)
            {
                m_Holster.Highlight = false;
                ShowCardHint();
            }
            // Only a card in your hand counts (a card lying near the reader doesn't start the game).
            if (held && m_Reader != null && m_Reader.Contains(m_Card.GetComponent<Rigidbody>()))
                StartCoroutine(CardTapped());
        }

        private IEnumerator CardTapped()
        {
            m_Phase = Phase.Starting;
            SynthAudio.PlayAt(SynthSound.CardApproved, m_Reader.transform.position, 0.9f);
            SetScreen($"<b>{m_GameTitle}</b>\n<size=60%><color={OnColor}>CARD OK!</color></size>");
            SetProgress($"<color={OnColor}>Card accepted!</color> <color={TextColor}>It's back in your hip pocket. Get ready...</color>");

            // Back into the pocket, so both hands are free to play.
            if (m_Card.interactionManager != null) m_Card.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Card);
            if (m_Holster != null) m_Holster.Store(m_Card);

            yield return new WaitForSeconds(0.6f);
            SynthAudio.PlayAt(SynthSound.ArcadeStart, Machine.position, 0.7f);
            SetScreen("<b>READY...</b>");
            yield return new WaitForSeconds(0.5f);
            SetScreen($"<b><color={OnColor}>GO!</color></b>");
            yield return new WaitForSeconds(0.4f);
            m_Phase = Phase.Playing;
            SetProgress($"<color={TextColor}>{PlayingHint}</color>");
            OnGameStart();
        }

        /// <summary>The game begins (after the card tap and the count-in).</summary>
        protected abstract void OnGameStart();

        /// <summary>Called every frame while playing.</summary>
        protected abstract void UpdateGame();

        /// <summary>The game's done: win jingle, "WIN!" on the screen, then the action completes.</summary>
        protected void Win(string screenText, string progress)
        {
            if (m_Phase != Phase.Playing) return;
            m_Phase = Phase.Won;
            SynthAudio.PlayAt(SynthSound.ArcadeWin, Machine.position, 0.8f);
            SetScreen(screenText);
            SetProgress(progress);
            Complete();
        }

        protected void SetScreen(string text)
        {
            if (m_Screen != null) m_Screen.text = text;
        }

        /// <summary>"SCORE 3/8" style screen text under the game title.</summary>
        protected void ShowScore(string label, int value, int target)
        {
            SetScreen($"<size=55%>{m_GameTitle}</size>\n<b>{label} <color={ScoreColor}>{value}</color>/{target}</b>");
        }
    }
}

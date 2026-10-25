using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRVillage
{
    /// <summary>
    /// Action: take the coffee out of the machine and pour milk into it. Hold the milk pitcher over the
    /// cup and tip it: milk streams from the rim, the cup fills up and turns the colour of the chosen
    /// drink. Completes after pouring for a couple of seconds.
    /// </summary>
    public class PourMilkStep : QuestStep
    {
        private const string OnColor = "#6EE787";
        private const string OffColor = "#8A8A92";
        private const string TextColor = "#E6E6EA";

        [Tooltip("The step that brewed the coffee (gives the cup and the drink).")]
        [SerializeField] private BrewStep m_Brew;
        [SerializeField] private XRSocketInteractor m_CupSocket;
        [SerializeField] private XRGrabInteractable m_Pitcher;
        [Tooltip("Centre of the pitcher's rim; its up axis is the pitcher's up.")]
        [SerializeField] private Transform m_Rim;
        [SerializeField] private float m_RimRadius = 0.05f;
        [SerializeField] private StreamFx m_Stream;
        [SerializeField] private Color m_MilkColor = new Color(0.97f, 0.96f, 0.92f);
        [Tooltip("Tip the pitcher at least this far (degrees from upright) to pour.")]
        [SerializeField] private float m_PourAngle = 50f;
        [Tooltip("The rim must be this close to above the cup (m, sideways).")]
        [SerializeField] private float m_Reach = 0.12f;
        [SerializeField] private float m_PourTime = 2.5f;
        [SerializeField, Range(0f, 1f)] private float m_FullFill = 0.9f;

        private AudioSource m_PourSound;
        private float m_Poured;
        private float m_StartFill;
        private Color m_StartColor;
        private int m_LastState = -1;

        private CupFill Cup => m_Brew != null && m_Brew.ChosenItem != null ? m_Brew.ChosenItem.GetComponent<CupFill>() : null;

        private bool CupInMachine =>
            m_CupSocket != null && m_CupSocket.hasSelection && m_Brew != null && m_Brew.ChosenItem != null &&
            ReferenceEquals(m_CupSocket.interactablesSelected[0], m_Brew.ChosenItem);

        public override Transform GuideTarget
        {
            get
            {
                CupFill cup = Cup;
                if (cup == null) return base.GuideTarget;
                if (CupInMachine) return cup.transform;
                if (m_Pitcher != null && !GrabUtil.IsHeldByHand(m_Pitcher)) return m_Pitcher.transform;
                return cup.transform;
            }
        }

        protected override void OnBegin()
        {
            CupFill cup = Cup;
            if (cup == null || m_Pitcher == null || m_Rim == null)
            {
                Debug.LogWarning("[Quest] PourMilkStep is missing the brewed cup, the pitcher or its rim.", this);
                return;
            }
            m_Poured = 0f;
            m_StartFill = cup.Fill;
            m_StartColor = cup.Color;
            m_PourSound = m_Rim.gameObject.GetComponent<AudioSource>();
            if (m_PourSound == null)
            {
                m_PourSound = m_Rim.gameObject.AddComponent<AudioSource>();
                m_PourSound.clip = SynthAudio.MilkPour;
                m_PourSound.loop = true;
                m_PourSound.spatialBlend = 1f;
                m_PourSound.minDistance = 0.5f;
                m_PourSound.maxDistance = 8f;
            }
            m_LastState = -1;
        }

        protected override void OnEnd()
        {
            if (m_Stream != null) m_Stream.Hide();
            if (m_PourSound != null) m_PourSound.Stop();
        }

        private void Update()
        {
            if (!IsActive) return;
            CupFill cup = Cup;
            if (cup == null || m_Pitcher == null || m_Rim == null) return;

            bool held = GrabUtil.IsHeldByHand(m_Pitcher);
            bool cupOut = !CupInMachine;
            float tilt = Vector3.Angle(m_Rim.up, Vector3.up);
            Vector3 lip = m_Rim.position + FlatDir(m_Rim.up) * m_RimRadius; // milk leaves from the low side of the rim
            Vector3 target = cup.SurfacePoint;
            Vector3 toCup = target - lip;
            float sideways = new Vector2(toCup.x, toCup.z).magnitude;
            bool overCup = sideways <= m_Reach && lip.y > target.y - 0.01f && lip.y - target.y < 0.4f;
            bool pouring = held && cupOut && tilt >= m_PourAngle && overCup;

            if (pouring)
            {
                m_Poured += Time.deltaTime;
                float k = Mathf.Clamp01(m_Poured / m_PourTime);
                cup.SetFill(Mathf.Lerp(m_StartFill, m_FullFill, k));
                cup.SetColor(Color.Lerp(m_StartColor, m_Brew.MilkColor, k));
                if (m_Stream != null) m_Stream.Show(lip, cup.SurfacePoint, m_MilkColor);
                if (!m_PourSound.isPlaying) m_PourSound.Play();
                if (k >= 1f)
                {
                    SynthAudio.PlayAt(SynthSound.Ding, target, 0.6f);
                    SetProgress($"<color={OnColor}>A perfect {m_Brew.DrinkName}!</color>");
                    Complete();
                    return;
                }
            }
            else
            {
                if (m_Stream != null) m_Stream.Hide();
                if (m_PourSound.isPlaying) m_PourSound.Stop();
            }

            int state = !cupOut ? 0 : !held ? 1 : !overCup ? 2 : tilt < m_PourAngle ? 3 : 4;
            int pct = Mathf.RoundToInt(Mathf.Clamp01(m_Poured / m_PourTime) * 100f);
            if (state == m_LastState && state != 4) return;
            m_LastState = state;
            string hint = state switch
            {
                0 => "Take the cup out from under the machine",
                1 => "Grab the milk pitcher",
                2 => "Hold the pitcher just above the cup",
                3 => "Now tip it to pour!",
                _ => "Pouring...",
            };
            SetProgress(
                $"<color={(cupOut ? OnColor : OffColor)}><b>CUP</b> {(cupOut ? "OUT" : "IN MACHINE")}</color>" +
                $"<space=1.2em><color={(pct > 0 ? OnColor : OffColor)}><b>MILK</b> {pct}%</color>\n" +
                $"<color={TextColor}><i>{hint}</i></color>");
        }

        private static Vector3 FlatDir(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
        }
    }
}

using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// Action: the arcade cabinet. A little game on its screen: steer the ship along the bottom with
    /// the joystick and catch the falling stars. Missed stars just come again. Catch 5 to win.
    /// </summary>
    public class CabinetGameStep : ArcadeGameStep
    {
        [Header("Cabinet game")]
        [SerializeField] private JoystickLever m_Joystick;
        [Tooltip("The play field on the screen (ship and star live in it, anchored at its centre).")]
        [SerializeField] private RectTransform m_PlayArea;
        [SerializeField] private RectTransform m_Ship;
        [SerializeField] private RectTransform m_Star;
        [SerializeField] private int m_CatchesToWin = 5;
        [Tooltip("Seconds for a star to fall the whole screen.")]
        [SerializeField] private float m_FallTime = 2.6f;
        [Tooltip("How quickly the ship follows the stick (higher = snappier).")]
        [SerializeField] private float m_ShipFollow = 12f;

        private int m_Catches;
        private float m_ShipX;

        protected override string PlayingHint =>
            "Grab the <b>red joystick</b> and move it left and right to catch the falling stars!\n" +
            $"<color={HintColor}><i>Hold grip on it and move your hand</i></color>";

        protected override Transform GameGuideTarget => m_Joystick != null ? m_Joystick.transform : null;

        protected override void Awake()
        {
            base.Awake();
            if (m_PlayArea != null) m_PlayArea.gameObject.SetActive(false);
        }

        protected override void OnGameStart()
        {
            m_Catches = 0;
            m_ShipX = 0f;
            if (m_PlayArea != null) m_PlayArea.gameObject.SetActive(true);
            ShowScore("STARS", 0, m_CatchesToWin);
            NewStar();
        }

        protected override void UpdateGame()
        {
            if (m_PlayArea == null || m_Ship == null || m_Star == null) return;
            Vector2 area = m_PlayArea.rect.size;
            float halfRange = area.x * 0.5f - m_Ship.rect.width * 0.5f;

            // Ship follows the stick: full tilt = the edge of the screen.
            float target = (m_Joystick != null ? m_Joystick.Value : 0f) * halfRange;
            m_ShipX = Mathf.Lerp(m_ShipX, target, 1f - Mathf.Exp(-m_ShipFollow * Time.deltaTime));
            m_Ship.anchoredPosition = new Vector2(m_ShipX, -area.y * 0.5f + m_Ship.rect.height * 0.5f + area.y * 0.04f);

            Vector2 star = m_Star.anchoredPosition;
            star.y -= area.y / Mathf.Max(m_FallTime, 0.2f) * Time.deltaTime;
            m_Star.anchoredPosition = star;

            float shipTop = m_Ship.anchoredPosition.y + m_Ship.rect.height * 0.5f;
            float starBottom = star.y - m_Star.rect.height * 0.5f;
            if (starBottom <= shipTop && star.y >= m_Ship.anchoredPosition.y - m_Ship.rect.height * 0.5f)
            {
                if (Mathf.Abs(star.x - m_ShipX) <= (m_Ship.rect.width + m_Star.rect.width) * 0.5f)
                {
                    Catch();
                    return;
                }
            }
            if (star.y < -area.y * 0.5f - m_Star.rect.height)
            {
                SynthAudio.PlayAt(SynthSound.ButtonBeep, Machine.position, 0.3f); // missed: just another one
                NewStar();
            }
        }

        private void Catch()
        {
            m_Catches++;
            SynthAudio.PlayAt(SynthSound.StarBlip, Machine.position, 0.8f);
            ShowScore("STARS", m_Catches, m_CatchesToWin);
            if (m_Catches >= m_CatchesToWin)
            {
                if (m_PlayArea != null) m_PlayArea.gameObject.SetActive(false);
                Win($"<size=55%>{GameTitle}</size>\n<b><color={OnColor}>YOU WIN!</color></b>\n<size=55%>{m_Catches} STARS</size>",
                    $"<color={OnColor}>Great flying! You caught {m_Catches} stars!</color>");
                return;
            }
            NewStar();
        }

        private void NewStar()
        {
            if (m_PlayArea == null || m_Star == null) return;
            Vector2 area = m_PlayArea.rect.size;
            float range = area.x * 0.5f - m_Star.rect.width;
            m_Star.anchoredPosition = new Vector2(Random.Range(-range, range), area.y * 0.5f + m_Star.rect.height * 0.5f);
        }
    }
}

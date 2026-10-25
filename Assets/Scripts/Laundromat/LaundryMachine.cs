using UnityEngine;

namespace VRVillage
{
    /// <summary>
    /// One washer or dryer: its door, its drum (spun by script while running, always stopping on a
    /// full turn so the clothes inside end up lying flat again), the laundry container inside it
    /// (washers) and the running sound.
    /// </summary>
    public class LaundryMachine : MonoBehaviour
    {
        [SerializeField] private MachineDoor m_Door;
        [Tooltip("Optional: spun while running.")]
        [SerializeField] private Transform m_Drum;
        [Tooltip("World axis the drum spins about (front to back).")]
        [SerializeField] private Vector3 m_SpinAxis = Vector3.forward;
        [Tooltip("Optional: clothes packed inside the drum (washers).")]
        [SerializeField] private LaundryContainer m_Load;
        [Tooltip("Where clothes go in: in front of the drum's mouth.")]
        [SerializeField] private ItemTrigger m_Opening;
        [SerializeField] private SynthSound m_RunSound = SynthSound.WashLoop;
        [SerializeField, Range(0f, 1f)] private float m_RunVolume = 0.6f;
        [SerializeField] private float m_SpinSpeed = 300f;

        private float m_Speed;
        private float m_Angle;
        private AudioSource m_Audio;

        public MachineDoor Door => m_Door;
        public LaundryContainer Load => m_Load;
        public ItemTrigger Opening => m_Opening;
        public bool IsRunning { get; private set; }
        /// <summary>True once the drum has come to rest after running.</summary>
        public bool IsStill => !IsRunning && m_Speed <= 0f;

        public void SetDoor(bool open)
        {
            if (m_Door != null) m_Door.SetOpen(open);
        }

        public void Run(bool on)
        {
            IsRunning = on;
            if (!on) return;
            if (m_Audio == null)
            {
                m_Audio = gameObject.AddComponent<AudioSource>();
                m_Audio.clip = SynthAudio.Get(m_RunSound);
                m_Audio.loop = true;
                m_Audio.spatialBlend = 1f;
                m_Audio.minDistance = 1f;
                m_Audio.maxDistance = 12f;
            }
            m_Audio.volume = m_RunVolume;
            if (!m_Audio.isPlaying) m_Audio.Play();
        }

        private void Update()
        {
            if (m_Audio != null && !IsRunning && m_Audio.isPlaying)
            {
                m_Audio.volume = Mathf.MoveTowards(m_Audio.volume, 0f, Time.deltaTime * 0.8f);
                if (m_Audio.volume <= 0f) m_Audio.Stop();
            }
            if (m_Drum == null) return;

            if (IsRunning)
            {
                m_Speed = Mathf.MoveTowards(m_Speed, m_SpinSpeed, m_SpinSpeed * 0.6f * Time.deltaTime);
            }
            else if (m_Speed > 0f)
            {
                // Coast to a stop exactly on the next full turn.
                float left = Mathf.Repeat(-m_Angle, 360f);
                m_Speed = Mathf.Max(Mathf.Min(m_Speed, left * 2f + 20f), 0f);
                if (left < m_Speed * Time.deltaTime || left < 0.5f)
                {
                    Spin(left);
                    m_Angle = 0f;
                    m_Speed = 0f;
                    return;
                }
            }
            if (m_Speed > 0f) Spin(m_Speed * Time.deltaTime);
        }

        private void Spin(float degrees)
        {
            m_Angle = Mathf.Repeat(m_Angle + degrees, 360f);
            m_Drum.Rotate(m_SpinAxis, degrees, Space.World);
        }
    }
}

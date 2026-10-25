using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// A crank you grab and turn in circles (e.g. a hand coffee grinder). While held, the crank
    /// follows the hand's angle around its shaft (the object's pivot, turning about world up).
    /// Pointing with a ray works too: the grab point sits where the ray hit and moves with it.
    /// Keeps a running total of how far it has been turned, and plays a loop while it moves.
    /// </summary>
    public class CrankHandle : XRSimpleInteractable
    {
        [Tooltip("The part that spins. Empty = this object. Its pivot must be on the shaft.")]
        [SerializeField] private Transform m_Crank;
        [Tooltip("Loop played while turning; louder the faster it turns.")]
        [SerializeField] private SynthSound m_LoopSound = SynthSound.None;
        [Tooltip("Turning speed (degrees/s) that plays the loop at full volume.")]
        [SerializeField] private float m_FullVolumeSpeed = 360f;

        private Quaternion m_RestRotation;
        private float m_Angle;          // current crank angle (degrees, unwrapped)
        private float m_LastHandAngle;
        private bool m_HasHandAngle;
        private float m_Speed;
        private AudioSource m_Loop;

        /// <summary>Total degrees turned since the last reset, counting both directions as progress.</summary>
        public float DegreesTurned { get; private set; }

        /// <summary>Raised every frame the crank moves, with the degrees moved (always positive).</summary>
        public event System.Action<float> Turned;

        public void ResetCount() => DegreesTurned = 0f;

        protected override void Awake()
        {
            base.Awake();
            if (m_Crank == null) m_Crank = transform;
            m_RestRotation = m_Crank.rotation;

            AudioClip clip = SynthAudio.Get(m_LoopSound);
            if (clip != null)
            {
                m_Loop = gameObject.AddComponent<AudioSource>();
                m_Loop.clip = clip;
                m_Loop.loop = true;
                m_Loop.playOnAwake = false;
                m_Loop.spatialBlend = 1f;
                m_Loop.minDistance = 0.6f;
                m_Loop.maxDistance = 10f;
                m_Loop.volume = 0f;
            }
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;

            float moved = 0f;
            if (isSelected && TryGetHandAngle(out float handAngle))
            {
                if (m_HasHandAngle)
                {
                    float delta = Mathf.DeltaAngle(m_LastHandAngle, handAngle);
                    m_Angle += delta;
                    moved = Mathf.Abs(delta);
                    m_Crank.rotation = Quaternion.AngleAxis(m_Angle, Vector3.up) * m_RestRotation;
                }
                m_LastHandAngle = handAngle;
                m_HasHandAngle = true;
            }
            else
            {
                m_HasHandAngle = false;
            }

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            m_Speed = Mathf.Lerp(m_Speed, moved / dt, 1f - Mathf.Exp(-dt * 8f));
            if (moved > 0f)
            {
                DegreesTurned += moved;
                Turned?.Invoke(moved);
            }
            UpdateLoop();
        }

        /// <summary>Angle (degrees) of the grab point around the shaft, seen from above.</summary>
        private bool TryGetHandAngle(out float angle)
        {
            angle = 0f;
            Transform attach = firstInteractorSelecting?.GetAttachTransform(this);
            if (attach == null) return false;
            Vector3 offset = attach.position - m_Crank.position;
            offset.y = 0f;
            if (offset.magnitude < 0.015f) return false; // right on the shaft: direction is meaningless
            angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            return true;
        }

        private void UpdateLoop()
        {
            if (m_Loop == null) return;
            float target = Mathf.Clamp01(m_Speed / m_FullVolumeSpeed);
            m_Loop.volume = target;
            m_Loop.pitch = Mathf.Lerp(0.85f, 1.1f, target);
            if (target > 0.02f && !m_Loop.isPlaying) m_Loop.Play();
            else if (target <= 0.02f && m_Loop.isPlaying) m_Loop.Stop();
        }
    }
}

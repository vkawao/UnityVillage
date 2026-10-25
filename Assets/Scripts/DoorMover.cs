using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Smoothly rotates the storefront door on its hinge when interacted with.
    /// Fulfills Action 1: "Open Storefront Door - Item moved by script".
    /// </summary>
    [SelectionBase]
    public class DoorMover : MonoBehaviour
    {
        [Header("Hinge & Motion Settings")]
        [Tooltip("The transform to rotate. If null, uses this gameObject's transform.")]
        [SerializeField] private Transform m_HingeTransform;

        [Tooltip("Euler angles when the door is fully closed.")]
        [SerializeField] private Vector3 m_ClosedRotation = Vector3.zero;

        [Tooltip("Euler angles when the door is fully open.")]
        [SerializeField] private Vector3 m_OpenRotation = new Vector3(0f, -90f, 0f);

        [Tooltip("Time in seconds to complete the swing.")]
        [SerializeField] private float m_SwingDuration = 0.85f;

        [Tooltip("Easing curve for natural door momentum.")]
        [SerializeField] private AnimationCurve m_MotionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("State")]
        [SerializeField] private bool m_IsOpen = false;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioSource m_AudioSource;
        [SerializeField] private AudioClip m_OpenSound;
        [SerializeField] private AudioClip m_CloseSound;
        [Tooltip("Use synthesized sounds (latch + creak + shop bell / thud) when no clip is assigned. " +
                 "Off by default so other houses' doors stay silent unless they opt in.")]
        [SerializeField] private bool m_UseBuiltInSounds = false;
        [SerializeField, Range(0f, 1f)] private float m_Volume = 0.9f;

        private Coroutine m_SwingRoutine;
        private XRSimpleInteractable m_Interactable;

        public bool IsOpen => m_IsOpen;

        /// <summary>Raised whenever the door is told to open (true) or close (false).</summary>
        public event Action<bool> StateChanged;

        private void Awake()
        {
            if (m_HingeTransform == null)
            {
                m_HingeTransform = transform;
            }

            if (m_AudioSource == null && (m_UseBuiltInSounds || m_OpenSound != null || m_CloseSound != null))
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
                m_AudioSource.playOnAwake = false;
                m_AudioSource.spatialBlend = 1f;
                m_AudioSource.minDistance = 1.5f;
                m_AudioSource.maxDistance = 25f;
            }

            // Search this GameObject first, then the whole child hierarchy.
            // This handles the case where the interactable lives on the Door Handle
            // child while DoorMover lives on the parent Door_Hinge pivot.
            m_Interactable = GetComponent<XRSimpleInteractable>();
            if (m_Interactable == null)
                m_Interactable = GetComponentInChildren<XRSimpleInteractable>(includeInactive: true);

            if (m_Interactable != null)
            {
                m_Interactable.selectEntered.AddListener(OnSelectEntered);
            }
            else
            {
                Debug.LogWarning($"[DoorMover] No XRSimpleInteractable found on '{gameObject.name}' or its children. " +
                                 "Attach one to the door handle for VR interaction.", this);
            }
        }

        private void OnDestroy()
        {
            if (m_Interactable != null)
            {
                m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            ToggleDoor();
        }

        /// <summary>
        /// Toggles door state between open and closed.
        /// </summary>
        [ContextMenu("Toggle Door")]
        public void ToggleDoor()
        {
            SetDoorState(!m_IsOpen);
        }

        public void OpenDoor()
        {
            if (!m_IsOpen) SetDoorState(true);
        }

        public void CloseDoor()
        {
            if (m_IsOpen) SetDoorState(false);
        }

        public void SetDoorState(bool open)
        {
            m_IsOpen = open;

            if (m_SwingRoutine != null)
            {
                StopCoroutine(m_SwingRoutine);
            }

            m_SwingRoutine = StartCoroutine(AnimateSwing(m_IsOpen ? m_OpenRotation : m_ClosedRotation));

            StateChanged?.Invoke(m_IsOpen);

            // Sound feedback
            if (m_AudioSource != null)
            {
                AudioClip clipToPlay = m_IsOpen ? m_OpenSound : m_CloseSound;
                if (clipToPlay == null && m_UseBuiltInSounds)
                {
                    clipToPlay = m_IsOpen ? SynthAudio.DoorOpen : SynthAudio.DoorClose;
                }
                if (clipToPlay != null)
                {
                    m_AudioSource.PlayOneShot(clipToPlay, m_Volume);
                }
            }
        }

        private IEnumerator AnimateSwing(Vector3 targetEuler)
        {
            Quaternion startRot = m_HingeTransform.localRotation;
            Quaternion targetRot = Quaternion.Euler(targetEuler);
            float elapsed = 0f;

            while (elapsed < m_SwingDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / m_SwingDuration);
                float curvedT = m_MotionCurve.Evaluate(t);
                m_HingeTransform.localRotation = Quaternion.Slerp(startRot, targetRot, curvedT);
                yield return null;
            }

            m_HingeTransform.localRotation = targetRot;
            m_SwingRoutine = null;
        }

        // Allow clicking in Editor/Simulator for quick testing
        private void OnMouseDown()
        {
            ToggleDoor();
        }
    }
}

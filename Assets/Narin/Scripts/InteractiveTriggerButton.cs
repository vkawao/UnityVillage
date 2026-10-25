using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace NarinVillage
{
    /// <summary>
    /// Handles 3D touch/poke/ray buttons (Doorbell, Stove Knob, Toilet Flush, Door Handle).
    /// Includes a 0.25s debounce so simultaneous XR Ray + PointerClick events never double-fire.
    /// </summary>
    public class InteractiveTriggerButton : MonoBehaviour, IPointerClickHandler
    {
        public int houseNumber = 2;
        public int actionNumber = 1;
        public string soundEffectType = "Chime"; // "Chime", "Flush", "Burner", "Horn", "Engine"
        [Tooltip("Drag & drop any custom AudioClip (.wav, .mp3, .ogg) here. If empty, uses the procedural sound.")]
        public AudioClip customAudioClip;
        public Light optionalBurnerLight;
        public GameObject optionalToggleObject;
        public bool rotateSelfOnBurner = false;
        public ScriptedMover linkedMover;

        private AudioSource _audioSource;
        private float _lastTriggerTime = -10f;

        private void Awake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0.5f;
            _audioSource.playOnAwake = false;

            if (GetComponent<Collider>() == null)
            {
                MeshFilter mf = GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    BoxCollider bc = gameObject.AddComponent<BoxCollider>();
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = Vector3.Max(mf.sharedMesh.bounds.size, new Vector3(0.12f, 0.12f, 0.08f));
                }
                else
                {
                    BoxCollider bc = gameObject.AddComponent<BoxCollider>();
                    bc.isTrigger = true;
                }
            }

            XRBaseInteractable interactable = GetComponent<XRBaseInteractable>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<XRSimpleInteractable>();
            }
            interactable.selectEntered.AddListener(_ => TriggerInteractionDebounced());
            interactable.activated.AddListener(_ => TriggerInteractionDebounced());
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TriggerInteractionDebounced();
        }

        private void OnMouseDown()
        {
            TriggerInteractionDebounced();
        }

        public void TriggerInteractionDebounced()
        {
            if (Time.unscaledTime - _lastTriggerTime < 0.25f) return;
            _lastTriggerTime = Time.unscaledTime;
            TriggerInteraction();
        }

        public void TriggerInteraction()
        {
            if (VillageActionManager.Instance != null)
            {
                VillageActionManager.Instance.CompleteAction(houseNumber, actionNumber);
            }

            if (customAudioClip != null)
            {
                _audioSource.PlayOneShot(customAudioClip, 1.0f);
            }
            else if (soundEffectType == "Flush")
            {
                _audioSource.PlayOneShot(ProceduralAudioGenerator.CreateFlushSound(2.0f), 0.9f);
            }
            else if (soundEffectType == "Burner")
            {
                _audioSource.PlayOneShot(ProceduralAudioGenerator.CreateRatchetSound(0.4f), 0.85f);
                if (rotateSelfOnBurner)
                {
                    transform.Rotate(45f, 0f, 0f, Space.Self);
                }
                if (optionalToggleObject != null)
                {
                    optionalToggleObject.SetActive(!optionalToggleObject.activeSelf);
                }
                if (optionalBurnerLight != null)
                {
                    optionalBurnerLight.enabled = optionalToggleObject != null
                        ? optionalToggleObject.activeSelf
                        : !optionalBurnerLight.enabled;
                }
            }
            else if (soundEffectType == "Horn")
            {
                _audioSource.PlayOneShot(ProceduralAudioGenerator.CreateCarHorn(0.75f), 0.95f);
            }
            else if (soundEffectType == "Engine")
            {
                _audioSource.PlayOneShot(ProceduralAudioGenerator.CreateEngineRev(1.8f), 0.95f);
            }
            else
            {
                _audioSource.PlayOneShot(ProceduralAudioGenerator.CreateChime(784f, 587f, 0.7f), 0.9f);
            }

            if (linkedMover != null)
            {
                linkedMover.SetOpen(true);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null) return;

            string n = other.name;
            if (n.Contains("Controller") || n.Contains("Hand") || n.Contains("Poke") || n.Contains("Direct") || n.Contains("Finger") || n.Contains("Interactor"))
            {
                TriggerInteractionDebounced();
            }
        }
    }
}

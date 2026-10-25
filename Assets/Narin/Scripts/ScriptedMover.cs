using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace NarinVillage
{
    /// <summary>
    /// Controls scripted doors, roll-up garage doors, sliding windows, car lifts, and drawers.
    /// Includes a 0.25s debounce so simultaneous XR Ray Select + PointerClick events never double-toggle.
    /// </summary>
    public class ScriptedMover : MonoBehaviour, IPointerClickHandler
    {
        [Header("Action Tracking")]
        public int houseNumber = 1;
        public int actionNumber = 1;
        public int secondaryUnlockActionNumber = 0;

        [Header("Movement Settings (Parent Space: Y=Up, X=Right, Z=Forward)")]
        public Vector3 openPositionOffset = Vector3.zero;
        [Tooltip("Degrees to rotate around the vertical Y axis when opened")]
        public float openYawAngle = 0f;
        public Vector3 openRotationEuler = Vector3.zero;
        public float speed = 4.0f;

        [Header("Optional Linked Follower (e.g. Car Body on Car Lift)")]
        public Transform linkedFollowTarget;

        [Header("State")]
        public bool isOpen = false;
        public bool isLocked = false;

        [Header("Proximity Trigger (e.g. Shop Automatic Door)")]
        public bool useProximityTrigger = false;
        public float proximityDistance = 3.5f;

        private Vector3 _closedLocalPos;
        private Quaternion _closedLocalRot;
        private Vector3 _followClosedLocalPos;
        private AudioSource _audio;
        private AudioClip _slideClip;
        private Transform _playerCamera;
        private float _lastToggleTime = -10f;

        private void Awake()
        {
            _closedLocalPos = transform.localPosition;
            _closedLocalRot = transform.localRotation;
            if (linkedFollowTarget != null)
            {
                _followClosedLocalPos = linkedFollowTarget.localPosition;
            }

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0.8f;
            _audio.maxDistance = 15f;
            _slideClip = ProceduralAudioGenerator.CreateSlideSound();

            // Ensure collider exists for XR Ray & Direct interaction
            if (GetComponent<Collider>() == null)
            {
                MeshFilter mf = GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    BoxCollider bc = gameObject.AddComponent<BoxCollider>();
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = mf.sharedMesh.bounds.size;
                }
                else
                {
                    gameObject.AddComponent<BoxCollider>();
                }
            }

            XRSimpleInteractable interactable = GetComponent<XRSimpleInteractable>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<XRSimpleInteractable>();
            }
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.activated.AddListener(OnActivated);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            ToggleOpenDebounced();
        }

        private void OnActivated(ActivateEventArgs args)
        {
            ToggleOpenDebounced();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            ToggleOpenDebounced();
        }

        private void OnMouseDown()
        {
            ToggleOpenDebounced();
        }

        private void ToggleOpenDebounced()
        {
            if (Time.unscaledTime - _lastToggleTime < 0.25f) return;
            _lastToggleTime = Time.unscaledTime;
            ToggleOpen();
        }

        public void ToggleOpen()
        {
            if (isLocked)
            {
                if (_audio != null)
                {
                    _audio.PlayOneShot(ProceduralAudioGenerator.CreateLockedRattleSound(), 0.85f);
                }
                Debug.Log($"[VRVillage] {gameObject.name} is currently LOCKED! Use the UNLOCK button on the Storage UI.");
                return;
            }

            isOpen = !isOpen;
            if (_audio != null && _slideClip != null)
            {
                _audio.PlayOneShot(_slideClip, 0.75f);
            }

            if (VillageActionManager.Instance != null && actionNumber > 0)
            {
                VillageActionManager.Instance.CompleteAction(houseNumber, actionNumber);
            }
        }

        public void SetOpen(bool open)
        {
            SetOpenState(open);
        }

        public void SetOpenState(bool open)
        {
            if (isLocked && open)
            {
                Debug.Log($"[VRVillage] {gameObject.name} is currently LOCKED! Cannot open.");
                return;
            }
            if (isOpen == open) return;

            isOpen = open;
            if (_audio != null && _slideClip != null)
            {
                _audio.PlayOneShot(_slideClip, 0.7f);
            }

            if (open && VillageActionManager.Instance != null && actionNumber > 0)
            {
                VillageActionManager.Instance.CompleteAction(houseNumber, actionNumber);
            }
        }

        public void LockDoorAndRecordAction(int houseNum, int lockActionNum)
        {
            SetLocked(true, lockActionNum, 0);
        }

        public void UnlockDoorAndRecordAction(int houseNum, int unlockActionNum)
        {
            SetLocked(false, 0, unlockActionNum);
            SetOpenState(true);
        }

        public void SetLocked(bool locked, int lockActionNum, int unlockActionNum)
        {
            isLocked = locked;
            if (locked)
            {
                isOpen = false;
                if (VillageActionManager.Instance != null && lockActionNum > 0)
                    VillageActionManager.Instance.CompleteAction(houseNumber, lockActionNum);
            }
            else
            {
                if (VillageActionManager.Instance != null && unlockActionNum > 0)
                    VillageActionManager.Instance.CompleteAction(houseNumber, unlockActionNum);
            }
        }

        private void Update()
        {
            if (useProximityTrigger)
            {
                if (_playerCamera == null && Camera.main != null)
                    _playerCamera = Camera.main.transform;

                if (_playerCamera != null)
                {
                    Vector3 a = transform.position;
                    Vector3 b = _playerCamera.position;
                    a.y = 0f;
                    b.y = 0f;
                    float dist = Vector3.Distance(a, b);
                    if (dist <= proximityDistance && !isOpen)
                    {
                        SetOpenState(true);
                    }
                    else if (dist > proximityDistance + 0.85f && isOpen)
                    {
                        SetOpenState(false);
                    }
                }
            }

            Vector3 targetPos = isOpen ? (_closedLocalPos + openPositionOffset) : _closedLocalPos;

            Quaternion targetRot = _closedLocalRot;
            if (isOpen)
            {
                if (Mathf.Abs(openYawAngle) > 0.01f)
                {
                    targetRot = Quaternion.Euler(0f, openYawAngle, 0f) * _closedLocalRot;
                }
                else if (openRotationEuler.sqrMagnitude > 0.01f)
                {
                    targetRot = _closedLocalRot * Quaternion.Euler(openRotationEuler);
                }
            }

            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * speed);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * speed);

            if (linkedFollowTarget != null)
            {
                Vector3 followTargetPos = isOpen ? (_followClosedLocalPos + openPositionOffset) : _followClosedLocalPos;
                linkedFollowTarget.localPosition = Vector3.Lerp(linkedFollowTarget.localPosition, followTargetPos, Time.deltaTime * speed);
            }
        }
    }
}

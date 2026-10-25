using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

namespace NarinVillage
{
    /// <summary>
    /// Backrooms Monster AI:
    /// - Respects God Mode / Invisibility toggle (VillageActionManager.Instance.isInvisibleToMonster).
    /// - Respects Pause toggle (VillageActionManager.Instance.isMonsterPaused).
    /// - Continuously patrols the Backrooms complex.
    /// - Chases when player is in direct Line of Sight or within 3m proximity.
    /// <summary>
    /// Style of synthesized stinger played when the monster spots the player.
    /// </summary>
    public enum PlayerFoundSoundStyle
    {
        DramaticHorrorStinger,
        DissonantScreechChord,
        StealthAlertSting,
        DeepOminousDrone
    }

    /// <summary>
    /// Backrooms Monster AI:
    /// - Respects God Mode / Invisibility toggle (VillageActionManager.Instance.isInvisibleToMonster).
    /// - Respects Pause toggle (VillageActionManager.Instance.isMonsterPaused).
    /// - Continuously patrols the Backrooms complex.
    /// - Chases when player is in direct Line of Sight or within 3m proximity.
    /// - Triggers Action 9 on Line-of-Sight chase, and Action 10 Jumpscare (image + scream) on touch.
    /// </summary>
    public class BackroomsMonsterAI : MonoBehaviour
    {
        [Header("Monster Speeds")]
        public float patrolSpeed = 1.6f;
        public float chaseSpeed = 3.2f;
        public float sightRange = 18f;
        public float touchDistance = 1.25f;

        [Header("Player Found Sound Settings")]
        [Tooltip("Custom 'Player Found' / spotted sound effect. Drag & drop any audio file (.wav, .mp3, .ogg) here in the Inspector to override.")]
        public AudioClip customPlayerFoundSound;
        [Tooltip("Procedural sound style to use when customPlayerFoundSound is not assigned.")]
        public PlayerFoundSoundStyle proceduralSoundStyle = PlayerFoundSoundStyle.DramaticHorrorStinger;
        [Tooltip("Volume of the 'Player Found' sound effect (0.0 to 1.0).")]
        [Range(0f, 1f)]
        public float playerFoundVolume = 0.95f;
        [Tooltip("Pitch / playback speed of the 'Player Found' sound (0.5 to 2.0).")]
        [Range(0.5f, 2.0f)]
        public float playerFoundPitch = 1.0f;
        [Tooltip("Spatial Blend: 0.0 = 2D (in player's headset like a dramatic horror movie score), 1.0 = 3D (emanates from monster's physical position in world).")]
        [Range(0f, 1f)]
        public float playerFoundSpatialBlend = 0.2f;
        [Tooltip("Cooldown period in seconds before the 'Player Found' sound can trigger again if sight is lost and regained.")]
        public float playerFoundCooldown = 3.5f;

        [Header("Custom Jumpscare Settings")]
        [Tooltip("Drag & drop any custom Sprite here. If empty, uses the default procedural scare face.")]
        public Sprite customJumpscareImage;
        [Tooltip("Drag & drop any custom AudioClip here. If empty, uses the default procedural scream.")]
        public AudioClip customJumpscareSound;

        [Header("Walking Sound / Footstep Settings")]
        [Tooltip("Custom footstep/walking sound effect. Drag & drop any audio file here.")]
        public AudioClip customFootstepSound;
        [Tooltip("Optional array of footstep variations. If provided, randomly alternates between them.")]
        public AudioClip[] customFootstepSounds;
        [Tooltip("Distance the monster must travel between footsteps (meters). Default is 0.85m.")]
        public float stepDistance = 0.85f;
        [Range(0f, 1f)]
        public float footstepVolume = 0.85f;

        public List<Vector3> patrolWaypoints = new List<Vector3>();

        private int _currentWaypoint = 0;
        private bool _isChasing = false;
        private float _lostSightTimer = 0f;
        private float _lastJumpscareTime = -20f;
        private float _lastPlayerFoundSoundTime = -30f;
        private Vector3 _lastKnownPlayerPos;
        private float _timeSinceLastSeen = 999f;
        private const float CHASE_LOST_GRACE_PERIOD = 2.5f;
        private Transform _playerTransform;
        private GameObject _jumpscareCanvasObj;
        private AudioSource _monsterAudio;
        private AudioSource _footstepAudio;
        private AudioSource _playerFoundAudio;
        private float _footstepDistAccumulator = 0f;
        private CharacterController _cc;

        private Vector3 _lastFramePos;
        private float _stuckTimer = 0f;
        private Vector3 _currentMoveDir = Vector3.forward;

        private void Start()
        {
            _cc = GetComponent<CharacterController>();
            if (_cc == null)
            {
                _cc = gameObject.AddComponent<CharacterController>();
            }
            _cc.radius = 0.30f;
            _cc.height = 1.8f;
            _cc.center = new Vector3(0f, 0.9f, 0f);
            _cc.stepOffset = 0.4f;
            _cc.slopeLimit = 60f;
            _cc.skinWidth = 0.02f;
            _cc.minMoveDistance = 0f;

            if (transform.position.y < 0.05f)
            {
                transform.position = new Vector3(transform.position.x, 0.05f, transform.position.z);
            }
            _lastFramePos = transform.position;

            _monsterAudio = gameObject.AddComponent<AudioSource>();
            _monsterAudio.spatialBlend = 0.85f;
            _monsterAudio.loop = true;
            _monsterAudio.minDistance = 2f;
            _monsterAudio.maxDistance = 25f;
            _monsterAudio.clip = ProceduralAudioGenerator.CreateFluorescentBuzz(3.0f);
            _monsterAudio.Play();

            _footstepAudio = gameObject.AddComponent<AudioSource>();
            _footstepAudio.spatialBlend = 1.0f; // 3D positional audio
            _footstepAudio.loop = false;
            _footstepAudio.playOnAwake = false;
            _footstepAudio.minDistance = 1.5f;
            _footstepAudio.maxDistance = 22f;
            _footstepAudio.rolloffMode = AudioRolloffMode.Logarithmic;

            _playerFoundAudio = gameObject.AddComponent<AudioSource>();
            _playerFoundAudio.spatialBlend = playerFoundSpatialBlend;
            _playerFoundAudio.loop = false;
            _playerFoundAudio.playOnAwake = false;
            _playerFoundAudio.minDistance = 2f;
            _playerFoundAudio.maxDistance = 35f;
            _playerFoundAudio.rolloffMode = AudioRolloffMode.Logarithmic;

            // Pre-cache player transform and ensure MainCamera tag
            GetPlayerTransform();

            if (chaseSpeed < 3.5f) chaseSpeed = 3.6f;
            if (sightRange < 20f) sightRange = 22f;

            _currentMoveDir = transform.forward;

            // If running inside procedural Backrooms, waypoints and spawn are handled by BackroomsProceduralGenerator
            if (BackroomsProceduralGenerator.Instance == null)
            {
                // Enforce clean 6-point orthogonal loop through central mid-depth rooms and doorways (~20-25m from player spawn):
                patrolWaypoints = new List<Vector3>
                {
                    new Vector3(214.4f, 0.05f, 219.2f),
                    new Vector3(219.2f, 0.05f, 219.2f),
                    new Vector3(224.0f, 0.05f, 219.2f),
                    new Vector3(224.0f, 0.05f, 224.0f),
                    new Vector3(219.2f, 0.05f, 224.0f),
                    new Vector3(214.4f, 0.05f, 224.0f)
                };

                if (transform.position.y < 0.05f || Vector3.Distance(transform.position, patrolWaypoints[0]) > 10f)
                {
                    transform.position = patrolWaypoints[0];
                }
            }
            _lastFramePos = transform.position;
        }

        public Transform GetPlayerTransform()
        {
            if (_playerTransform != null && _playerTransform.gameObject.activeInHierarchy)
            {
                return _playerTransform;
            }

            // 1. Try Camera.main
            if (Camera.main != null)
            {
                _playerTransform = Camera.main.transform;
                return _playerTransform;
            }

            // 2. Try VillageActionManager's xrOriginTransform
            if (VillageActionManager.Instance != null && VillageActionManager.Instance.xrOriginTransform != null)
            {
                Camera c = VillageActionManager.Instance.xrOriginTransform.GetComponentInChildren<Camera>();
                if (c != null)
                {
                    TagCameraAsMain(c.gameObject);
                    _playerTransform = c.transform;
                    return _playerTransform;
                }
            }

            // 3. Try XROrigin in scene
            var origin = FindFirstObjectByType<XROrigin>();
            if (origin != null && origin.Camera != null)
            {
                TagCameraAsMain(origin.Camera.gameObject);
                _playerTransform = origin.Camera.transform;
                return _playerTransform;
            }

            // 4. Try GameObject with Player tag
            GameObject playerGo = GameObject.FindWithTag("Player");
            if (playerGo != null)
            {
                Camera c = playerGo.GetComponentInChildren<Camera>();
                if (c != null)
                {
                    TagCameraAsMain(c.gameObject);
                    _playerTransform = c.transform;
                    return _playerTransform;
                }
                _playerTransform = playerGo.transform;
                return _playerTransform;
            }

            // 5. Try any active rendering Camera in scene
            Camera[] allCams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in allCams)
            {
                if (cam != null && cam.isActiveAndEnabled && cam.targetTexture == null)
                {
                    TagCameraAsMain(cam.gameObject);
                    _playerTransform = cam.transform;
                    return _playerTransform;
                }
            }

            return null;
        }

        private void TagCameraAsMain(GameObject go)
        {
            if (go != null && (string.IsNullOrEmpty(go.tag) || go.tag == "Untagged"))
            {
                try { go.tag = "MainCamera"; } catch {}
            }
        }

        private void OnEnable()
        {
            _lastFramePos = transform.position;
            _isChasing = false;
            _timeSinceLastSeen = 999f;
            _lostSightTimer = 0f;
            _stuckTimer = 0f;
            if (_monsterAudio != null && !_monsterAudio.isPlaying)
            {
                _monsterAudio.Play();
            }
        }

        private void OnDisable()
        {
            if (_monsterAudio != null && _monsterAudio.isPlaying)
            {
                _monsterAudio.Stop();
            }
            if (_footstepAudio != null && _footstepAudio.isPlaying)
            {
                _footstepAudio.Stop();
            }
            if (_playerFoundAudio != null && _playerFoundAudio.isPlaying)
            {
                _playerFoundAudio.Stop();
            }
        }

        private void Update()
        {
            if (VillageActionManager.Instance != null && VillageActionManager.Instance.isMonsterPaused)
            {
                if (_monsterAudio != null && _monsterAudio.isPlaying)
                    _monsterAudio.Pause();
                return;
            }
            if (_monsterAudio != null && !_monsterAudio.isPlaying)
            {
                _monsterAudio.UnPause();
            }

            Transform playerT = GetPlayerTransform();
            bool canSeePlayer = CheckLineOfSight(playerT);

            if (canSeePlayer && playerT != null)
            {
                _timeSinceLastSeen = 0f;
                _lostSightTimer = 0f;
                _lastKnownPlayerPos = new Vector3(playerT.position.x, transform.position.y, playerT.position.z);

                // Active chasing when player is in direct Line of Sight
                if (!_isChasing)
                {
                    _isChasing = true;
                    PlayPlayerFoundSound();
                    if (VillageActionManager.Instance != null)
                        VillageActionManager.Instance.CompleteAction(5, 9);
                }

                float dist = Vector3.Distance(transform.position, _lastKnownPlayerPos);

                // Check touch for Jumpscare (Action 10)
                if (dist <= touchDistance && Time.time - _lastJumpscareTime > 5f)
                {
                    TriggerJumpscare(playerT);
                    return;
                }

                MoveTowards(_lastKnownPlayerPos, chaseSpeed);
            }
            else
            {
                _timeSinceLastSeen += Time.deltaTime;

                if (_isChasing)
                {
                    // Player was in sight but just went through a doorway or turned a corner:
                    // Continue pursuing towards the last known doorway/corner position during grace period!
                    if (_timeSinceLastSeen < CHASE_LOST_GRACE_PERIOD)
                    {
                        float distToLastSeen = Vector3.Distance(transform.position, _lastKnownPlayerPos);
                        if (distToLastSeen > 0.6f)
                        {
                            MoveTowards(_lastKnownPlayerPos, chaseSpeed);
                            return;
                        }
                        else
                        {
                            // Monster reached the doorway/corner: steer in player's direction to establish line of sight
                            Vector3 steerDir = transform.forward;
                            if (playerT != null)
                            {
                                Vector3 toPlayer = playerT.position - transform.position;
                                toPlayer.y = 0f;
                                if (toPlayer.sqrMagnitude > 0.01f) steerDir = toPlayer.normalized;
                            }
                            Vector3 peekTarget = transform.position + steerDir * 2.0f;
                            MoveTowards(peekTarget, chaseSpeed);
                            return;
                        }
                    }
                    else
                    {
                        // Grace period expired without re-acquiring line of sight.
                        // Line of sight is truly cut off: stop in place and return to roaming.
                        _isChasing = false;
                        _lostSightTimer = 1.4f; // Stops in place when line of sight is cut off
                        _currentWaypoint = GetNearestWaypointIndex();
                    }
                }

                if (_lostSightTimer > 0f)
                {
                    // Remain stopped in place
                    _lostSightTimer -= Time.deltaTime;
                    if (_cc != null && _cc.enabled)
                    {
                        _cc.Move(Vector3.down * (9.81f * Time.deltaTime));
                    }
                    return;
                }

                // Roam along waypoints when it doesn't see the player
                Patrol();
            }
        }

        private bool CheckLineOfSight(Transform playerCam)
        {
            if (playerCam == null) return false;
            if (VillageActionManager.Instance != null && VillageActionManager.Instance.isInvisibleToMonster) return false;

            Vector3 monsterPos = transform.position;
            Vector3 playerPos = playerCam.position;
            float dist = Vector3.Distance(monsterPos, playerPos);

            if (dist > sightRange) return false;

            // 1. Immediate proximity detection (within 2.2m):
            // When right in the doorway or touching distance, monster immediately senses player
            if (dist <= 2.2f)
            {
                return true;
            }

            Vector3 toPlayer = playerPos - monsterPos;

            // 2. Field of View check:
            // When roaming, player must be within wide 220-degree peripheral vision (angle <= 110 deg).
            // When chasing, monster has 360-degree awareness.
            if (!_isChasing)
            {
                Vector3 toPlayerFlat = new Vector3(toPlayer.x, 0f, toPlayer.z);
                if (toPlayerFlat.sqrMagnitude > 0.01f)
                {
                    float angle = Vector3.Angle(transform.forward, toPlayerFlat.normalized);
                    if (angle > 110f)
                    {
                        return false;
                    }
                }
            }

            // 3. Multi-ray Line of Sight check:
            // Casts from eye, chest, and lateral offsets to player head, chest, and waist.
            // Guarantees reliable detection through doorways and past obstacles.
            Vector3 monsterEye = monsterPos + Vector3.up * 1.15f;
            Vector3 monsterChest = monsterPos + Vector3.up * 0.70f;
            Vector3 rightOffset = transform.right * 0.22f;

            Vector3 playerHead = playerPos;
            Vector3 playerChest = playerPos - Vector3.up * 0.5f;
            Vector3 playerWaist = playerPos - Vector3.up * 0.9f;

            return HasClearRay(monsterEye, playerHead) ||
                   HasClearRay(monsterEye, playerChest) ||
                   HasClearRay(monsterChest, playerChest) ||
                   HasClearRay(monsterChest, playerWaist) ||
                   HasClearRay(monsterEye + rightOffset, playerHead) ||
                   HasClearRay(monsterEye - rightOffset, playerHead);
        }

        private bool HasClearRay(Vector3 origin, Vector3 target)
        {
            Vector3 diff = target - origin;
            float dist = diff.magnitude;
            if (dist < 0.25f) return true;
            Vector3 dir = diff / dist;

            RaycastHit[] hits = Physics.RaycastAll(origin, dir, dist - 0.12f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null) continue;
                if (c.isTrigger) continue;
                if (IsSelfOrChild(c)) continue;
                if (IsPlayerHit(hits[i])) continue;
                if (c.GetComponentInParent<Canvas>() != null) continue;

                string cName = c.name;
                if (cName.IndexOf("Almond", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (cName.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (cName.IndexOf("Carpet", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (cName.IndexOf("Ceiling", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (cName.IndexOf("Light", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (cName.IndexOf("Flashlight", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (cName.IndexOf("Desk", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (cName.IndexOf("Table", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (cName.IndexOf("Drawer", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (cName.IndexOf("Chair", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

                // An obstacle (e.g. solid wall/corner) is blocking line of sight
                return false;
            }
            return true;
        }

        private int GetNearestWaypointIndex()
        {
            if (patrolWaypoints == null || patrolWaypoints.Count == 0) return 0;
            int nearest = 0;
            float minDist = float.MaxValue;
            for (int i = 0; i < patrolWaypoints.Count; i++)
            {
                float d = Vector3.Distance(transform.position, patrolWaypoints[i]);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = i;
                }
            }
            return nearest;
        }

        private bool IsSelfOrChild(Collider c)
        {
            if (c == null) return false;
            return c.transform.IsChildOf(transform) || c.gameObject == gameObject;
        }

        private bool IsPlayerHit(RaycastHit hit)
        {
            if (hit.collider == null) return false;
            if (_playerTransform != null && hit.collider.transform.IsChildOf(NarinScope.PlayerRig(_playerTransform))) return true;
            return hit.collider.CompareTag("Player") ||
                   hit.collider.GetComponentInParent<CharacterController>() != null ||
                   hit.collider.name.Contains("XR") ||
                   hit.collider.name.Contains("Camera");
        }

        private void Patrol()
        {
            if (patrolWaypoints == null || patrolWaypoints.Count == 0) return;

            Vector3 target = patrolWaypoints[_currentWaypoint];
            target.y = transform.position.y;
            Vector3 toWaypoint = target - transform.position;

            if (toWaypoint.magnitude < 1.4f || _stuckTimer > 0.8f)
            {
                _currentWaypoint = (_currentWaypoint + 1) % patrolWaypoints.Count;
                _stuckTimer = 0f;
                target = patrolWaypoints[_currentWaypoint];
                target.y = transform.position.y;
            }

            MoveTowards(target, patrolSpeed);
        }

        private bool IsIgnoredObstacle(Collider c)
        {
            if (c == null) return true;
            if (c.isTrigger) return true;
            if (IsSelfOrChild(c)) return true;
            if (_playerTransform != null && c.transform.IsChildOf(NarinScope.PlayerRig(_playerTransform))) return true;
            if (c.CompareTag("Player")) return true;
            if (c.name.Contains("XR") || c.name.Contains("Camera")) return true;
            if (c.GetComponentInParent<Canvas>() != null) return true;

            string name = c.name;
            if (name.IndexOf("Almond", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.IndexOf("Carpet", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.IndexOf("Ceiling", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.IndexOf("Light", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.IndexOf("Flashlight", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

        private bool CheckProbeHit(Vector3 origin, Vector3 dir, float distance, float radius, out RaycastHit hitResult)
        {
            hitResult = default;
            float closestDist = float.MaxValue;
            bool hasHit = false;

            // 1. RaycastAll: Pinpoint line sweep from origin (ignores self, triggers, floor, player)
            RaycastHit[] rayHits = Physics.RaycastAll(origin, dir, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < rayHits.Length; i++)
            {
                RaycastHit h = rayHits[i];
                if (IsIgnoredObstacle(h.collider)) continue;
                if (h.distance < closestDist)
                {
                    closestDist = h.distance;
                    hitResult = h;
                    hasHit = true;
                }
            }

            // 2. SphereCastAll: Swept sphere for body & shoulder clearance through doorways
            if (radius > 0.02f)
            {
                Vector3 sphereStart = origin + dir * 0.10f;
                float sweepDist = Mathf.Max(0.05f, distance - 0.10f);
                RaycastHit[] sphereHits = Physics.SphereCastAll(sphereStart, radius, dir, sweepDist, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < sphereHits.Length; i++)
                {
                    RaycastHit h = sphereHits[i];
                    if (IsIgnoredObstacle(h.collider)) continue;
                    float effDist = h.distance + 0.10f;
                    if (effDist < closestDist)
                    {
                        closestDist = effDist;
                        hitResult = h;
                        hitResult.distance = effDist;
                        hasHit = true;
                    }
                }
            }

            return hasHit;
        }

        private Vector3 ComputeSteeringDirection(Vector3 desiredDir, Vector3 targetPos)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 0.85f;
            float forwardProbeDist = 1.15f;
            float bodyRadius = 0.26f;

            // 1. Check direct forward path
            bool forwardBlocked = CheckProbeHit(rayOrigin, desiredDir, forwardProbeDist, bodyRadius, out RaycastHit forwardHit);

            if (!forwardBlocked)
            {
                // Forward is open! Check side feelers to keep centered in corridors & doorways
                Vector3 steer = desiredDir;
                Vector3 leftFeeler = Quaternion.Euler(0f, -32f, 0f) * desiredDir;
                Vector3 rightFeeler = Quaternion.Euler(0f, 32f, 0f) * desiredDir;

                if (CheckProbeHit(rayOrigin, leftFeeler, 0.65f, 0.14f, out RaycastHit leftHit))
                {
                    float repulse = Mathf.Clamp01((0.65f - leftHit.distance) / 0.65f);
                    Vector3 norm = leftHit.normal; norm.y = 0f;
                    if (norm.sqrMagnitude > 0.01f) steer += norm.normalized * (repulse * 0.55f);
                }
                if (CheckProbeHit(rayOrigin, rightFeeler, 0.65f, 0.14f, out RaycastHit rightHit))
                {
                    float repulse = Mathf.Clamp01((0.65f - rightHit.distance) / 0.65f);
                    Vector3 norm = rightHit.normal; norm.y = 0f;
                    if (norm.sqrMagnitude > 0.01f) steer += norm.normalized * (repulse * 0.55f);
                }

                steer.y = 0f;
                return steer.sqrMagnitude > 0.01f ? steer.normalized : desiredDir;
            }

            // 2. Forward is blocked by an obstacle/wall!
            Vector3 wallNormal = forwardHit.normal;
            wallNormal.y = 0f;
            if (wallNormal.sqrMagnitude > 0.01f)
                wallNormal.Normalize();
            else
                wallNormal = -desiredDir;

            // Context Steering: evaluate a wide fan of candidate directions
            float[] angles = new float[] {
                25f, -25f, 45f, -45f, 65f, -65f, 85f, -85f, 110f, -110f, 135f, -135f
            };

            Vector3 bestDir = desiredDir;
            float bestScore = float.MinValue;

            // Also test the wall-sliding tangents (parallel to wall surface)
            Vector3 tangentR = Vector3.Cross(Vector3.up, wallNormal).normalized;
            Vector3 tangentL = -tangentR;

            List<Vector3> candidates = new List<Vector3>(angles.Length + 2);
            for (int i = 0; i < angles.Length; i++)
            {
                candidates.Add(Quaternion.Euler(0f, angles[i], 0f) * desiredDir);
            }
            candidates.Add(tangentR);
            candidates.Add(tangentL);

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 cand = candidates[i];
                cand.y = 0f;
                if (cand.sqrMagnitude < 0.01f) continue;
                cand.Normalize();

                // Reject directions heading directly into the wall
                float intoWall = Vector3.Dot(cand, -wallNormal);
                if (intoWall > 0.30f) continue;

                float testDist = 1.1f;
                bool candHitObstacle = CheckProbeHit(rayOrigin, cand, testDist, bodyRadius, out RaycastHit candHit);
                float clearance = candHitObstacle ? candHit.distance : testDist + 0.6f;

                // If clearance is too small (e.g. wall immediately touching body), direction is unusable
                if (clearance < 0.35f) continue;

                // Score candidate:
                // Alignment with desired target (range: -1 to +1)
                float targetDot = Vector3.Dot(cand, desiredDir);
                // Clearance bonus (longer open corridor / doorway = higher score)
                float clearanceScore = clearance * 1.6f;
                // Wall normal bonus (slight reward for moving along or away from wall)
                float wallDot = Vector3.Dot(cand, wallNormal) * 0.4f;

                float score = (targetDot * 2.2f) + clearanceScore + wallDot;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = cand;
                }
            }

            if (bestScore > float.MinValue + 10f)
            {
                return bestDir;
            }

            // Fallback: move along wall normal out of collision
            return wallNormal;
        }

        private void MoveTowards(Vector3 targetPos, float speed)
        {
            Vector3 desiredDir = targetPos - transform.position;
            desiredDir.y = 0f;
            float distToTarget = desiredDir.magnitude;
            if (distToTarget < 0.05f) return;
            desiredDir /= distToTarget;

            Vector3 finalDir = ComputeSteeringDirection(desiredDir, targetPos);

            // Smooth steering transitions
            if (_currentMoveDir.sqrMagnitude < 0.01f)
            {
                _currentMoveDir = finalDir;
            }
            else
            {
                _currentMoveDir = Vector3.RotateTowards(_currentMoveDir, finalDir, 10f * Time.deltaTime, 0f);
            }

            Vector3 motion = _currentMoveDir * (speed * Time.deltaTime) + Vector3.down * (9.81f * Time.deltaTime);
            if (_cc != null && _cc.enabled)
            {
                _cc.Move(motion);
            }
            else
            {
                transform.position += _currentMoveDir * (speed * Time.deltaTime);
            }

            // Anti-stuck watchdog
            float distMoved = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                               new Vector3(_lastFramePos.x, 0, _lastFramePos.z));

            if (distMoved < 0.008f)
            {
                _stuckTimer += Time.deltaTime;
                if (_stuckTimer > 0.8f)
                {
                    if (!_isChasing && patrolWaypoints != null && patrolWaypoints.Count > 0)
                    {
                        _currentWaypoint = (_currentWaypoint + 1) % patrolWaypoints.Count;
                    }
                    _stuckTimer = 0f;
                }
            }
            else
            {
                _stuckTimer = Mathf.Max(0f, _stuckTimer - Time.deltaTime);
            }

            _lastFramePos = transform.position;

            // Footstep / walking noise tracking
            if (distMoved > 0.005f)
            {
                _footstepDistAccumulator += distMoved;
                float currentStepDistance = _isChasing ? Mathf.Max(0.45f, stepDistance * 0.70f) : stepDistance;
                if (_footstepDistAccumulator >= currentStepDistance)
                {
                    _footstepDistAccumulator = 0f;
                    PlayFootstepSound();
                }
            }

            // Fast, responsive rotation during chase (14f), smooth during patrol (6f)
            Vector3 flatDir = new Vector3(_currentMoveDir.x, 0f, _currentMoveDir.z);
            if (flatDir.sqrMagnitude > 0.001f)
            {
                float rotSpeed = _isChasing ? 14f : 6f;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flatDir.normalized), Time.deltaTime * rotSpeed);
            }
        }

        private void PlayFootstepSound()
        {
            if (_footstepAudio == null) return;

            AudioClip clipToPlay = null;
            if (customFootstepSounds != null && customFootstepSounds.Length > 0)
            {
                clipToPlay = customFootstepSounds[Random.Range(0, customFootstepSounds.Length)];
            }
            if (clipToPlay == null)
            {
                clipToPlay = customFootstepSound;
            }
            if (clipToPlay == null)
            {
                clipToPlay = ProceduralAudioGenerator.CreateFootstepThud(0.18f);
            }

            if (clipToPlay != null)
            {
                _footstepAudio.pitch = Random.Range(0.92f, 1.08f);
                _footstepAudio.PlayOneShot(clipToPlay, footstepVolume);
            }
        }

        /// <summary>
        /// Plays the editable 'Player Found' / spotted sound effect when the monster spots the player.
        /// Fully customizable in the Inspector: drag & drop any custom audio file, choose from 4 procedural styles,
        /// and adjust volume, pitch, and 2D/3D spatial blend.
        /// </summary>
        public void PlayPlayerFoundSound()
        {
            if (Time.time - _lastPlayerFoundSoundTime < playerFoundCooldown) return;
            _lastPlayerFoundSoundTime = Time.time;

            if (_playerFoundAudio == null)
            {
                _playerFoundAudio = gameObject.AddComponent<AudioSource>();
                _playerFoundAudio.loop = false;
                _playerFoundAudio.playOnAwake = false;
                _playerFoundAudio.minDistance = 2f;
                _playerFoundAudio.maxDistance = 35f;
                _playerFoundAudio.rolloffMode = AudioRolloffMode.Logarithmic;
            }

            _playerFoundAudio.spatialBlend = playerFoundSpatialBlend;
            _playerFoundAudio.pitch = playerFoundPitch;

            AudioClip clipToPlay = customPlayerFoundSound;
            if (clipToPlay == null)
            {
                clipToPlay = ProceduralAudioGenerator.CreatePlayerFoundSound(proceduralSoundStyle);
            }

            if (clipToPlay != null)
            {
                _playerFoundAudio.PlayOneShot(clipToPlay, playerFoundVolume);
                Debug.Log($"[BackroomsMonsterAI] Played 'Player Found' sound effect: {clipToPlay.name} (Style: {proceduralSoundStyle}, Vol: {playerFoundVolume:F2}, Pitch: {playerFoundPitch:F2}, SpatialBlend: {playerFoundSpatialBlend:F2})");
            }
        }

        /// <summary>
        /// Context menu helper to audition / test play the 'Player Found' sound effect directly from the Unity Editor Inspector.
        /// </summary>
        [ContextMenu("Test Play Player Found Sound")]
        public void TestPlayPlayerFoundSound()
        {
            _lastPlayerFoundSoundTime = -30f;
            PlayPlayerFoundSound();
        }

        private void TriggerJumpscare(Transform playerCam)
        {
            if (playerCam == null) playerCam = GetPlayerTransform();
            if (playerCam == null) return;

            _lastJumpscareTime = Time.time;
            _isChasing = false;
            _timeSinceLastSeen = 999f;
            _lostSightTimer = 2.0f;

            if (VillageActionManager.Instance != null)
            {
                VillageActionManager.Instance.CompleteAction(5, 10);
            }

            // Play jumpscare scream (custom audio clip if assigned, otherwise procedural)
            AudioClip screamClip = customJumpscareSound != null 
                ? customJumpscareSound 
                : ProceduralAudioGenerator.CreateJumpscareScream(1.1f);
            AudioSource.PlayClipAtPoint(screamClip, playerCam.position, 1.0f);

            // Show Jumpscare Image overlay in front of VR camera
            StartCoroutine(ShowJumpscareOverlayRoutine(playerCam));

            // Relocate monster to out-of-line-of-sight hidden position so player can recover without seeing it spawn
            Vector3 newPos = Vector3.zero;
            if (BackroomsProceduralGenerator.Instance != null)
            {
                newPos = BackroomsProceduralGenerator.Instance.FindHiddenSpawnPosition(playerCam, 24f, 42f);
            }
            if (newPos == Vector3.zero && patrolWaypoints != null && patrolWaypoints.Count > 0)
            {
                newPos = patrolWaypoints[(_currentWaypoint + 3) % patrolWaypoints.Count];
            }

            if (newPos != Vector3.zero)
            {
                if (_cc != null) _cc.enabled = false;
                transform.position = newPos;
                if (_cc != null) _cc.enabled = true;
                _currentMoveDir = transform.forward;
                _lastFramePos = newPos;

                if (BackroomsProceduralGenerator.Instance != null)
                {
                    float cs = BackroomsProceduralGenerator.Instance.cellSize;
                    patrolWaypoints = new List<Vector3>
                    {
                        newPos,
                        newPos + new Vector3(cs, 0f, 0f),
                        newPos + new Vector3(cs, 0f, cs),
                        newPos + new Vector3(0f, 0f, cs)
                    };
                    _currentWaypoint = 0;
                }
            }
        }

        private IEnumerator ShowJumpscareOverlayRoutine(Transform playerCam)
        {
            if (_jumpscareCanvasObj != null)
            {
                Destroy(_jumpscareCanvasObj);
            }
            _jumpscareCanvasObj = CreateJumpscareCanvas(playerCam);

            _jumpscareCanvasObj.transform.SetParent(playerCam, false);
            _jumpscareCanvasObj.transform.localPosition = new Vector3(0f, 0f, 0.45f);
            _jumpscareCanvasObj.transform.localRotation = Quaternion.identity;
            _jumpscareCanvasObj.SetActive(true);

            float elapsed = 0f;
            while (elapsed < 1.15f)
            {
                elapsed += Time.deltaTime;
                _jumpscareCanvasObj.transform.localPosition = new Vector3(
                    Random.Range(-0.03f, 0.03f),
                    Random.Range(-0.03f, 0.03f),
                    0.45f);
                yield return null;
            }

            _jumpscareCanvasObj.SetActive(false);
        }

        private GameObject CreateJumpscareCanvas(Transform parentCam)
        {
            GameObject canvasGO = new GameObject("Jumpscare_VR_Overlay");
            canvasGO.transform.SetParent(parentCam, false);
            Canvas c = canvasGO.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.sortingOrder = 999;

            RectTransform rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(800, 800);
            rt.localScale = Vector3.one * 0.0012f;

            if (customJumpscareImage != null)
            {
                GameObject customImgGO = new GameObject("CustomScareImage");
                customImgGO.transform.SetParent(canvasGO.transform, false);
                Image img = customImgGO.AddComponent<Image>();
                img.sprite = customJumpscareImage;
                img.color = Color.white;
                img.preserveAspect = true;
                RectTransform imgRt = customImgGO.GetComponent<RectTransform>();
                imgRt.anchorMin = Vector2.zero;
                imgRt.anchorMax = Vector2.one;
                imgRt.offsetMin = Vector2.zero;
                imgRt.offsetMax = Vector2.zero;
            }
            else
            {
                GameObject bg = new GameObject("ScareImage");
                bg.transform.SetParent(canvasGO.transform, false);
                Image img = bg.AddComponent<Image>();
                img.color = new Color(0.08f, 0.0f, 0.0f, 0.96f);
                RectTransform bgRt = bg.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;

                CreateEye(bg.transform, new Vector2(-110, 80));
                CreateEye(bg.transform, new Vector2(110, 80));

                GameObject mouth = new GameObject("ScareMouth");
                mouth.transform.SetParent(bg.transform, false);
                Image mImg = mouth.AddComponent<Image>();
                mImg.color = new Color(0.95f, 0.05f, 0.05f, 1f);
                RectTransform mRt = mouth.GetComponent<RectTransform>();
                mRt.anchoredPosition = new Vector2(0, -90);
                mRt.sizeDelta = new Vector2(260, 140);
            }

            return canvasGO;
        }

        private void CreateEye(Transform parent, Vector2 pos)
        {
            GameObject eye = new GameObject("Eye");
            eye.transform.SetParent(parent, false);
            Image img = eye.AddComponent<Image>();
            img.color = Color.white;
            RectTransform rt = eye.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(90, 90);

            GameObject pupil = new GameObject("Pupil");
            pupil.transform.SetParent(eye.transform, false);
            Image pImg = pupil.AddComponent<Image>();
            pImg.color = Color.red;
            RectTransform pRt = pupil.GetComponent<RectTransform>();
            pRt.anchoredPosition = Vector2.zero;
            pRt.sizeDelta = new Vector2(36, 36);
        }
    }
}

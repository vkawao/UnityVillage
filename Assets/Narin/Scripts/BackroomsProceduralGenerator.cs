using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace NarinVillage
{
    public enum BackroomsGenMode
    {
        InfiniteStreaming,
        EnclosedMaze
    }

    public enum MonsterSpawnTriggerMode
    {
        TimeOrDrinks,   // Awaken after time delay OR drinking N almond waters, whichever comes first
        DrinksOnly,     // Awaken only after drinking N almond waters
        TimeOnly,       // Awaken only after time delay
        BothRequired    // Awaken only after BOTH time delay and drinking N almond waters are met
    }

    /// <summary>
    /// Runtime procedural Backrooms generator for House 5.
    /// Features:
    /// 1. Procedural Carpet Floor: Automatically generated with the new Backrooms carpet material,
    ///    seamlessly scaling with Enclosed Maze or dynamically streaming per-chunk in Infinite Mode.
    /// 2. Mode Option: Choose between Infinite Backrooms (Chunk Streaming) and Enclosed 8x8 Maze.
    /// 3. Doorway & Wall Rarity: Long solid walls (~75%), spacious open hallways (~15%), and rare doorways (~10%).
    /// 4. Spread & Rare Almond Water: 1 starter bottle on the desk, with remaining bottles placed rarely in distant dead-ends.
    /// 5. Surreal Junk Piles: Objects from Houses 1-4 (boxes, tetris blocks, stoves, toilets, grocery shelves, chips, tires, wrenches)
    ///    scattered in eerie no-clipped piles in dead-ends and corners.
    /// 6. Infinite Monster Stalking: In Infinite mode, repositions the monster ahead of the player as they explore deep into the complex.
    /// </summary>
    public class BackroomsProceduralGenerator : MonoBehaviour
    {
        public static BackroomsProceduralGenerator Instance { get; private set; }

        [Header("Generation Mode")]
        public BackroomsGenMode generationMode = BackroomsGenMode.InfiniteStreaming;

        [Header("Infinite Streaming Settings")]
        [Tooltip("Number of rooms/cells per chunk axis (e.g. 4 means 4x4 = 16 rooms per chunk)")]
        public int chunkSize = 4;
        [Tooltip("Active chunk radius around the player")]
        public int renderDistanceChunks = 2;

        [Header("Enclosed Maze Settings")]
        [Range(4, 32)] public int gridWidth = 8;
        [Range(4, 32)] public int gridDepth = 8;

        [Header("Shared Settings")]
        public float scaleMultiplier = 1.6f;
        public float cellSize = 4.8f;
        public int seed = 42;
        public bool generateOnStart = false;
        public bool randomizeSeedOnStart = true;

        [Header("Procedural Probabilities & Rarity")]
        [Range(0.01f, 0.50f), Tooltip("Chance of doorway arches (Default: 0.10 = 10% rare doorways)")]
        public float doorwayChance = 0.30f;
        // Open + doorway must stay above 0.5: below that, every connected area of a grid maze is a closed pocket
        // (0.25 + 0.10 made the "infinite" Backrooms a dead end around the spawn).
        [Range(0.05f, 0.85f), Tooltip("Chance of wide open hallway spaces without walls. Increase this for MORE EMPTY SPACE! (e.g. 0.35 - 0.60)")]
        public float openSpaceChance = 0.35f;
        [Range(0.02f, 0.50f), Tooltip("Chance for surreal junk piles from Houses 1-4 to spawn in dead-ends (Default: 0.12 = 12%)")]
        public float junkPileChance = 0.12f;

        [Header("Almond Water Frequency & Spawning")]
        [Range(0.01f, 0.40f), Tooltip("Spawn chance for Almond Water in rooms/dead-ends in Infinite Mode (Increase this slider for MORE COMMON Almond Water! e.g. 0.10 - 0.25. Default: 0.05)")]
        public float almondWaterSpawnChance = 0.05f;
        [Range(1, 25), Tooltip("Total Almond Water bottles spawned across rooms in Enclosed Maze Mode (Increase for more bottles! Default: 5)")]
        public int enclosedAlmondBottlesCount = 5;

        [Header("Modular Templates (from Backrooms.blend)")]
        public GameObject solidWallTemplate;
        public GameObject doorwayWallTemplate;
        public GameObject ceilingTemplate;
        public GameObject ceilingLightTemplate;
        public Material carpetMaterial;
        public GameObject deskTemplate;
        public GameObject drawerTemplate;
        public GameObject flashlightTemplate;
        public GameObject almondWaterTemplate;
        public GameObject monsterPrefab;

        [Header("Surreal House Junk Props (Houses 1-4)")]
        public GameObject[] house1Props; // Storage: Box, Trash, L Block, T Block, Tetris Line
        public GameObject[] house2Props; // Residential: Pan, Stove, Toilet, Window
        public GameObject[] house3Props; // Shop: Shelf, Bottle, Chips, Leek, Basket
        public GameObject[] house4Props; // Mechanic: Wheel, Rim, Toolbox, Screwdriver, Wrench

        [Header("Backrooms Skybox & Void Settings")]
        [Tooltip("Custom skybox material for the Backrooms. Drag and drop any skybox material here! If left null, a dark liminal void skybox is automatically used.")]
        public Material customBackroomsSkybox;
        [Tooltip("Dark void background color when looking into distant unrendered chunks or missing walls.")]
        public Color backroomsVoidColor = new Color(0.035f, 0.033f, 0.028f, 1f);
        [Tooltip("Camera clear flags in Backrooms. Skybox keeps skybox material; SolidColor forces pure dark void color.")]
        public CameraClearFlags backroomsCameraClearFlags = CameraClearFlags.Skybox;
        [Tooltip("Automatically restore Village skybox, fog, and lighting when exiting the Backrooms.")]
        public bool autoRestoreVillageEnvironment = true;

        [Header("Backrooms Fog & Visibility")]
        [Tooltip("Enable liminal fog to blend distant unrendered chunks seamlessly into the void.")]
        public bool enableBackroomsFog = true;
        [Tooltip("Fog mode to use for Backrooms (Linear allows clear visibility nearby with smooth distant falloff).")]
        public FogMode backroomsFogMode = FogMode.Linear;
        [Tooltip("Distance where fog starts. Within this distance, rooms are 100% clear and bright!")]
        public float backroomsFogStart = 22f;
        [Tooltip("Distance where fog reaches full density, fading unrendered chunks into the void.")]
        public float backroomsFogEnd = 70f;
        [Tooltip("Color of the distant void fog.")]
        public Color backroomsFogColor = new Color(0.045f, 0.042f, 0.035f, 1f);
        [Range(0.001f, 0.2f), Tooltip("Density of the Backrooms fog (if using Exponential or Exp2).")]
        public float backroomsFogDensity = 0.012f;

        [Header("Backrooms Lighting & Visibility")]
        [Range(0.2f, 1f), Tooltip("Percentage of rooms with fluorescent ceiling lights. Default 0.65 (65% lit, 35% darker corners/patches).")]
        public float ceilingLightChance = 0.65f;
        [Range(0f, 0.5f), Tooltip("Percentage of ceiling lights that flicker erratically.")]
        public float lightFlickerChance = 0.15f;
        [Range(1f, 6f), Tooltip("Intensity of fluorescent ceiling point lights.")]
        public float ceilingLightIntensity = 2.8f;
        [Range(6f, 25f), Tooltip("Range of fluorescent ceiling point lights.")]
        public float ceilingLightRange = 13.5f;
        [Tooltip("Color tint of fluorescent ceiling lights.")]
        public Color ceilingLightColor = new Color(1f, 0.96f, 0.85f, 1f);

        [Header("Backrooms Ambient Lighting")]
        [Tooltip("Enable dedicated ambient light for Backrooms so unlit corners have warm fluorescent bounce instead of pitch black.")]
        public bool enableBackroomsAmbient = true;
        [Tooltip("Ambient light color for the Backrooms.")]
        public Color backroomsAmbientColor = new Color(0.22f, 0.20f, 0.16f, 1f);
        [Range(0.2f, 3f), Tooltip("Ambient light intensity multiplier.")]
        public float backroomsAmbientIntensity = 1.2f;

        // Environment Restoration Cache
        private Material _villageSkybox;
        private bool _villageFogEnabled;
        private Color _villageFogColor;
        private float _villageFogDensity;
        private FogMode _villageFogMode;
        private float _villageFogStartDistance;
        private float _villageFogEndDistance;
        private UnityEngine.Rendering.AmbientMode _villageAmbientMode;
        private Color _villageAmbientLight;
        private float _villageAmbientIntensity;
        private Color _villageAmbientSkyColor;
        private Color _villageAmbientEquatorColor;
        private Color _villageAmbientGroundColor;
        private CameraClearFlags _villageClearFlags = CameraClearFlags.Skybox;
        private Color _villageBgColor = Color.black;
        private bool _hasCachedVillageEnvironment = false;
        private bool _isBackroomsEnvironmentActive = false;
        private readonly List<Light> _villageSuns = new List<Light>();
        private Material _runtimeVoidSkybox;

        [Header("Monster Spawn & Awakening Settings")]
        [Tooltip("If enabled, the monster does NOT spawn immediately upon entering the Backrooms. It awakens after exploration time or drinking Almond Water.")]
        public bool enableDelayedMonsterSpawn = true;
        [Tooltip("Trigger condition to awaken and spawn the monster.")]
        public MonsterSpawnTriggerMode monsterSpawnCondition = MonsterSpawnTriggerMode.TimeOrDrinks;
        [Tooltip("Seconds after entering the Backrooms before the monster awakens (if Time is enabled). Default 60 seconds.")]
        public float monsterSpawnDelaySeconds = 60f;
        [Range(1, 5), Tooltip("Number of Almond Waters drunk before the monster awakens (e.g. 2 or 3). Default: 2")]
        public int monsterAwakenDrinkCount = 2;
        [Tooltip("Play a terrifying distant roar sound cue when the monster awakens.")]
        public bool playAwakenAudioCue = true;
        [Range(0f, 1f), Tooltip("Volume of the monster awaken roar.")]
        public float awakenAudioVolume = 0.95f;
        [Tooltip("Custom awaken sound clip. If null, a procedural distant guttural roar is synthesized.")]
        public AudioClip customAwakenSound;

        [Header("Monster Runtime State")]
        public bool isMonsterSpawned = false;
        public float timeInsideBackrooms = 0f;
        private AudioSource _awakenAudioSource;

        [Header("Runtime References & UI")]
        public Transform playerTransform;
        public BackroomsMonsterAI activeMonsterAI;
        public Text modeButtonText;

        // Infinite Mode State
        private readonly Dictionary<Vector2Int, GameObject> _activeChunks = new Dictionary<Vector2Int, GameObject>();
        private readonly Queue<Vector2Int> _chunkLoadQueue = new Queue<Vector2Int>();
        private readonly HashSet<Vector2Int> _queuedCoords = new HashSet<Vector2Int>();
        private Coroutine _chunkQueueCoroutine;
        private Vector2Int _lastPlayerChunk = new Vector2Int(-99999, -99999);
        private GameObject _infiniteChunksRoot;
        private GameObject _starterFoyerObj;

        // Enclosed Mode State
        private GameObject _enclosedMazeRoot;

        // Runtime Stats
        public int totalBottlesSpawned = 0;
        public int totalJunkPilesSpawned = 0;
        public int activeChunkCount => _activeChunks.Count;

        public bool isLoaded { get; private set; } = false;
        public bool isPlayerInsideBackrooms = false;

        private void Awake()
        {
            Instance = this;
            generateOnStart = false;
            isPlayerInsideBackrooms = false;
            isMonsterSpawned = false;
            timeInsideBackrooms = 0f;
        }

        private void Start()
        {
            float mult = Mathf.Max(1.0f, scaleMultiplier);
            cellSize = 3.0f * mult;

            LocatePlayer();
            ResolvePropTemplates();

#if UNITY_EDITOR
            if (customBackroomsSkybox == null)
            {
                customBackroomsSkybox = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Houses/GeneratedMaterials/M_BackroomsDarkSkybox.mat");
            }
#endif

            generateOnStart = false;

            // Cache village environment if player starts in the village (near 0,0,0)
            bool isPlayerInVillage = playerTransform != null && Vector3.Distance(playerTransform.position, Vector3.zero) < 500f;
            if (isPlayerInVillage)
            {
                CacheVillageEnvironment();
                isPlayerInsideBackrooms = false;
                isMonsterSpawned = false;
                timeInsideBackrooms = 0f;
                if (activeMonsterAI != null && enableDelayedMonsterSpawn)
                {
                    activeMonsterAI.gameObject.SetActive(false);
                }
            }
            else
            {
                // Started inside the Backrooms dimension directly
                isPlayerInsideBackrooms = true;
                timeInsideBackrooms = 0f;
                ApplyBackroomsEnvironment();
                if (!isLoaded)
                {
                    if (randomizeSeedOnStart) seed = Random.Range(1000, 999999);
                    GenerateMaze();
                }
            }

            UpdateModeButtonUI();
        }

        public void OnPlayerEnterBackrooms()
        {
            isPlayerInsideBackrooms = true;
            timeInsideBackrooms = 0f;

            if (enableDelayedMonsterSpawn)
            {
                isMonsterSpawned = false;
                if (activeMonsterAI != null)
                {
                    activeMonsterAI.gameObject.SetActive(false);
                }
            }
            else
            {
                isMonsterSpawned = true;
                if (activeMonsterAI != null)
                {
                    activeMonsterAI.gameObject.SetActive(true);
                }
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            LocatePlayer();
            ResolvePropTemplates();
            ApplyBackroomsEnvironment();

            if (!isLoaded)
            {
                if (randomizeSeedOnStart)
                    seed = Random.Range(1000, 999999);
                GenerateMaze();
            }
            else if (generationMode == BackroomsGenMode.InfiniteStreaming)
            {
                if (_chunkQueueCoroutine == null && gameObject.activeInHierarchy)
                {
                    _chunkQueueCoroutine = StartCoroutine(ProcessChunkQueueRoutine());
                }
                UpdateInfiniteChunks(true);
            }

            UpdateModeButtonUI();
        }

        public void OnPlayerExitBackrooms()
        {
            isPlayerInsideBackrooms = false;
            timeInsideBackrooms = 0f;
            DespawnMonster();
            if (_chunkQueueCoroutine != null)
            {
                StopCoroutine(_chunkQueueCoroutine);
                _chunkQueueCoroutine = null;
            }
            _chunkLoadQueue.Clear();
            _queuedCoords.Clear();

            if (generationMode == BackroomsGenMode.InfiniteStreaming)
            {
                CleanupAll();
            }

            if (autoRestoreVillageEnvironment)
            {
                RestoreVillageEnvironment();
            }

            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (_chunkQueueCoroutine != null)
            {
                StopCoroutine(_chunkQueueCoroutine);
                _chunkQueueCoroutine = null;
            }
            _chunkLoadQueue.Clear();
            _queuedCoords.Clear();

            if (_isBackroomsEnvironmentActive && autoRestoreVillageEnvironment)
            {
                RestoreVillageEnvironment();
            }
        }

        private void OnDestroy()
        {
            if (_isBackroomsEnvironmentActive && autoRestoreVillageEnvironment)
            {
                RestoreVillageEnvironment();
            }
        }

        private void OnApplicationQuit()
        {
            if (_isBackroomsEnvironmentActive && autoRestoreVillageEnvironment)
            {
                RestoreVillageEnvironment();
            }
        }

        public void LocatePlayer()
        {
            if (playerTransform != null) return;

            if (Camera.main != null)
            {
                // The head, not the rig: in the XR Interaction Simulator, WASD moves the headset and leaves
                // the rig behind, so streaming around the rig stopped generating rooms where the player went.
                playerTransform = Camera.main.transform;
            }
            else
            {
                var pc = FindFirstObjectByType<CharacterController>();
                if (pc != null) playerTransform = pc.transform;
            }
        }

        private void ResolvePropTemplates()
        {
            // If prop arrays were not wired in the inspector, auto-discover from Backrooms_Junk_Templates
            Transform junkRoot = transform.Find("Backrooms_Junk_Templates");
            if (junkRoot != null)
            {
                List<GameObject> h1 = new List<GameObject>();
                List<GameObject> h2 = new List<GameObject>();
                List<GameObject> h3 = new List<GameObject>();
                List<GameObject> h4 = new List<GameObject>();

                foreach (Transform child in junkRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (child == junkRoot) continue;
                    string n = child.name;

                    if (n.Contains("Box") || n.Contains("Trash") || n.Contains("Block") || n.Contains("Tetris"))
                    {
                        if (!h1.Contains(child.gameObject)) h1.Add(child.gameObject);
                    }
                    else if (n.Contains("Stove") || n.Contains("Pan") || n.Contains("Toilet") || n.Contains("Window"))
                    {
                        if (!h2.Contains(child.gameObject)) h2.Add(child.gameObject);
                    }
                    else if (n.Contains("Shelf") || n.Contains("Chips") || n.Contains("Bottle") || n.Contains("Basket") || n.Contains("Leak") || n.Contains("Leek"))
                    {
                        if (!h3.Contains(child.gameObject)) h3.Add(child.gameObject);
                    }
                    else if (n.Contains("Wheel") || n.Contains("Rim") || n.Contains("Tire") || n.Contains("Wrench") || n.Contains("Screw") || n.Contains("Drawer"))
                    {
                        if (!h4.Contains(child.gameObject)) h4.Add(child.gameObject);
                    }
                }

                if ((house1Props == null || house1Props.Length == 0) && h1.Count > 0) house1Props = h1.ToArray();
                if ((house2Props == null || house2Props.Length == 0) && h2.Count > 0) house2Props = h2.ToArray();
                if ((house3Props == null || house3Props.Length == 0) && h3.Count > 0) house3Props = h3.ToArray();
                if ((house4Props == null || house4Props.Length == 0) && h4.Count > 0) house4Props = h4.ToArray();
            }
        }

        public void GenerateMaze()
        {
            CleanupAll();

            float mult = Mathf.Max(1.0f, scaleMultiplier);
            cellSize = 3.0f * mult;

            if (generationMode == BackroomsGenMode.InfiniteStreaming)
            {
                StartInfiniteMode();
            }
            else
            {
                GenerateEnclosedMaze();
            }

            isLoaded = true;
            UpdateModeButtonUI();
        }

        public void RegenerateMaze(int? newSeed = null)
        {
            if (newSeed.HasValue) seed = newSeed.Value;
            else seed = Random.Range(1000, 999999);

            GenerateMaze();
        }

        public void SetMode(BackroomsGenMode mode)
        {
            generationMode = mode;
            GenerateMaze();
        }

        public void ToggleMode()
        {
            if (generationMode == BackroomsGenMode.InfiniteStreaming)
                generationMode = BackroomsGenMode.EnclosedMaze;
            else
                generationMode = BackroomsGenMode.InfiniteStreaming;

            GenerateMaze();
        }

        private void UpdateModeButtonUI()
        {
            if (modeButtonText != null)
            {
                modeButtonText.text = generationMode == BackroomsGenMode.InfiniteStreaming
                    ? "MODE: INFINITE STREAMING (CLICK FOR ENCLOSED)"
                    : "MODE: ENCLOSED MAZE (CLICK FOR INFINITE)";
            }
        }

        private void CleanupAll()
        {
            if (_chunkQueueCoroutine != null)
            {
                StopCoroutine(_chunkQueueCoroutine);
                _chunkQueueCoroutine = null;
            }
            _chunkLoadQueue.Clear();
            _queuedCoords.Clear();

            // Remove previous generated roots
            Transform oldGen = transform.Find("Generated_Cells");
            if (oldGen != null) DestroyObjectDirect(oldGen.gameObject);

            if (_infiniteChunksRoot != null) DestroyObjectDirect(_infiniteChunksRoot);
            if (_enclosedMazeRoot != null) DestroyObjectDirect(_enclosedMazeRoot);
            if (_starterFoyerObj != null) DestroyObjectDirect(_starterFoyerObj);

            _activeChunks.Clear();
            _lastPlayerChunk = new Vector2Int(-99999, -99999);
            totalBottlesSpawned = 0;
            totalJunkPilesSpawned = 0;
            isLoaded = false;
            if (enableDelayedMonsterSpawn)
            {
                DespawnMonster();
            }
        }

        private void DestroyObjectDirect(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private void Update()
        {
            if (!isLoaded || !isPlayerInsideBackrooms) return;

            // Only exit backrooms if player was moved or teleported back to the Village (near 0,0,0)
            if (playerTransform != null && Vector3.Distance(playerTransform.position, Vector3.zero) < 300f)
            {
                OnPlayerExitBackrooms();
                return;
            }

            // Delayed monster awakening timer
            if (enableDelayedMonsterSpawn && !isMonsterSpawned)
            {
                timeInsideBackrooms += Time.deltaTime;
                CheckMonsterSpawnCondition();
            }

            if (_isBackroomsEnvironmentActive)
            {
                Camera cam = Camera.main;
                if (cam != null && (cam.backgroundColor != backroomsVoidColor || cam.clearFlags != backroomsCameraClearFlags))
                {
                    cam.backgroundColor = backroomsVoidColor;
                    cam.clearFlags = backroomsCameraClearFlags;
                }
            }

            if (generationMode == BackroomsGenMode.InfiniteStreaming)
            {
                UpdateInfiniteChunks();
                if (isMonsterSpawned)
                {
                    UpdateMonsterStalking();
                }
            }
        }

        // =========================================================================
        // INFINITE STREAMING SYSTEM
        // =========================================================================

        private void StartInfiniteMode()
        {
            _infiniteChunksRoot = new GameObject("Infinite_Backrooms_World");
            _infiniteChunksRoot.transform.SetParent(transform, false);

            float mult = Mathf.Max(1.0f, scaleMultiplier);

            // Spawn starter room (desk, flashlight, 1 starter almond water)
            SetupSpawnFoyer(mult, _infiniteChunksRoot.transform);

            // Start queue worker coroutine
            if (_chunkQueueCoroutine != null) StopCoroutine(_chunkQueueCoroutine);
            _chunkQueueCoroutine = StartCoroutine(ProcessChunkQueueRoutine());

            // Initial chunk generation (synchronously builds immediate 3x3 chunks, queues periphery)
            UpdateInfiniteChunks(true);

            // Setup monster hidden out of line of sight
            SetupMonster(mult);

            Debug.Log($"[BackroomsProcGen] Started Infinite Streaming Mode (Seed: {seed}, ChunkSize: {chunkSize}x{chunkSize}).");
        }

        private void UpdateInfiniteChunks(bool forceImmediate = false)
        {
            if (playerTransform == null)
            {
                LocatePlayer();
                if (playerTransform == null) return;
            }

            // CRITICAL GUARD: Never stream chunks if player is in the Village!
            if (!isPlayerInsideBackrooms || Vector3.Distance(playerTransform.position, Vector3.zero) < 300f)
            {
                return;
            }

            Vector3 localP = transform.InverseTransformPoint(playerTransform.position);
            float chunkWorldSize = chunkSize * cellSize;

            int currentChunkX = Mathf.FloorToInt((localP.x + (cellSize * 0.5f)) / chunkWorldSize);
            int currentChunkZ = Mathf.FloorToInt((localP.z + (cellSize * 0.5f)) / chunkWorldSize);
            Vector2Int currentChunk = new Vector2Int(currentChunkX, currentChunkZ);

            if (!forceImmediate && currentChunk == _lastPlayerChunk) return;
            _lastPlayerChunk = currentChunk;

            // Collect missing chunks in render distance
            List<Vector2Int> missingChunks = new List<Vector2Int>();
            for (int dx = -renderDistanceChunks; dx <= renderDistanceChunks; dx++)
            {
                for (int dz = -renderDistanceChunks; dz <= renderDistanceChunks; dz++)
                {
                    Vector2Int coord = new Vector2Int(currentChunkX + dx, currentChunkZ + dz);
                    if (!_activeChunks.ContainsKey(coord) && !_queuedCoords.Contains(coord))
                    {
                        // If forceImmediate (e.g. entering backrooms), immediately build 3x3 core
                        if (forceImmediate && Mathf.Abs(dx) <= 1 && Mathf.Abs(dz) <= 1)
                        {
                            GameObject chunkObj = GenerateChunk(coord.x, coord.y);
                            _activeChunks[coord] = chunkObj;
                        }
                        else
                        {
                            missingChunks.Add(coord);
                        }
                    }
                }
            }

            // Sort missing chunks by proximity to player so nearest chunks generate first
            missingChunks.Sort((a, b) =>
            {
                int distA = Mathf.Max(Mathf.Abs(a.x - currentChunkX), Mathf.Abs(a.y - currentChunkZ));
                int distB = Mathf.Max(Mathf.Abs(b.x - currentChunkX), Mathf.Abs(b.y - currentChunkZ));
                return distA.CompareTo(distB);
            });

            for (int i = 0; i < missingChunks.Count; i++)
            {
                _queuedCoords.Add(missingChunks[i]);
                _chunkLoadQueue.Enqueue(missingChunks[i]);
            }

            // Unload distant chunks
            List<Vector2Int> toUnload = new List<Vector2Int>();
            int maxDist = renderDistanceChunks + 1;

            foreach (var kvp in _activeChunks)
            {
                if (Mathf.Abs(kvp.Key.x - currentChunkX) > maxDist || Mathf.Abs(kvp.Key.y - currentChunkZ) > maxDist)
                {
                    toUnload.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toUnload.Count; i++)
            {
                Vector2Int coord = toUnload[i];
                if (_activeChunks.TryGetValue(coord, out GameObject go))
                {
                    if (go != null) DestroyObjectDirect(go);
                    _activeChunks.Remove(coord);
                }
            }
        }

        private IEnumerator ProcessChunkQueueRoutine()
        {
            while (true)
            {
                while (_chunkLoadQueue.Count > 0)
                {
                    if (!isPlayerInsideBackrooms || _infiniteChunksRoot == null)
                    {
                        _chunkLoadQueue.Clear();
                        _queuedCoords.Clear();
                        break;
                    }

                    Vector2Int coord = _chunkLoadQueue.Dequeue();
                    _queuedCoords.Remove(coord);

                    if (_activeChunks.ContainsKey(coord)) continue;

                    // Skip if player moved away from this chunk while it was queued
                    if (Mathf.Abs(coord.x - _lastPlayerChunk.x) > renderDistanceChunks + 1 ||
                        Mathf.Abs(coord.y - _lastPlayerChunk.y) > renderDistanceChunks + 1)
                    {
                        continue;
                    }

                    GameObject chunkObj = GenerateChunk(coord.x, coord.y);
                    _activeChunks[coord] = chunkObj;

                    // Yield 1 frame between chunk instantiations to prevent any hitching or freezing
                    yield return null;
                }

                yield return new WaitForSeconds(0.04f);
            }
        }

        private GameObject GenerateChunk(int cx, int cz)
        {
            GameObject chunkObj = new GameObject($"Chunk_{cx}_{cz}");
            chunkObj.transform.SetParent(_infiniteChunksRoot.transform, false);

            float mult = Mathf.Max(1.0f, scaleMultiplier);
            float chunkWorldSize = chunkSize * cellSize;

            // 1. Procedural Carpet Floor for this chunk
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(chunkObj.transform, false);

            float floorCenterX = (cx * chunkSize + (chunkSize * 0.5f) - 0.5f) * cellSize;
            float floorCenterZ = (cz * chunkSize + (chunkSize * 0.5f) - 0.5f) * cellSize;
            floor.transform.localPosition = new Vector3(floorCenterX, -0.5f, floorCenterZ);
            floor.transform.localScale = new Vector3(chunkWorldSize, 1.0f, chunkWorldSize);

            if (carpetMaterial != null)
                floor.GetComponent<Renderer>().sharedMaterial = carpetMaterial;

            Quaternion baseWallRot = solidWallTemplate != null ? solidWallTemplate.transform.localRotation : Quaternion.Euler(-90f, 0f, 0f);
            Vector3 baseWallScale = solidWallTemplate != null ? solidWallTemplate.transform.localScale * mult : Vector3.one * mult;
            Quaternion baseCeilRot = ceilingTemplate != null ? ceilingTemplate.transform.localRotation : Quaternion.Euler(-90f, 0f, 0f);

            float wallCenterY = 1.45f * mult;
            float ceilingY = 2.95f * mult;

            int startX = cx * chunkSize;
            int startZ = cz * chunkSize;

            for (int lx = 0; lx < chunkSize; lx++)
            {
                for (int lz = 0; lz < chunkSize; lz++)
                {
                    int x = startX + lx;
                    int z = startZ + lz;

                    Vector3 cellCenter = new Vector3(x * cellSize, 0f, z * cellSize);

                    // A. Ceiling & Ceiling Light
                    uint ceilHash = HashCoords(seed, x, z, 2);
                    float lightRoll = (ceilHash % 1000) / 1000f;
                    bool isSpawnCell = (x == 0 && z == 0);
                    bool useLight = (isSpawnCell || (lightRoll < ceilingLightChance)) && (ceilingLightTemplate != null);
                    GameObject cPrefab = useLight ? ceilingLightTemplate : ceilingTemplate;

                    if (cPrefab != null)
                    {
                        GameObject ceil = Instantiate(cPrefab, chunkObj.transform);
                        ceil.name = useLight ? $"CeilLight_{x}_{z}" : $"Ceil_{x}_{z}";
                        ceil.transform.localPosition = cellCenter + new Vector3(0f, ceilingY, 0f);
                        ceil.transform.localRotation = baseCeilRot;
                        ceil.transform.localScale = cPrefab.transform.localScale * mult;
                        ceil.SetActive(true);

                        if (useLight)
                        {
                            Light pt = ceil.GetComponentInChildren<Light>();
                            if (pt == null)
                            {
                                GameObject ptGO = new GameObject("Fluorescent_Light");
                                ptGO.transform.SetParent(ceil.transform, false);
                                ptGO.transform.localPosition = new Vector3(0f, 0f, -0.25f);
                                pt = ptGO.AddComponent<Light>();
                                pt.type = LightType.Point;
                            }
                            pt.color = ceilingLightColor;
                            pt.intensity = ceilingLightIntensity;
                            pt.range = ceilingLightRange;
                            pt.shadows = LightShadows.None;

                            BackroomsFlickerLight fl = ceil.GetComponent<BackroomsFlickerLight>();
                            if (fl == null) fl = ceil.AddComponent<BackroomsFlickerLight>();
                            fl.pointLight = pt;
                            fl.isFlickeringLight = (lightRoll < lightFlickerChance);
                        }
                    }

                    // B. East Wall (Vertical Edge between x and x+1)
                    EdgeType eastEdge = GetEdgeType(seed, x, z, 0);
                    if (eastEdge == EdgeType.Solid && solidWallTemplate != null)
                    {
                        Vector3 wPos = cellCenter + new Vector3(cellSize * 0.5f, wallCenterY, 0f);
                        SpawnWallPiece(solidWallTemplate, chunkObj.transform, wPos, 90f, baseWallRot, baseWallScale);
                    }
                    else if (eastEdge == EdgeType.Doorway && doorwayWallTemplate != null)
                    {
                        Vector3 dPos = cellCenter + new Vector3(cellSize * 0.5f, wallCenterY, 0f);
                        SpawnWallPiece(doorwayWallTemplate, chunkObj.transform, dPos, 90f, baseWallRot, baseWallScale);
                    }

                    // C. North Wall (Horizontal Edge between z and z+1)
                    EdgeType northEdge = GetEdgeType(seed, x, z, 1);
                    if (northEdge == EdgeType.Solid && solidWallTemplate != null)
                    {
                        Vector3 wPos = cellCenter + new Vector3(0f, wallCenterY, cellSize * 0.5f);
                        SpawnWallPiece(solidWallTemplate, chunkObj.transform, wPos, 0f, baseWallRot, baseWallScale);
                    }
                    else if (northEdge == EdgeType.Doorway && doorwayWallTemplate != null)
                    {
                        Vector3 dPos = cellCenter + new Vector3(0f, wallCenterY, cellSize * 0.5f);
                        SpawnWallPiece(doorwayWallTemplate, chunkObj.transform, dPos, 0f, baseWallRot, baseWallScale);
                    }

                    // D. Almond Water Spawning (configurable via almondWaterSpawnChance slider)
                    if (x != 0 || z != 0)
                    {
                        if (Mathf.Abs(x) > 1 || Mathf.Abs(z) > 1)
                        {
                            uint bottleHash = HashCoords(seed, x, z, 3);
                            int chanceThreshold = Mathf.RoundToInt(almondWaterSpawnChance * 1000f);
                            if ((bottleHash % 1000) < chanceThreshold && almondWaterTemplate != null)
                            {
                                GameObject b = Instantiate(almondWaterTemplate, chunkObj.transform);
                                b.name = $"Almond_Water_{x}_{z}";
                                Vector3 cornerOff = new Vector3(((bottleHash % 5) - 2) * 0.6f, 0.35f, (((bottleHash >> 4) % 5) - 2) * 0.6f);
                                b.transform.localPosition = cellCenter + cornerOff;
                                b.transform.localRotation = Quaternion.Euler(-90f, (bottleHash % 360), 0f);
                                b.SetActive(true);
                                ConfigureAlmondBottle(b);
                                totalBottlesSpawned++;
                            }
                        }
                    }

                    // E. Surreal House Junk Piles (configurable chance, far from spawn room)
                    if (x != 0 || z != 0)
                    {
                        uint junkHash = HashCoords(seed, x, z, 5);
                        if ((junkHash % 100) < (junkPileChance * 100f))
                        {
                            int pileType = (int)(junkHash % 4);
                            SpawnJunkPile(chunkObj.transform, cellCenter, pileType, junkHash, mult);
                            totalJunkPilesSpawned++;
                        }
                    }
                }
            }

            return chunkObj;
        }

        private enum EdgeType { Open, Doorway, Solid }

        private EdgeType GetEdgeType(int worldSeed, int cellX, int cellZ, int orientation)
        {
            // Keep starter foyer open to adjacent rooms
            if (orientation == 0) // East wall
            {
                if (cellX == 0 && (cellZ == 0 || cellZ == 1)) return EdgeType.Open;
                if (cellX == -1 && (cellZ == 0 || cellZ == 1)) return EdgeType.Open;
            }
            else // North wall
            {
                if (cellZ == 0 && (cellX == 0 || cellX == 1)) return EdgeType.Open;
                if (cellZ == -1 && (cellX == 0 || cellX == 1)) return EdgeType.Open;
            }

            uint h = HashCoords(worldSeed, cellX, cellZ, orientation);
            float val = (h % 10000) / 10000f;

            // Configurable Doorways & Open Space Rarity:
            if (val < openSpaceChance) return EdgeType.Open;
            if (val < (openSpaceChance + doorwayChance)) return EdgeType.Doorway;
            return EdgeType.Solid;
        }

        private static uint HashCoords(int seed, int x, int z, int type)
        {
            uint h = (uint)seed;
            h ^= (uint)(x * 73856093);
            h ^= (uint)(z * 19349663);
            h ^= (uint)(type * 83492791);
            h = (h ^ (h >> 13)) * 0x5bd1e995;
            return h ^ (h >> 15);
        }

        private void UpdateMonsterStalking()
        {
            if (!isPlayerInsideBackrooms || !isMonsterSpawned || activeMonsterAI == null || playerTransform == null) return;
            if (Vector3.Distance(playerTransform.position, Vector3.zero) < 300f) return;
            if (!activeMonsterAI.gameObject.activeInHierarchy) return;

            float distToPlayer = Vector3.Distance(activeMonsterAI.transform.position, playerTransform.position);

            // If player wandered far away in infinite space (> 50m), reposition monster completely out of line of sight!
            if (distToPlayer > 50f)
            {
                Vector3 hiddenPos = FindHiddenSpawnPosition(playerTransform, 22f, 38f);

                CharacterController cc = activeMonsterAI.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                activeMonsterAI.transform.position = hiddenPos;
                if (cc != null) cc.enabled = true;

                activeMonsterAI.patrolWaypoints = new List<Vector3>
                {
                    hiddenPos,
                    hiddenPos + new Vector3(cellSize * 1.5f, 0f, 0f),
                    hiddenPos + new Vector3(cellSize * 1.5f, 0f, cellSize * 1.5f),
                    hiddenPos + new Vector3(0f, 0f, cellSize * 1.5f)
                };
            }
        }

        // =========================================================================
        // ENCLOSED MAZE SYSTEM
        // =========================================================================

        private void GenerateEnclosedMaze()
        {
            _enclosedMazeRoot = new GameObject("Generated_Cells");
            _enclosedMazeRoot.transform.SetParent(transform, false);

            float mult = Mathf.Max(1.0f, scaleMultiplier);

            // 1. Build Labyrinth Graph
            bool[,] hWalls = new bool[gridWidth, gridDepth - 1];
            bool[,] vWalls = new bool[gridWidth - 1, gridDepth];
            BuildEnclosedGraph(hWalls, vWalls);

            // 2. Procedural Carpet Floor for the entire maze
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Procedural_Carpet_Floor";
            floor.transform.SetParent(_enclosedMazeRoot.transform, false);
            float totalWidth = gridWidth * cellSize;
            float totalDepth = gridDepth * cellSize;
            floor.transform.localPosition = new Vector3(totalWidth * 0.5f - (cellSize * 0.5f), -0.5f, totalDepth * 0.5f - (cellSize * 0.5f));
            floor.transform.localScale = new Vector3(totalWidth + 4f, 1.0f, totalDepth + 4f);
            if (carpetMaterial != null) floor.GetComponent<Renderer>().sharedMaterial = carpetMaterial;

            // 3. Build Architecture
            Quaternion baseWallRot = solidWallTemplate != null ? solidWallTemplate.transform.localRotation : Quaternion.Euler(-90f, 0f, 0f);
            Vector3 baseWallScale = solidWallTemplate != null ? solidWallTemplate.transform.localScale * mult : Vector3.one * mult;
            Quaternion baseCeilRot = ceilingTemplate != null ? ceilingTemplate.transform.localRotation : Quaternion.Euler(-90f, 0f, 0f);

            float wallCenterY = 1.45f * mult;
            float ceilingY = 2.95f * mult;

            // Outer Perimeter Solid Walls
            for (int x = 0; x < gridWidth; x++)
            {
                SpawnWallPiece(solidWallTemplate, _enclosedMazeRoot.transform, new Vector3(x * cellSize, wallCenterY, -cellSize * 0.5f), 0f, baseWallRot, baseWallScale);
                SpawnWallPiece(solidWallTemplate, _enclosedMazeRoot.transform, new Vector3(x * cellSize, wallCenterY, (gridDepth - 1) * cellSize + cellSize * 0.5f), 0f, baseWallRot, baseWallScale);
            }
            for (int z = 0; z < gridDepth; z++)
            {
                SpawnWallPiece(solidWallTemplate, _enclosedMazeRoot.transform, new Vector3(-cellSize * 0.5f, wallCenterY, z * cellSize), 90f, baseWallRot, baseWallScale);
                SpawnWallPiece(solidWallTemplate, _enclosedMazeRoot.transform, new Vector3((gridWidth - 1) * cellSize + cellSize * 0.5f, wallCenterY, z * cellSize), 90f, baseWallRot, baseWallScale);
            }

            // Interior Partitions & Ceilings
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridDepth; z++)
                {
                    Vector3 cellCenter = new Vector3(x * cellSize, 0f, z * cellSize);

                    // Ceiling
                    uint ceilHash = (uint)(x * 397 ^ z * 101 ^ seed);
                    float lightRoll = (ceilHash % 1000) / 1000f;
                    bool isSpawnCell = (x == 0 && z == 0);
                    bool useLight = (isSpawnCell || (lightRoll < ceilingLightChance)) && (ceilingLightTemplate != null);
                    GameObject cPrefab = useLight ? ceilingLightTemplate : ceilingTemplate;
                    if (cPrefab != null)
                    {
                        GameObject ceil = Instantiate(cPrefab, _enclosedMazeRoot.transform);
                        ceil.name = useLight ? $"CeilLight_{x}_{z}" : $"Ceil_{x}_{z}";
                        ceil.transform.localPosition = cellCenter + new Vector3(0f, ceilingY, 0f);
                        ceil.transform.localRotation = baseCeilRot;
                        ceil.transform.localScale = cPrefab.transform.localScale * mult;
                        ceil.SetActive(true);

                        if (useLight)
                        {
                            Light pt = ceil.GetComponentInChildren<Light>();
                            if (pt == null)
                            {
                                GameObject ptGO = new GameObject("Fluorescent_Light");
                                ptGO.transform.SetParent(ceil.transform, false);
                                ptGO.transform.localPosition = new Vector3(0f, 0f, -0.25f);
                                pt = ptGO.AddComponent<Light>();
                                pt.type = LightType.Point;
                            }
                            pt.color = ceilingLightColor;
                            pt.intensity = ceilingLightIntensity;
                            pt.range = ceilingLightRange;
                            pt.shadows = LightShadows.None;

                            BackroomsFlickerLight fl = ceil.GetComponent<BackroomsFlickerLight>();
                            if (fl == null) fl = ceil.AddComponent<BackroomsFlickerLight>();
                            fl.pointLight = pt;
                            fl.isFlickeringLight = (lightRoll < lightFlickerChance);
                        }
                    }

                    // Vertical partition (East edge between x and x+1)
                    if (x < gridWidth - 1 && vWalls[x, z])
                    {
                        uint h = HashCoords(seed, x, z, 10);
                        int doorPercent = Mathf.RoundToInt(doorwayChance * 100f);
                        bool useDoor = ((h % 100) < doorPercent) && doorwayWallTemplate != null;
                        GameObject wPrefab = useDoor ? doorwayWallTemplate : solidWallTemplate;
                        SpawnWallPiece(wPrefab, _enclosedMazeRoot.transform, cellCenter + new Vector3(cellSize * 0.5f, wallCenterY, 0f), 90f, baseWallRot, baseWallScale);
                    }

                    // Horizontal partition (North edge between z and z+1)
                    if (z < gridDepth - 1 && hWalls[x, z])
                    {
                        uint h = HashCoords(seed, x, z, 11);
                        int doorPercent = Mathf.RoundToInt(doorwayChance * 100f);
                        bool useDoor = ((h % 100) < doorPercent) && doorwayWallTemplate != null;
                        GameObject wPrefab = useDoor ? doorwayWallTemplate : solidWallTemplate;
                        SpawnWallPiece(wPrefab, _enclosedMazeRoot.transform, cellCenter + new Vector3(0f, wallCenterY, cellSize * 0.5f), 0f, baseWallRot, baseWallScale);
                    }
                }
            }

            // 4. Setup Spawn Foyer
            SetupSpawnFoyer(mult, _enclosedMazeRoot.transform);

            // 5. Spread Out 5 Almond Water Bottles to Distant Corners/Dead-ends
            SpawnEnclosedAlmondWaters(_enclosedMazeRoot.transform);

            // 6. Spawn Surreal House Junk Piles in 6-8 Selected Rooms
            SpawnEnclosedJunkPiles(_enclosedMazeRoot.transform, mult);

            // 7. Setup Monster Patrol
            SetupMonster(mult);

            Debug.Log($"[BackroomsProcGen] Enclosed Maze Generated ({gridWidth}x{gridDepth}, Seed: {seed}).");
        }

        private void BuildEnclosedGraph(bool[,] hWalls, bool[,] vWalls)
        {
            Random.InitState(seed);
            bool[,] visited = new bool[gridWidth, gridDepth];

            for (int x = 0; x < gridWidth; x++)
                for (int z = 0; z < gridDepth - 1; z++)
                    hWalls[x, z] = true;

            for (int x = 0; x < gridWidth - 1; x++)
                for (int z = 0; z < gridDepth; z++)
                    vWalls[x, z] = true;

            Stack<Vector2Int> stack = new Stack<Vector2Int>();
            Vector2Int current = new Vector2Int(0, 0);
            visited[0, 0] = true;
            stack.Push(current);

            Vector2Int[] dirs = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };

            while (stack.Count > 0)
            {
                current = stack.Peek();
                List<int> validDirs = new List<int>();

                for (int d = 0; d < 4; d++)
                {
                    Vector2Int next = current + dirs[d];
                    if (next.x >= 0 && next.x < gridWidth && next.y >= 0 && next.y < gridDepth && !visited[next.x, next.y])
                    {
                        validDirs.Add(d);
                    }
                }

                if (validDirs.Count > 0)
                {
                    int chosen = validDirs[Random.Range(0, validDirs.Count)];
                    Vector2Int next = current + dirs[chosen];

                    if (current.x == next.x)
                    {
                        int z = Mathf.Min(current.y, next.y);
                        hWalls[current.x, z] = false;
                    }
                    else
                    {
                        int x = Mathf.Min(current.x, next.x);
                        vWalls[x, current.y] = false;
                    }

                    visited[next.x, next.y] = true;
                    stack.Push(next);
                }
                else
                {
                    stack.Pop();
                }
            }

            // Keep the 2x2 spawn foyer completely open
            if (gridWidth >= 2 && gridDepth >= 2)
            {
                vWalls[0, 0] = false;
                hWalls[0, 0] = false;
                if (gridWidth > 1) vWalls[0, 1] = false;
                if (gridDepth > 1) hWalls[1, 0] = false;
            }

            // Remove additional interior walls based on openSpaceChance to create large open halls and empty spaces!
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridDepth; z++)
                {
                    if (x < gridWidth - 1 && vWalls[x, z])
                    {
                        uint h = HashCoords(seed, x, z, 77);
                        if (((h % 1000) / 1000f) < openSpaceChance)
                        {
                            vWalls[x, z] = false;
                        }
                    }
                    if (z < gridDepth - 1 && hWalls[x, z])
                    {
                        uint h = HashCoords(seed, x, z, 88);
                        if (((h % 1000) / 1000f) < openSpaceChance)
                        {
                            hWalls[x, z] = false;
                        }
                    }
                }
            }
        }

        private void SpawnEnclosedAlmondWaters(Transform parent)
        {
            if (almondWaterTemplate == null) return;

            List<Vector2Int> candidates = new List<Vector2Int>();
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridDepth; z++)
                {
                    if (x <= 1 && z <= 1) continue; // Skip 2x2 starter spawn foyer
                    candidates.Add(new Vector2Int(x, z));
                }
            }

            // Shuffle deterministically based on seed
            for (int i = 0; i < candidates.Count; i++)
            {
                uint h = HashCoords(seed, candidates[i].x, candidates[i].y, 33);
                int swapIdx = (int)(h % (uint)candidates.Count);
                Vector2Int tmp = candidates[i];
                candidates[i] = candidates[swapIdx];
                candidates[swapIdx] = tmp;
            }

            int countToSpawn = Mathf.Clamp(enclosedAlmondBottlesCount, 1, candidates.Count);
            for (int i = 0; i < countToSpawn; i++)
            {
                Vector2Int c = candidates[i];
                GameObject b = Instantiate(almondWaterTemplate, parent);
                b.name = $"Almond_Water_Distant_{i + 1}";
                Vector3 cellCenter = new Vector3(c.x * cellSize, 0f, c.y * cellSize);
                b.transform.localPosition = cellCenter + new Vector3(0.4f, 0.35f, 0.4f);
                b.transform.localRotation = Quaternion.Euler(-90f, (i * 72f), 0f);
                b.SetActive(true);
                ConfigureAlmondBottle(b);
                totalBottlesSpawned++;
            }
        }

        private void SpawnEnclosedJunkPiles(Transform parent, float mult)
        {
            // Select 6-8 distant rooms to feature surreal junk piles from other houses
            Vector2Int[] pileRooms =
            {
                new Vector2Int(3, 7), // Room A: Mechanic Shop pile
                new Vector2Int(7, 3), // Room B: Residential House pile
                new Vector2Int(1, 5), // Room C: Storage Unit pile
                new Vector2Int(5, 1), // Room D: Convenience Shop pile
                new Vector2Int(6, 6), // Room E: Mixed Mechanic/Storage pile
                new Vector2Int(2, 4), // Room F: Residential/Shop pile
                new Vector2Int(5, 5)  // Room G: Anomaly pile
            };

            for (int i = 0; i < pileRooms.Length; i++)
            {
                Vector2Int r = pileRooms[i];
                if (r.x < gridWidth && r.y < gridDepth)
                {
                    Vector3 cellCenter = new Vector3(r.x * cellSize, 0f, r.y * cellSize);
                    int pileType = i % 4;
                    uint rnd = HashCoords(seed, r.x, r.y, 9);
                    SpawnJunkPile(parent, cellCenter, pileType, rnd, mult);
                    totalJunkPilesSpawned++;
                }
            }
        }

        // =========================================================================
        // SURREAL JUNK PILE SPAWNER (HOUSES 1-4)
        // =========================================================================

        private void SpawnJunkPile(Transform parent, Vector3 cellCenter, int pileType, uint rnd, float mult)
        {
            GameObject[] sourceArray = null;
            string categoryName = "";

            switch (pileType)
            {
                case 0:
                    sourceArray = house1Props;
                    categoryName = "Storage_Junk_Pile";
                    break;
                case 1:
                    sourceArray = house2Props;
                    categoryName = "Residential_Junk_Pile";
                    break;
                case 2:
                    sourceArray = house3Props;
                    categoryName = "Shop_Junk_Pile";
                    break;
                case 3:
                    sourceArray = house4Props;
                    categoryName = "Mechanic_Junk_Pile";
                    break;
            }

            if (sourceArray == null || sourceArray.Length == 0) return;

            GameObject pileRoot = new GameObject(categoryName);
            pileRoot.transform.SetParent(parent, false);

            // Anchor pile toward a quadrant, staying well clear of walls (0.20f of cellSize = ~0.96m)
            float xCorner = ((rnd % 2 == 0) ? 1f : -1f) * (cellSize * 0.20f);
            float zCorner = (((rnd >> 2) % 2 == 0) ? 1f : -1f) * (cellSize * 0.20f);
            Vector3 pileCenter = cellCenter + new Vector3(xCorner, 0f, zCorner);
            pileRoot.transform.localPosition = pileCenter;

            int itemCount = Mathf.Min(sourceArray.Length, 3 + (int)(rnd % 3)); // 3 to 5 items

            for (int i = 0; i < itemCount; i++)
            {
                GameObject template = sourceArray[(i + (int)(rnd >> 4)) % sourceArray.Length];
                if (template == null) continue;

                GameObject item = Instantiate(template, pileRoot.transform);
                item.name = template.name;

                // Subtle messy offset in pile
                float jx = (((rnd + i * 37) % 100) / 100f - 0.5f) * 0.8f;
                float jz = ((((rnd >> 3) + i * 53) % 100) / 100f - 0.5f) * 0.8f;
                float rotY = ((rnd + i * 79) % 360);
                float rotZ = (itemCount > 2 && i % 2 == 1) ? 20f : 0f; // occasional tipped tilt

                item.transform.localPosition = new Vector3(jx, 0.05f, jz);
                item.transform.localRotation = Quaternion.Euler(0f, rotY, rotZ);
                item.transform.localScale = template.transform.localScale;
                item.SetActive(true);
                EnsureStaticCollider(item);

                // Anti-Wall Clipping: Measure the item's renderer bounds to guarantee zero penetration into walls
                float itemExtent = 0.35f;
                Renderer[] rends = item.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rends.Length; r++)
                {
                    Bounds b = rends[r].bounds;
                    float ext = Mathf.Max(b.extents.x, b.extents.z);
                    if (ext > itemExtent) itemExtent = ext;
                }
                itemExtent = Mathf.Clamp(itemExtent, 0.25f, 0.80f);

                // In a 4.8m cell, walls sit at ±(cellSize * 0.5f) = ±2.4m.
                // With wall thickness and margin, safe limit is 2.4m - itemExtent - 0.15m.
                float maxSafeOffset = (cellSize * 0.5f) - itemExtent - 0.15f;
                maxSafeOffset = Mathf.Max(0.4f, maxSafeOffset);

                // Check distance from cell center (walls are relative to cellCenter)
                Vector3 worldOffsetFromCell = (pileCenter + new Vector3(jx, 0f, jz)) - cellCenter;
                float clampedX = Mathf.Clamp(worldOffsetFromCell.x, -maxSafeOffset, maxSafeOffset);
                float clampedZ = Mathf.Clamp(worldOffsetFromCell.z, -maxSafeOffset, maxSafeOffset);

                // Reposition item relative to pileRoot (floor clipping is allowed, wall clipping is prevented)
                item.transform.localPosition = new Vector3(clampedX - xCorner, 0.05f, clampedZ - zCorner);
            }
        }

        // =========================================================================
        // FOYER, MONSTER & PROP HELPERS
        // =========================================================================

        private void SetupSpawnFoyer(float mult, Transform parent)
        {
            _starterFoyerObj = new GameObject("Spawn_Foyer_Desk_Items");
            _starterFoyerObj.transform.SetParent(parent, false);

            if (deskTemplate != null)
            {
                GameObject desk = Instantiate(deskTemplate, _starterFoyerObj.transform);
                desk.name = "Desk";
                desk.transform.localPosition = new Vector3(2.4f, 0.24f, 2.8f);
                desk.SetActive(true);
                EnsureStaticCollider(desk);

                if (drawerTemplate != null)
                {
                    GameObject drawer = Instantiate(drawerTemplate, desk.transform);
                    drawer.name = "Drawer";
                    drawer.transform.localPosition = Vector3.zero;
                    drawer.SetActive(true);
                }
            }

            if (flashlightTemplate != null)
            {
                GameObject flash = Instantiate(flashlightTemplate, _starterFoyerObj.transform);
                flash.name = "Flash Light";
                flash.transform.localPosition = new Vector3(2.2f, 0.72f, 2.7f);
                flash.SetActive(true);
            }

            // Exactly ONE starter Almond Water bottle on the desk
            if (almondWaterTemplate != null)
            {
                GameObject b = Instantiate(almondWaterTemplate, _starterFoyerObj.transform);
                b.name = "Starter_Almond_Water";
                b.transform.localPosition = new Vector3(2.6f, 0.72f, 2.8f);
                b.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                b.SetActive(true);
                ConfigureAlmondBottle(b);
                totalBottlesSpawned++;
            }
        }

        private void SetupMonster(float mult)
        {
            if (activeMonsterAI == null)
            {
                if (monsterPrefab != null)
                {
                    GameObject mObj = Instantiate(monsterPrefab, transform);
                    mObj.name = "Procedural_Monster";
                    activeMonsterAI = mObj.GetComponent<BackroomsMonsterAI>();
                }
                else
                {
                    activeMonsterAI = FindFirstObjectByType<BackroomsMonsterAI>();
                }
            }

            if (activeMonsterAI != null)
            {
                LocatePlayer();
                Vector3 hiddenPos = FindHiddenSpawnPosition(playerTransform, 20f, 35f);

                CharacterController cc = activeMonsterAI.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                activeMonsterAI.transform.position = hiddenPos;
                if (cc != null) cc.enabled = true;

                activeMonsterAI.patrolWaypoints = new List<Vector3>
                {
                    hiddenPos,
                    hiddenPos + new Vector3(cellSize * 1.5f, 0f, 0f),
                    hiddenPos + new Vector3(cellSize * 1.5f, 0f, cellSize * 1.5f),
                    hiddenPos + new Vector3(0f, 0f, cellSize * 1.5f)
                };

                if (enableDelayedMonsterSpawn && !isMonsterSpawned)
                {
                    activeMonsterAI.gameObject.SetActive(false);
                }
                else
                {
                    activeMonsterAI.gameObject.SetActive(true);
                }
            }
        }

        // =========================================================================
        // MONSTER DELAYED SPAWN & AWAKENING SYSTEM
        // =========================================================================

        public void OnAlmondWaterDrunk(int totalDrunk)
        {
            if (!isPlayerInsideBackrooms) return;
            if (enableDelayedMonsterSpawn && !isMonsterSpawned)
            {
                CheckMonsterSpawnCondition();
            }
        }

        public void CheckMonsterSpawnCondition()
        {
            if (isMonsterSpawned) return;

            bool timeMet = timeInsideBackrooms >= monsterSpawnDelaySeconds;
            bool drinksMet = AlmondWaterBottle.TotalDrunkCount >= monsterAwakenDrinkCount;

            bool shouldSpawn = false;
            string reason = "";

            switch (monsterSpawnCondition)
            {
                case MonsterSpawnTriggerMode.TimeOrDrinks:
                    if (drinksMet)
                    {
                        shouldSpawn = true;
                        reason = $"Drank {AlmondWaterBottle.TotalDrunkCount}/{monsterAwakenDrinkCount} Almond Waters";
                    }
                    else if (timeMet)
                    {
                        shouldSpawn = true;
                        reason = $"Exploration time reached {timeInsideBackrooms:F1}s (delay: {monsterSpawnDelaySeconds:F0}s)";
                    }
                    break;

                case MonsterSpawnTriggerMode.DrinksOnly:
                    if (drinksMet)
                    {
                        shouldSpawn = true;
                        reason = $"Drank {AlmondWaterBottle.TotalDrunkCount}/{monsterAwakenDrinkCount} Almond Waters";
                    }
                    break;

                case MonsterSpawnTriggerMode.TimeOnly:
                    if (timeMet)
                    {
                        shouldSpawn = true;
                        reason = $"Exploration time reached {timeInsideBackrooms:F1}s (delay: {monsterSpawnDelaySeconds:F0}s)";
                    }
                    break;

                case MonsterSpawnTriggerMode.BothRequired:
                    if (timeMet && drinksMet)
                    {
                        shouldSpawn = true;
                        reason = $"Both met: Explored {timeInsideBackrooms:F1}s and drank {AlmondWaterBottle.TotalDrunkCount} Almond Waters";
                    }
                    break;
            }

            if (shouldSpawn)
            {
                SpawnMonster(reason);
            }
        }

        public void SpawnMonster(string reason = "Condition Met")
        {
            if (isMonsterSpawned && activeMonsterAI != null && activeMonsterAI.gameObject.activeSelf) return;

            isMonsterSpawned = true;

            float mult = Mathf.Max(1.0f, scaleMultiplier);
            if (activeMonsterAI == null)
            {
                SetupMonster(mult);
            }

            if (activeMonsterAI != null)
            {
                LocatePlayer();
                Vector3 hiddenPos = FindHiddenSpawnPosition(playerTransform, 20f, 35f);

                CharacterController cc = activeMonsterAI.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                activeMonsterAI.transform.position = hiddenPos;
                if (cc != null) cc.enabled = true;

                activeMonsterAI.patrolWaypoints = new List<Vector3>
                {
                    hiddenPos,
                    hiddenPos + new Vector3(cellSize * 1.5f, 0f, 0f),
                    hiddenPos + new Vector3(cellSize * 1.5f, 0f, cellSize * 1.5f),
                    hiddenPos + new Vector3(0f, 0f, cellSize * 1.5f)
                };

                activeMonsterAI.gameObject.SetActive(true);
            }

            PlayMonsterAwakenCue();

            Debug.Log($"[BackroomsProcGen] Monster AWAKENED & SPAWNED! Reason: {reason}");
        }

        public void DespawnMonster()
        {
            isMonsterSpawned = false;
            if (activeMonsterAI != null)
            {
                activeMonsterAI.gameObject.SetActive(false);
            }
        }

        public void PlayMonsterAwakenCue()
        {
            if (!playAwakenAudioCue) return;

            AudioClip clip = customAwakenSound;
            if (clip == null)
            {
                clip = ProceduralAudioGenerator.CreateMonsterAwakenRoar(2.4f);
            }

            if (clip != null)
            {
                Vector3 soundPos = transform.position;
                if (activeMonsterAI != null)
                {
                    soundPos = activeMonsterAI.transform.position;
                }
                else if (playerTransform != null)
                {
                    soundPos = playerTransform.position + playerTransform.forward * 10f;
                }

                if (_awakenAudioSource == null)
                {
                    GameObject audioGo = new GameObject("Monster_Awaken_AudioSource");
                    audioGo.transform.SetParent(transform, false);
                    _awakenAudioSource = audioGo.AddComponent<AudioSource>();
                    _awakenAudioSource.playOnAwake = false;
                    _awakenAudioSource.loop = false;
                    _awakenAudioSource.spatialBlend = 0.4f;
                    _awakenAudioSource.minDistance = 5f;
                    _awakenAudioSource.maxDistance = 80f;
                    _awakenAudioSource.rolloffMode = AudioRolloffMode.Linear;
                }

                _awakenAudioSource.transform.position = soundPos;
                _awakenAudioSource.clip = clip;
                _awakenAudioSource.volume = awakenAudioVolume;
                _awakenAudioSource.Play();
            }
        }

        private void SpawnWallPiece(GameObject prefab, Transform parent, Vector3 localPos, float yawDegrees, Quaternion baseRot, Vector3 baseScale)
        {
            if (prefab == null) return;
            GameObject w = Instantiate(prefab, parent);
            w.transform.localPosition = localPos;
            w.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f) * baseRot;
            w.transform.localScale = baseScale;
            w.SetActive(true);
            EnsureStaticCollider(w);
        }

        private void EnsureStaticCollider(GameObject go)
        {
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.GetComponent<Collider>() == null)
                {
                    mf.gameObject.AddComponent<MeshCollider>();
                }
            }
        }

        private void ConfigureAlmondBottle(GameObject bottleGO)
        {
            // Remove old canvas if cloned
            Transform existingBtn = bottleGO.transform.Find("Bottle_Drink_Button_Canvas");
            if (existingBtn != null) DestroyObjectDirect(existingBtn.gameObject);

            // Remove any conflicting XRSimpleInteractable from bottle root
            XRSimpleInteractable simple = bottleGO.GetComponent<XRSimpleInteractable>();
            if (simple != null)
            {
                if (Application.isPlaying) Destroy(simple);
                else DestroyImmediate(simple);
            }

            // Ensure physical BoxCollider
            BoxCollider bc = bottleGO.GetComponent<BoxCollider>();
            if (bc == null) bc = bottleGO.AddComponent<BoxCollider>();
            MeshFilter mf = bottleGO.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                bc.center = b.center;
                Vector3 s = b.size;
                bc.size = new Vector3(Mathf.Max(s.x, 0.12f), Mathf.Max(s.y, 0.12f), Mathf.Max(s.z, 0.12f));
            }
            else
            {
                bc.center = Vector3.zero;
                bc.size = new Vector3(0.15f, 0.35f, 0.15f);
            }
            bc.isTrigger = false;

            // Ensure Rigidbody
            Rigidbody rb = bottleGO.GetComponent<Rigidbody>();
            if (rb == null) rb = bottleGO.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            Vector3 localUp = bottleGO.transform.InverseTransformDirection(Vector3.up);
            rb.centerOfMass = -localUp * 0.08f;

            // Bottles are copies of the template bottle by the spawn desk. With interpolation on, setting only
            // the transform let physics pull every copy back to the template's spot (a pile at the desk),
            // so move the body itself to where the caller placed the bottle.
            rb.position = bottleGO.transform.position;
            rb.rotation = bottleGO.transform.rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            GrabAndPlaceItem grab = bottleGO.GetComponent<GrabAndPlaceItem>();
            if (grab == null) grab = bottleGO.AddComponent<GrabAndPlaceItem>();
            grab.placementRules.Clear();
            grab.placementRules.Add(new PlacementTargetRule
            {
                targetNameContains = "Desk",
                houseNumber = 5,
                actionNumber = 5
            });
            grab.placementRules.Add(new PlacementTargetRule
            {
                targetNameContains = "Drawer",
                houseNumber = 5,
                actionNumber = 5
            });

            AlmondWaterBottle drink = bottleGO.GetComponent<AlmondWaterBottle>();
            if (drink == null) drink = bottleGO.AddComponent<AlmondWaterBottle>();

            // Ensure XRGrabInteractable
            XRGrabInteractable xri = bottleGO.GetComponent<XRGrabInteractable>();
            if (xri == null) xri = bottleGO.AddComponent<XRGrabInteractable>();
            xri.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            xri.throwOnDetach = true;
            xri.interactionLayers = 1;
            xri.retainTransformParent = false;
            xri.colliders.Clear();
            if (bc != null) xri.colliders.Add(bc);

            // Attach floating drink button
            GameObject btnCanvas = new GameObject("Bottle_Drink_Button_Canvas");
            btnCanvas.transform.SetParent(bottleGO.transform, false);
            btnCanvas.transform.localPosition = new Vector3(0f, 0f, 0.52f);
            btnCanvas.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Canvas c = btnCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            if (Camera.main != null) c.worldCamera = Camera.main;

            btnCanvas.AddComponent<GraphicRaycaster>();
            var tdgr = btnCanvas.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            tdgr.ignoreReversedGraphics = false;

            RectTransform rt = btnCanvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 64);
            btnCanvas.transform.localScale = Vector3.one * 0.0020f;

            GameObject btnGO = new GameObject("Btn_DrinkThisBottle");
            btnGO.transform.SetParent(btnCanvas.transform, false);
            Image img = btnGO.AddComponent<Image>();
            img.color = new Color(0.15f, 0.65f, 0.8f);
            Button btn = btnGO.AddComponent<Button>();
            RectTransform btnRt = btnGO.GetComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(186, 52);

            GameObject txtGO = new GameObject("Text");
            txtGO.transform.SetParent(btnGO.transform, false);
            Text txt = txtGO.AddComponent<Text>();
            txt.text = "DRINK WATER";
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 20;
            txt.color = Color.white;
            RectTransform txtRt = txtGO.GetComponent<RectTransform>();
            txtRt.sizeDelta = btnRt.sizeDelta;

            VillageUIButtonBinding binding = btnGO.AddComponent<VillageUIButtonBinding>();
            binding.commandType = "DrinkAlmond";
            binding.targetAlmondBottle = drink;
        }

        public Vector3 FindHiddenSpawnPosition(Transform playerT, float minDistance = 20f, float maxDistance = 38f)
        {
            if (playerT == null) playerT = this.playerTransform;
            if (playerT == null) return transform.position + new Vector3(cellSize * 4f, 0.05f, cellSize * 4f);

            Vector3 playerWorldPos = playerT.position;
            Vector3 playerForward = playerT.forward;
            playerForward.y = 0f;
            if (playerForward.sqrMagnitude < 0.01f) playerForward = Vector3.forward;
            else playerForward.Normalize();

            Vector3 eyePos = playerWorldPos + Vector3.up * 1.6f;
            if (Camera.main != null) eyePos = Camera.main.transform.position;

            Vector3 localP = transform.InverseTransformPoint(playerWorldPos);
            int currentCellX = Mathf.RoundToInt(localP.x / cellSize);
            int currentCellZ = Mathf.RoundToInt(localP.z / cellSize);

            List<Vector3> rank1HiddenBehind = new List<Vector3>(); // Behind player AND occluded by wall
            List<Vector3> rank2HiddenWall = new List<Vector3>();   // Occluded by wall
            List<Vector3> rank3Behind = new List<Vector3>();       // Behind player (dot < -0.3)

            int maxCellDist = Mathf.CeilToInt(maxDistance / cellSize) + 1;

            for (int dx = -maxCellDist; dx <= maxCellDist; dx++)
            {
                for (int dz = -maxCellDist; dz <= maxCellDist; dz++)
                {
                    int cx = currentCellX + dx;
                    int cz = currentCellZ + dz;

                    Vector3 candidatePos = transform.TransformPoint(new Vector3(cx * cellSize, 0.05f, cz * cellSize));
                    float dist = Vector3.Distance(candidatePos, playerWorldPos);
                    if (dist < minDistance || dist > maxDistance) continue;

                    Vector3 toCandidate = candidatePos - eyePos;
                    Vector3 flatDir = new Vector3(toCandidate.x, 0f, toCandidate.z).normalized;
                    float dot = Vector3.Dot(playerForward, flatDir);

                    // Check line of sight from player eye to candidate head height (1.6m)
                    Vector3 candidateHead = candidatePos + Vector3.up * 1.6f;
                    bool isWallOccluded = false;

                    if (Physics.Linecast(eyePos, candidateHead, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider != null && !hit.collider.isTrigger && !IsPlayerCollider(hit.collider, playerT))
                        {
                            string colName = hit.collider.name;
                            if (colName.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                                colName.IndexOf("Carpet", System.StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                isWallOccluded = true;
                            }
                        }
                    }

                    // Collision clearance check around candidate center
                    Collider[] hits = Physics.OverlapSphere(candidatePos + Vector3.up * 0.9f, 0.35f, ~0, QueryTriggerInteraction.Ignore);
                    bool hasObstacle = false;
                    for (int h = 0; h < hits.Length; h++)
                    {
                        if (hits[h] != null && !hits[h].isTrigger && !IsPlayerCollider(hits[h], playerT))
                        {
                            string n = hits[h].name;
                            if (n.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                                n.IndexOf("Carpet", System.StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                hasObstacle = true;
                                break;
                            }
                        }
                    }
                    if (hasObstacle) continue;

                    // Classify candidate
                    if (isWallOccluded && dot < -0.15f)
                    {
                        rank1HiddenBehind.Add(candidatePos);
                    }
                    else if (isWallOccluded)
                    {
                        rank2HiddenWall.Add(candidatePos);
                    }
                    else if (dot < -0.35f)
                    {
                        rank3Behind.Add(candidatePos);
                    }
                }
            }

            if (rank1HiddenBehind.Count > 0)
            {
                return rank1HiddenBehind[Random.Range(0, rank1HiddenBehind.Count)];
            }
            if (rank2HiddenWall.Count > 0)
            {
                return rank2HiddenWall[Random.Range(0, rank2HiddenWall.Count)];
            }
            if (rank3Behind.Count > 0)
            {
                return rank3Behind[Random.Range(0, rank3Behind.Count)];
            }

            // Fallback: spawn strictly behind player's back
            Vector3 fallback = playerWorldPos - (playerForward * minDistance) + (playerT.right * Random.Range(-4f, 4f));
            fallback.y = 0.05f;
            return fallback;
        }

        private bool IsPlayerCollider(Collider c, Transform playerT)
        {
            if (c == null) return false;
            if (playerT != null && c.transform.IsChildOf(NarinScope.PlayerRig(playerT))) return true;
            return c.CompareTag("Player") ||
                   c.GetComponentInParent<CharacterController>() != null ||
                   c.name.Contains("XR") ||
                   c.name.Contains("Camera");
        }

        // =========================================================================
        // BACKROOMS SKYBOX & ENVIRONMENT MANAGEMENT
        // =========================================================================

        public void CacheVillageEnvironment()
        {
            if (playerTransform != null && Vector3.Distance(playerTransform.position, Vector3.zero) >= 500f && _hasCachedVillageEnvironment)
            {
                // Already inside Backrooms and already cached, don't overwrite with Backrooms settings!
                return;
            }

            _villageSkybox = RenderSettings.skybox;
            _villageFogEnabled = RenderSettings.fog;
            _villageFogColor = RenderSettings.fogColor;
            _villageFogDensity = RenderSettings.fogDensity;
            _villageFogMode = RenderSettings.fogMode;
            _villageFogStartDistance = RenderSettings.fogStartDistance;
            _villageFogEndDistance = RenderSettings.fogEndDistance;

            _villageAmbientMode = RenderSettings.ambientMode;
            _villageAmbientLight = RenderSettings.ambientLight;
            _villageAmbientIntensity = RenderSettings.ambientIntensity;
            _villageAmbientSkyColor = RenderSettings.ambientSkyColor;
            _villageAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            _villageAmbientGroundColor = RenderSettings.ambientGroundColor;

            Camera cam = Camera.main;
            if (cam != null)
            {
                _villageClearFlags = cam.clearFlags;
                _villageBgColor = cam.backgroundColor;
            }

            _hasCachedVillageEnvironment = true;
        }

        [ContextMenu("Apply Backrooms Environment (Preview)")]
        public void ApplyBackroomsEnvironment()
        {
            if (!_hasCachedVillageEnvironment)
            {
                CacheVillageEnvironment();
            }

            // 1. Determine which skybox material to apply
            Material targetSkybox = customBackroomsSkybox;
            if (targetSkybox == null)
            {
#if UNITY_EDITOR
                targetSkybox = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Houses/GeneratedMaterials/M_BackroomsDarkSkybox.mat");
                if (targetSkybox != null)
                {
                    customBackroomsSkybox = targetSkybox;
                }
#endif
            }

            if (targetSkybox == null)
            {
                if (_runtimeVoidSkybox == null)
                {
                    Shader hShader = Shader.Find("Skybox/Horizontal Skybox");
                    if (hShader != null)
                    {
                        _runtimeVoidSkybox = new Material(hShader);
                        _runtimeVoidSkybox.name = "Backrooms_Runtime_VoidSkybox";
                        _runtimeVoidSkybox.SetColor("_Color1", backroomsVoidColor * 0.9f);
                        _runtimeVoidSkybox.SetColor("_Color2", backroomsVoidColor * 1.3f);
                        _runtimeVoidSkybox.SetColor("_Color3", backroomsVoidColor * 0.7f);
                        _runtimeVoidSkybox.SetFloat("_Intensity", 0.35f);
                    }
                    else
                    {
                        Shader procShader = Shader.Find("Skybox/Procedural");
                        if (procShader != null)
                        {
                            _runtimeVoidSkybox = new Material(procShader);
                            _runtimeVoidSkybox.name = "Backrooms_Runtime_VoidSkybox";
                            _runtimeVoidSkybox.SetColor("_SkyTint", backroomsVoidColor);
                            _runtimeVoidSkybox.SetColor("_GroundColor", backroomsVoidColor);
                            _runtimeVoidSkybox.SetFloat("_AtmosphereThickness", 0.2f);
                            _runtimeVoidSkybox.SetFloat("_Exposure", 0.25f);
                        }
                    }
                }
                targetSkybox = _runtimeVoidSkybox;
            }

            if (targetSkybox != null)
            {
                RenderSettings.skybox = targetSkybox;
            }

            // 2. Apply Fog (Clear visibility nearby, smooth fade into void in far distance)
            RenderSettings.fog = enableBackroomsFog;
            if (enableBackroomsFog)
            {
                RenderSettings.fogColor = backroomsFogColor;
                RenderSettings.fogMode = backroomsFogMode;
                if (backroomsFogMode == FogMode.Linear)
                {
                    RenderSettings.fogStartDistance = backroomsFogStart;
                    RenderSettings.fogEndDistance = backroomsFogEnd;
                }
                else
                {
                    RenderSettings.fogDensity = backroomsFogDensity;
                }
            }

            // 3. Apply Ambient Lighting (sickly warm fluorescent bounce so corners aren't pitch black)
            if (enableBackroomsAmbient)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = backroomsAmbientColor;
                RenderSettings.ambientIntensity = backroomsAmbientIntensity;
            }

            // 4. Camera background & clear flags
            Camera[] allCams = Camera.allCameras;
            if (allCams != null && allCams.Length > 0)
            {
                foreach (Camera c in allCams)
                {
                    if (c == null) continue;
                    c.backgroundColor = backroomsVoidColor;
                    c.clearFlags = backroomsCameraClearFlags;
                }
            }
            else if (Camera.main != null)
            {
                Camera.main.backgroundColor = backroomsVoidColor;
                Camera.main.clearFlags = backroomsCameraClearFlags;
            }

            // The shared village's sun would light the Backrooms through the ceilings (Narin's project hid it with
            // 45 m shadows; the village renders 2.5 m), so switch every sun off while inside.
            if (!_isBackroomsEnvironmentActive)
            {
                _villageSuns.Clear();
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l.type != LightType.Directional || !l.enabled) continue;
                    _villageSuns.Add(l);
                    l.enabled = false;
                }
            }

            _isBackroomsEnvironmentActive = true;
            try
            {
                DynamicGI.UpdateEnvironment();
            }
            catch { }
            Debug.Log("[BackroomsProcGen] Applied Backrooms Lighting & Environment.");
        }

        [ContextMenu("Restore Village Environment")]
        public void RestoreVillageEnvironment()
        {
            if (!_isBackroomsEnvironmentActive || !_hasCachedVillageEnvironment) return;

            RenderSettings.skybox = _villageSkybox;
            RenderSettings.fog = _villageFogEnabled;
            RenderSettings.fogColor = _villageFogColor;
            RenderSettings.fogDensity = _villageFogDensity;
            RenderSettings.fogMode = _villageFogMode;
            RenderSettings.fogStartDistance = _villageFogStartDistance;
            RenderSettings.fogEndDistance = _villageFogEndDistance;

            RenderSettings.ambientMode = _villageAmbientMode;
            RenderSettings.ambientLight = _villageAmbientLight;
            RenderSettings.ambientIntensity = _villageAmbientIntensity;
            RenderSettings.ambientSkyColor = _villageAmbientSkyColor;
            RenderSettings.ambientEquatorColor = _villageAmbientEquatorColor;
            RenderSettings.ambientGroundColor = _villageAmbientGroundColor;

            Camera[] allCams = Camera.allCameras;
            if (allCams != null && allCams.Length > 0)
            {
                foreach (Camera c in allCams)
                {
                    if (c == null) continue;
                    c.clearFlags = _villageClearFlags;
                    c.backgroundColor = _villageBgColor;
                }
            }
            else if (Camera.main != null)
            {
                Camera.main.clearFlags = _villageClearFlags;
                Camera.main.backgroundColor = _villageBgColor;
            }

            foreach (Light l in _villageSuns)
                if (l != null) l.enabled = true;
            _villageSuns.Clear();

            _isBackroomsEnvironmentActive = false;
            try
            {
                DynamicGI.UpdateEnvironment();
            }
            catch { }
            Debug.Log("[BackroomsProcGen] Restored Village Skybox, Fog and Lighting.");
        }

        public void CycleSkyboxMode()
        {
            if (backroomsCameraClearFlags == CameraClearFlags.Skybox)
            {
                backroomsCameraClearFlags = CameraClearFlags.SolidColor;
                Debug.Log("[BackroomsProcGen] Skybox mode switched to: Solid Color Void.");
            }
            else
            {
                backroomsCameraClearFlags = CameraClearFlags.Skybox;
                Debug.Log("[BackroomsProcGen] Skybox mode switched to: Custom Skybox Material.");
            }
            ApplyBackroomsEnvironment();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && isPlayerInsideBackrooms && _isBackroomsEnvironmentActive)
            {
                ApplyBackroomsEnvironment();
            }
        }
#endif
    }
}

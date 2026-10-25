using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NarinVillage
{
    /// <summary>
    /// Central manager tracking the 5 houses and 10 unique actions per house (50 actions total),
    /// plus God Mode / Monster Invisibility and VR Player teleportation.
    /// </summary>
    public class VillageActionManager : MonoBehaviour
    {
        public static VillageActionManager Instance { get; private set; }

        [Header("Player & Spawn References")]
        public Transform xrOriginTransform;
        public Transform villageSpawnPoint;
        public Transform backroomsSpawnPoint;

        [Header("Debug / God Mode")]
        [Tooltip("When true, the Backrooms monster cannot see or jumpscare the player.")]
        public bool isInvisibleToMonster = false;
        [Tooltip("When true, the Backrooms monster AI movement and behavior are completely paused.")]
        public bool isMonsterPaused = false;

        [Header("UI Displays")]
        public Text wristStatusText;
        public Text godModeButtonText;
        public List<Text> godModeButtonTexts = new List<Text>();
        public List<Text> monsterPauseButtonTexts = new List<Text>();
        public List<Text> houseBoardTexts = new List<Text>();

        private readonly bool[,] _completed = new bool[5, 10];
        private AudioSource _audioSource;
        private AudioClip _completeChime;

        public static readonly string[] HouseNames =
        {
            "House 1: Storage Facility",
            "House 2: Residential House",
            "House 3: Convenience Shop",
            "House 4: Mechanics Shop",
            "House 5: The Backrooms"
        };

        public static readonly string[,] ActionDescriptions =
        {
            // House 1: Storage Unit
            {
                "1. [SCRIPT] Slide roll-up Storage Door down/open",
                "2. [GRAB] Grab Box and place onto Trash Bin",
                "3. [GRAB] Grab Trash Bin and place onto Table",
                "4. [GRAB] Stack Tetris T block on Tetris blocks",
                "5. [GRAB] Stack Tetris Line block on Tetris blocks",
                "6. [GRAB] Stack Cube Puzzle on Tetris blocks",
                "7. [GRAB] Place Pyramid Puzzle into puzzle slot",
                "8. [GRAB] Place Sphere Puzzle into puzzle slot",
                "9. [UI] Press Lock Storage Unit Door button",
                "10. [UI] Press Unlock Storage Unit Door button"
            },
            // House 2: Residential House
            {
                "1. Press Front Doorbell button",
                "2. [SCRIPT] Swing House Front Door open",
                "3. [SCRIPT] Slide Front Window open",
                "4. [UI] Press Power on Boombox UI",
                "5. [UI] Press Next Track on Boombox UI",
                "6. [GRAB] Place Frying Pan onto Stove",
                "7. Turn Stove Temperature Knob",
                "8. [GRAB] Place raw Bacon into Pan (Sizzle)",
                "9. [GRAB] Place Pan with cooked Bacon onto Table",
                "10. Press Toilet Flush Handle in bathroom"
            },
            // House 3: Convenience Shop
            {
                "1. [UI] Press Start Shopping on Cash Register",
                "2. [UI] Add Bottle Price (+$2.50) on Register",
                "3. [SCRIPT] Swing Fridge Doors open",
                "4. [GRAB] Grab Bottle from Fridge into Basket",
                "5. [UI] Add Chips (+$1.99) / Leek (+$1.50) Price",
                "6. [GRAB] Grab Chips from Shelf into Basket",
                "7. [GRAB] Grab Leek from Shelf into Basket",
                "8. [GRAB] Place full Basket onto Checkout Table",
                "9. [UI] Press Pay & Confirm on Cash Register",
                "10. [SCRIPT] Automatic Sliding Door via proximity"
            },
            // House 4: Mechanics Shop
            {
                "1. [SCRIPT] Open Workshop Garage Door",
                "2. [SCRIPT] Slide Toolbox Drawer open",
                "3. [GRAB] Grab Screw Driver from Drawer",
                "4. Touch Screw Driver to Car Body (Screw sound)",
                "5. [GRAB] Grab Wrench from Drawer",
                "6. Use Wrench on Wheel Bolt",
                "7. [GRAB] Stack Tires / Mount Rim in Tire",
                "8. [GRAB] Stack Rims on top of each other",
                "9. [UI] Press Test Horn button (Honk)",
                "10. [UI] Press Test Engine button (Engine sound)"
            },
            // House 5: Backrooms
            {
                "1. Open Front Door of Backrooms House",
                "2. [SCRIPT/TELEPORT] Step through Doorway Noclip",
                "3. [GRAB] Grab Flash Light from floor",
                "4. [UI] Press Flash Light Toggle Switch",
                "5. [GRAB] Place Almond Water onto Desk",
                "6. [SCRIPT] Drink 5 Almond Waters to teleport out",
                "7. [SCRIPT] Slide Desk Drawer open",
                "8. [SCRIPT] Ceiling Light flickers and buzzes",
                "9. [SCRIPT] Monster chases in Line of Sight",
                "10. [SCRIPT] Monster touches -> Jumpscare"
            }
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            AlmondWaterBottle.TotalDrunkCount = 0;

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f;
            _completeChime = ProceduralAudioGenerator.CreateChime(660f, 990f, 0.45f);

            // 1. Ensure XRInteractionManager exists in the scene so all XR Ray/Direct interactables work
            if (FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>() == null)
            {
                GameObject imGO = new GameObject("XR Interaction Manager");
                imGO.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
                Debug.Log("[VRVillage] Auto-created XR Interaction Manager.");
            }

            // 2. Ensure EventSystem has XRUIInputModule with mouse enabled for VR Rays and mouse clicks
            EventSystem es = FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                es = esGO.AddComponent<EventSystem>();
            }
            var xrui = es.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
            if (xrui == null)
            {
                xrui = es.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
            }

            // 3. Ensure exactly one AudioListener is active in the scene
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            if (listeners.Length > 1)
            {
                bool keptOne = false;
                for (int i = 0; i < listeners.Length; i++)
                {
                    Camera cam = listeners[i].GetComponent<Camera>();
                    if (!keptOne && cam != null && cam == Camera.main)
                    {
                        keptOne = true;
                    }
                    else if (keptOne)
                    {
                        listeners[i].enabled = false;
                    }
                }
            }

            // 4. Resolve XR Origin and ensure collision enforcer on active rig
            if (xrOriginTransform == null)
            {
                XROrigin origin = FindFirstObjectByType<XROrigin>();
                if (origin != null) xrOriginTransform = origin.transform;
            }

            // Shared village: nothing is added to the XR rig (no VRPlayerCollisionEnforcer).
        }

        private void Start()
        {
            if (villageSpawnPoint == null)
            {
                GameObject vSpawn = NarinScope.Find("Village_SpawnPoint");
                if (vSpawn != null) villageSpawnPoint = vSpawn.transform;
            }
            // Shared village: the player starts at the village's own spawn, not Narin's.

            EnsureWorldCameras();
            EnsureDebugButtonBindings();
            EnsureHouse2DoorwayOpen();
            EnsureAllAlmondBottlesConfigured();
            EnsureTrashContainersConfigured();
            EnsureShopCashRegisterConfigured();
            RefreshAllUI();
        }

        private void Update()
        {
            // The shared rig has no height clamp (Narin's VRPlayerCollisionEnforcer is not used here):
            // if the player ever drops through the Backrooms floor, put them back at its spawn.
            BackroomsProceduralGenerator gen = BackroomsProceduralGenerator.Instance;
            if (gen != null && gen.isPlayerInsideBackrooms && xrOriginTransform != null &&
                xrOriginTransform.position.y < -3f && backroomsSpawnPoint != null)
            {
                TeleportPlayerTo(backroomsSpawnPoint.position, backroomsSpawnPoint.rotation);
            }

#if ENABLE_INPUT_SYSTEM
            // 1. Desktop Mouse Left Click (Universal click for World UI Canvases and 3D Interactables)
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                HandleScreenClick(mousePos);
            }

            // 2. Keyboard 'E' or 'F' key for immediate proximity interaction / drinking
            if (Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame))
            {
                HandleKeyboardInteract();
            }
#else
            if (Input.GetMouseButtonDown(0))
            {
                HandleScreenClick(Input.mousePosition);
            }
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
            {
                HandleKeyboardInteract();
            }
#endif
        }

        public void HandleScreenClick(Vector2 screenPos)
        {
            Camera cam = Camera.main;
            if (cam == null && xrOriginTransform != null) cam = xrOriginTransform.GetComponentInChildren<Camera>();
            if (cam == null) return;

            // 1. First Raycast UI Canvases via EventSystem
            if (EventSystem.current != null)
            {
                PointerEventData ped = new PointerEventData(EventSystem.current) { position = screenPos };
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(ped, results);

                for (int i = 0; i < results.Count; i++)
                {
                    GameObject hitGO = results[i].gameObject;
                    if (hitGO == null) continue;
                    if (!NarinScope.IsMine(hitGO)) return;

                    var binding = hitGO.GetComponentInParent<VillageUIButtonBinding>();
                    if (binding != null)
                    {
                        binding.ExecuteCommandDebounced();
                        return;
                    }

                    var btn = hitGO.GetComponentInParent<Button>();
                    if (btn != null && btn.interactable)
                    {
                        btn.onClick.Invoke();
                        return;
                    }

                    var trig = hitGO.GetComponentInParent<InteractiveTriggerButton>();
                    if (trig != null)
                    {
                        trig.TriggerInteraction();
                        return;
                    }
                }
            }

            // 2. Physics Raycast into 3D world (Bottles, Doors, 3D buttons, Canvas colliders)
            Ray ray = cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 35f, ~0, QueryTriggerInteraction.Collide))
            {
                ProcessHitInteractable(hit.collider.gameObject);
            }
        }

        public void ProcessHitInteractable(GameObject go)
        {
            if (go == null || !NarinScope.IsMine(go)) return;

            // 1. Almond Water Bottle (Action 6 drinking)
            var bottle = go.GetComponentInParent<AlmondWaterBottle>();
            if (bottle != null)
            {
                bottle.DrinkBottle();
                return;
            }

            // 2. Village UI Button Binding
            var binding = go.GetComponentInParent<VillageUIButtonBinding>();
            if (binding != null)
            {
                binding.ExecuteCommandDebounced();
                return;
            }

            // 3. Interactive 3D Trigger Button (Doorbell, Stove knob, Flush handle, etc.)
            var trig = go.GetComponentInParent<InteractiveTriggerButton>();
            if (trig != null)
            {
                trig.TriggerInteraction();
                return;
            }

            // 4. UI Button component on world canvas
            var btn = go.GetComponentInParent<Button>();
            if (btn != null && btn.interactable)
            {
                btn.onClick.Invoke();
                return;
            }

            // 5. Scripted Mover (Doors, Drawers, Garage Door)
            var mover = go.GetComponentInParent<ScriptedMover>();
            if (mover != null)
            {
                mover.ToggleOpen();
                return;
            }
        }

        public void HandleKeyboardInteract()
        {
            Camera cam = Camera.main;
            if (cam == null && xrOriginTransform != null) cam = xrOriginTransform.GetComponentInChildren<Camera>();
            if (cam == null) return;

            // 1. Center of screen raycast (5.0m)
            Ray centerRay = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(centerRay, out RaycastHit hit, 5.0f, ~0, QueryTriggerInteraction.Collide))
            {
                ProcessHitInteractable(hit.collider.gameObject);
                return;
            }

            // 2. Proximity to Almond Water bottle (< 4m)
            AlmondWaterBottle nearestBottle = null;
            float minBottleDist = 4.0f;
            foreach (var b in FindObjectsByType<AlmondWaterBottle>(FindObjectsSortMode.None))
            {
                if (b != null && b.gameObject.activeInHierarchy)
                {
                    float d = Vector3.Distance(cam.transform.position, b.transform.position);
                    if (d < minBottleDist)
                    {
                        minBottleDist = d;
                        nearestBottle = b;
                    }
                }
            }
            if (nearestBottle != null)
            {
                nearestBottle.DrinkBottle();
                return;
            }

            // 3. Proximity to 3D Trigger Button (< 3m)
            InteractiveTriggerButton nearestTrig = null;
            float minTrigDist = 3.0f;
            foreach (var tb in FindObjectsByType<InteractiveTriggerButton>(FindObjectsSortMode.None))
            {
                if (tb != null && tb.gameObject.activeInHierarchy)
                {
                    float d = Vector3.Distance(cam.transform.position, tb.transform.position);
                    if (d < minTrigDist)
                    {
                        minTrigDist = d;
                        nearestTrig = tb;
                    }
                }
            }
            if (nearestTrig != null)
            {
                nearestTrig.TriggerInteraction();
                return;
            }

            // 4. Proximity to ScriptedMover (Doors, Drawers < 3.5m)
            ScriptedMover nearestMover = null;
            float minMoverDist = 3.5f;
            foreach (var sm in FindObjectsByType<ScriptedMover>(FindObjectsSortMode.None))
            {
                if (sm != null && sm.gameObject.activeInHierarchy)
                {
                    float d = Vector3.Distance(cam.transform.position, sm.transform.position);
                    if (d < minMoverDist)
                    {
                        minMoverDist = d;
                        nearestMover = sm;
                    }
                }
            }
            if (nearestMover != null)
            {
                nearestMover.ToggleOpen();
                return;
            }
        }

        public void EnsureHouse2DoorwayOpen()
        {
            GameObject h2Root = NarinScope.Find("House2_ResidentialHouse");
            Transform modelT = null;
            if (h2Root != null)
            {
                modelT = h2Root.transform.Find("Residential_House_Model");
            }
            if (modelT == null)
            {
                GameObject mGO = NarinScope.Find("Residential_House_Model");
                if (mGO != null) modelT = mGO.transform;
            }

            if (modelT != null)
            {
                Transform houseChild = modelT.Find("House");
                if (houseChild != null)
                {
                    MeshFilter mf = houseChild.GetComponent<MeshFilter>();
                    MeshCollider mc = houseChild.GetComponent<MeshCollider>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        Mesh srcMesh = mf.sharedMesh;
                        if (srcMesh.subMeshCount >= 4)
                        {
                            Mesh bodyMesh = new Mesh();
                            bodyMesh.name = "House_Body_NoDoor_Runtime";
                            bodyMesh.vertices = srcMesh.vertices;
                            bodyMesh.normals = srcMesh.normals;
                            bodyMesh.uv = srcMesh.uv;
                            bodyMesh.subMeshCount = 3;
                            for (int s = 0; s < 3; s++)
                            {
                                bodyMesh.SetTriangles(srcMesh.GetTriangles(s), s);
                            }
                            bodyMesh.RecalculateBounds();
                            mf.sharedMesh = bodyMesh;
                            if (mc != null)
                            {
                                mc.sharedMesh = bodyMesh;
                            }
                        }
                        else if (mc != null && mc.sharedMesh != mf.sharedMesh)
                        {
                            mc.sharedMesh = mf.sharedMesh;
                        }
                    }
                }
            }
        }

        public void EnsureAllAlmondBottlesConfigured()
        {
            List<AlmondWaterBottle> bottlesList = new List<AlmondWaterBottle>();

            foreach (var go in NarinScope.AllObjects())
            {
                if (go == null || go.scene.name == null) continue;
                if (go.name.IndexOf("Almond", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (go.name.IndexOf("Canvas", StringComparison.OrdinalIgnoreCase) >= 0 || go.name.IndexOf("Btn", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                // 0. Remove conflicting XRSimpleInteractable from bottle root
                XRSimpleInteractable simple = go.GetComponent<XRSimpleInteractable>();
                if (simple != null)
                {
                    if (Application.isPlaying) Destroy(simple);
                    else DestroyImmediate(simple);
                }

                // 1. Ensure Collider
                BoxCollider bc = go.GetComponent<BoxCollider>();
                if (bc == null)
                {
                    bc = go.AddComponent<BoxCollider>();
                }
                MeshFilter mf = go.GetComponent<MeshFilter>();
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

                // 2. Ensure Rigidbody
                Rigidbody rb = go.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = go.AddComponent<Rigidbody>();
                }
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.mass = 0.4f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                Vector3 localUp = go.transform.InverseTransformDirection(Vector3.up);
                rb.centerOfMass = -localUp * 0.08f;

                // 3. Ensure GrabAndPlaceItem
                GrabAndPlaceItem grab = go.GetComponent<GrabAndPlaceItem>();
                if (grab == null)
                {
                    grab = go.AddComponent<GrabAndPlaceItem>();
                }
                if (grab.placementRules == null || grab.placementRules.Count == 0)
                {
                    grab.placementRules = new List<PlacementTargetRule>
                    {
                        new PlacementTargetRule { targetNameContains = "Desk", houseNumber = 5, actionNumber = 5 },
                        new PlacementTargetRule { targetNameContains = "Drawer", houseNumber = 5, actionNumber = 5 }
                    };
                }

                // 4. Ensure AlmondWaterBottle
                AlmondWaterBottle drink = go.GetComponent<AlmondWaterBottle>();
                if (drink == null)
                {
                    drink = go.AddComponent<AlmondWaterBottle>();
                }
                bottlesList.Add(drink);

                // 5. Ensure XRGrabInteractable on the bottle
                var xri = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (xri == null)
                {
                    xri = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                }
                xri.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                xri.throwOnDetach = true;
                xri.interactionLayers = 1;
                xri.retainTransformParent = false;
                xri.colliders.Clear();
                if (bc != null) xri.colliders.Add(bc);

                // 6. Retarget any child button canvas to THIS bottle
                foreach (var b in go.GetComponentsInChildren<VillageUIButtonBinding>(true))
                {
                    if (b != null && b.commandType == "DrinkAlmond")
                    {
                        b.targetAlmondBottle = drink;
                    }
                }
            }

            // Narin's "gather 5 bottles at the spawn desk" pass is left out: its positions were stale (x/z 20x)
            // so it never worked in the original either, and it also woke up the hidden bottle templates.
            // Bottles come from the maze instead (starter bottle at the desk + almondWaterSpawnChance per room).
        }

        public void EnsureTrashContainersConfigured()
        {
            foreach (var go in NarinScope.AllObjects())
            {
                if (go == null || go.scene.name == null) continue;
                if (go.name.IndexOf("Trash", StringComparison.OrdinalIgnoreCase) >= 0 || go.name.IndexOf("Bin", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Rigidbody rb = go.GetComponent<Rigidbody>();
                    if (rb == null) rb = go.AddComponent<Rigidbody>();
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.mass = 2.0f;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;

                    Vector3 localUp = go.transform.InverseTransformDirection(Vector3.up);
                    rb.centerOfMass = -localUp * 0.15f;

                    GrabAndPlaceItem grab = go.GetComponent<GrabAndPlaceItem>();
                    if (grab == null)
                    {
                        grab = go.AddComponent<GrabAndPlaceItem>();
                        grab.isHollowContainer = true;
                    }
                    grab.isHollowContainer = true;
                    bool hasRule = false;
                    foreach (var r in grab.placementRules)
                    {
                        if (r != null && r.targetNameContains == "Table") { hasRule = true; break; }
                    }
                    if (!hasRule)
                    {
                        grab.placementRules.Add(new PlacementTargetRule
                        {
                            targetNameContains = "Table",
                            houseNumber = 1,
                            actionNumber = 3
                        });
                    }

                    GrabAndPlaceItem.BuildHollowOpenTopColliders(go);

                    var xri = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                    if (xri == null) xri = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                    xri.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                    xri.throwOnDetach = true;
                    xri.interactionLayers = 1;
                    xri.retainTransformParent = false;
                    GrabAndPlaceItem.SyncGrabColliders(go);
                }
            }
        }

        public void EnsureShopCashRegisterConfigured()
        {
            HouseUIController shopUI = null;
            foreach (var ui in Resources.FindObjectsOfTypeAll<HouseUIController>())
            {
                if (ui != null && ui.gameObject.scene.name != null && ui.name.IndexOf("House3", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    shopUI = ui;
                    break;
                }
            }

            if (shopUI != null)
            {
                // 1. Position the UI right on the checkout counter at eye level (Y = 1.35)
                Transform shopUIT = shopUI.transform;
                if (shopUIT.parent != null && shopUIT.parent.name.Contains("House3"))
                {
                    shopUIT.localPosition = new Vector3(-2.15f, 1.35f, -7.05f);
                    shopUIT.localRotation = Quaternion.Euler(15f, 0f, 0f);
                }
                else
                {
                    shopUIT.position = new Vector3(3.85f, 1.35f, 11.95f);
                    shopUIT.rotation = Quaternion.Euler(15f, 0f, 0f);
                }

                // 2. Ensure Canvas raycaster doesn't block rays
                Canvas c = shopUI.GetComponent<Canvas>();
                if (c != null)
                {
                    if (Camera.main != null && c.worldCamera == null)
                        c.worldCamera = Camera.main;
                }
                GraphicRaycaster gr = shopUI.GetComponent<GraphicRaycaster>();
                if (gr != null)
                {
                    gr.blockingObjects = GraphicRaycaster.BlockingObjects.None;
                }
                var tdgr = shopUI.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
                if (tdgr != null)
                {
                    tdgr.ignoreReversedGraphics = false;
                }

                // 3. Ensure all buttons on shopUI have BoxCollider, Button, XRSimpleInteractable, VillageUIButtonBinding
                foreach (Button btn in shopUI.GetComponentsInChildren<Button>(true))
                {
                    RectTransform rt = btn.GetComponent<RectTransform>();
                    BoxCollider bc = btn.GetComponent<BoxCollider>();
                    if (bc == null) bc = btn.gameObject.AddComponent<BoxCollider>();
                    bc.isTrigger = true;
                    if (rt != null)
                    {
                        bc.size = new Vector3(Mathf.Max(rt.rect.width, 100f), Mathf.Max(rt.rect.height, 40f), 40f);
                    }

                    var xrs = btn.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
                    if (xrs == null) xrs = btn.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

                    VillageUIButtonBinding binding = btn.GetComponent<VillageUIButtonBinding>();
                    if (binding != null)
                    {
                        binding.uiController = shopUI;
                    }
                }

                shopUI.EnsureCashRegisterDrawerCreated();
            }
        }

        public void EnsureWorldCameras()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null && xrOriginTransform != null)
            {
                mainCam = xrOriginTransform.GetComponentInChildren<Camera>();
            }
            if (mainCam == null)
            {
                XROrigin origin = FindFirstObjectByType<XROrigin>();
                if (origin != null && origin.Camera != null)
                {
                    mainCam = origin.Camera;
                }
            }
            if (mainCam != null)
            {
                if (string.IsNullOrEmpty(mainCam.tag) || mainCam.tag == "Untagged")
                {
                    try { mainCam.tag = "MainCamera"; } catch {}
                }

                foreach (Canvas c in NarinScope.Components<Canvas>())
                {
                    if (c == null) continue;
                    if (c.renderMode == RenderMode.WorldSpace)
                    {
                        if (c.worldCamera == null)
                            c.worldCamera = mainCam;

                        if (c.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() == null)
                        {
                            c.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
                        }

                        if (c.GetComponent<GraphicRaycaster>() == null)
                        {
                            c.gameObject.AddComponent<GraphicRaycaster>();
                        }
                    }
                }
            }
        }

        public void EnsureDebugButtonBindings()
        {
            foreach (var binding in Resources.FindObjectsOfTypeAll<VillageUIButtonBinding>())
            {
                if (binding == null || binding.gameObject.scene.name == null) continue;
                if (binding.commandType == "GodMode")
                {
                    Text t = binding.GetComponentInChildren<Text>(true);
                    if (t != null && !godModeButtonTexts.Contains(t))
                        godModeButtonTexts.Add(t);
                }
                else if (binding.commandType == "PauseMonster")
                {
                    Text t = binding.GetComponentInChildren<Text>(true);
                    if (t != null && !monsterPauseButtonTexts.Contains(t))
                        monsterPauseButtonTexts.Add(t);
                }
            }
        }

        public void CompleteAction(int houseNumber, int actionNumber)
        {
            int h = Mathf.Clamp(houseNumber - 1, 0, 4);
            int a = Mathf.Clamp(actionNumber - 1, 0, 9);

            if (_completed[h, a]) return;

            _completed[h, a] = true;
            Debug.Log($"[VRVillage] Completed {HouseNames[h]} -> {ActionDescriptions[h, a]}");

            if (_audioSource != null && _completeChime != null)
            {
                _audioSource.PlayOneShot(_completeChime, 0.6f);
            }

            RefreshAllUI();
        }

        public bool IsActionCompleted(int houseNumber, int actionNumber)
        {
            int h = Mathf.Clamp(houseNumber - 1, 0, 4);
            int a = Mathf.Clamp(actionNumber - 1, 0, 9);
            return _completed[h, a];
        }

        public int GetHouseCompletedCount(int houseIndex0)
        {
            int count = 0;
            for (int a = 0; a < 10; a++)
            {
                if (_completed[houseIndex0, a]) count++;
            }
            return count;
        }

        public int GetTotalCompletedCount()
        {
            int total = 0;
            for (int h = 0; h < 5; h++)
                total += GetHouseCompletedCount(h);
            return total;
        }

        public void ToggleGodMode()
        {
            isInvisibleToMonster = !isInvisibleToMonster;
            Debug.Log($"[VRVillage] GOD MODE (Monster Invisibility) is now: {(isInvisibleToMonster ? "ON" : "OFF")}");
            if (_audioSource != null && _completeChime != null)
                _audioSource.PlayOneShot(_completeChime, 0.65f);
            RefreshAllUI();
        }

        public void ToggleMonsterPause()
        {
            isMonsterPaused = !isMonsterPaused;
            Debug.Log($"[VRVillage] PAUSE ENTITY AI is now: {(isMonsterPaused ? "PAUSED" : "ACTIVE")}");
            if (_audioSource != null && _completeChime != null)
                _audioSource.PlayOneShot(_completeChime, 0.65f);
            RefreshAllUI();
        }

        public void TeleportToVillageSpawn()
        {
            // Unload / deactive Backrooms dimension when returning to village to save resources
            BackroomsProceduralGenerator procGen = BackroomsProceduralGenerator.Instance;
            if (procGen == null)
            {
                procGen = FindFirstObjectByType<BackroomsProceduralGenerator>(FindObjectsInactive.Include);
            }
            if (procGen != null)
            {
                procGen.OnPlayerExitBackrooms();
            }

            if (villageSpawnPoint != null)
                TeleportPlayerTo(villageSpawnPoint.position, villageSpawnPoint.rotation);
            else
                TeleportPlayerTo(new Vector3(0f, 0.65f, -12f), Quaternion.identity);
        }

        public void TeleportToBackrooms()
        {
            CompleteAction(5, 2);

            // Move the player first: the generator only builds rooms (and their floors) around a player
            // who is already inside the Backrooms, so entering before the teleport left no floor to land on.
            if (backroomsSpawnPoint != null)
                TeleportPlayerTo(backroomsSpawnPoint.position, backroomsSpawnPoint.rotation);
            else
                TeleportPlayerTo(new Vector3(2003.3f, 0.65f, 2002.7f), Quaternion.identity);

            // Dynamically activate and load the Backrooms dimension when nocliped
            BackroomsProceduralGenerator procGen = BackroomsProceduralGenerator.Instance;
            if (procGen == null)
            {
                procGen = FindFirstObjectByType<BackroomsProceduralGenerator>(FindObjectsInactive.Include);
            }
            if (procGen != null)
            {
                procGen.OnPlayerEnterBackrooms();
            }
        }

        public void TeleportPlayerTo(Vector3 targetPos, Quaternion targetRot)
        {
            Vector3 backroomsCenter = backroomsSpawnPoint != null ? backroomsSpawnPoint.position : new Vector3(2003.3f, 0.65f, 2002.7f);
            if (Vector3.Distance(targetPos, backroomsCenter) > 300f)
            {
                BackroomsProceduralGenerator procGen = BackroomsProceduralGenerator.Instance;
                if (procGen == null) procGen = FindFirstObjectByType<BackroomsProceduralGenerator>(FindObjectsInactive.Include);
                if (procGen != null && procGen.isPlayerInsideBackrooms)
                {
                    procGen.OnPlayerExitBackrooms();
                }
            }

            XROrigin origin = xrOriginTransform != null ? xrOriginTransform.GetComponentInChildren<XROrigin>() : null;
            if (origin == null)
            {
                origin = FindFirstObjectByType<XROrigin>();
                if (origin != null) xrOriginTransform = origin.transform;
            }

            // The XR Origin itself, not transform.root: the rig may sit under a group object in the village.
            Transform rootToMove = origin != null ? origin.transform : xrOriginTransform;

            if (rootToMove != null)
            {
                // Disable ALL CharacterControllers in the player rig so PhysX cannot lock position
                CharacterController[] allCCs = rootToMove.GetComponentsInChildren<CharacterController>();
                foreach (var cc in allCCs) cc.enabled = false;

                VRPlayerCollisionEnforcer[] enforcers = rootToMove.GetComponentsInChildren<VRPlayerCollisionEnforcer>();
                foreach (var enf in enforcers) enf.ResetVerticalVelocity();

                // Counter-offset Camera Offset so Main Camera sits horizontally centered over rig
                if (origin != null && origin.Camera != null)
                {
                    Transform camT = origin.Camera.transform;
                    Vector3 camLocal = camT.localPosition;
                    if (camT.parent != null && camT.parent != rootToMove)
                    {
                        camT.parent.localPosition = new Vector3(-camLocal.x, camT.parent.localPosition.y, -camLocal.z);
                    }
                }

                Vector3 safeTargetPos = new Vector3(targetPos.x, Mathf.Max(targetPos.y, 0.05f), targetPos.z);
                rootToMove.SetPositionAndRotation(safeTargetPos, targetRot);
                Physics.SyncTransforms();

                foreach (var cc in allCCs) cc.enabled = true;
                Debug.Log($"[VRVillage] Teleported player safely to {safeTargetPos}");
            }
        }

        public void RefreshAllUI()
        {
            EnsureDebugButtonBindings();

            string godLabel = isInvisibleToMonster
                ? "<b>GOD MODE (INVISIBLE): <color=#44FF44>ON</color></b>"
                : "<b>GOD MODE (INVISIBLE): <color=#FFCC44>OFF</color></b>";

            if (godModeButtonText != null)
                godModeButtonText.text = godLabel;

            foreach (Text t in godModeButtonTexts)
            {
                if (t != null) t.text = godLabel;
            }

            string pauseLabel = isMonsterPaused
                ? "<b>PAUSE ENTITY AI: <color=#44FF44>PAUSED</color></b>"
                : "<b>PAUSE ENTITY AI: <color=#FFCC44>ACTIVE</color></b>";

            foreach (Text t in monsterPauseButtonTexts)
            {
                if (t != null) t.text = pauseLabel;
            }

            for (int h = 0; h < houseBoardTexts.Count && h < 5; h++)
            {
                if (houseBoardTexts[h] == null) continue;
                StringBuilder sb = new StringBuilder();
                int done = GetHouseCompletedCount(h);
                sb.AppendLine($"<b>{HouseNames[h]} ({done}/10)</b>");
                for (int a = 0; a < 10; a++)
                {
                    string mark = _completed[h, a] ? "<color=#44FF44>[DONE]</color>" : "<color=#FFCC44>[TODO]</color>";
                    sb.AppendLine($"{mark} {ActionDescriptions[h, a]}");
                }
                houseBoardTexts[h].text = sb.ToString();
            }

            if (wristStatusText != null)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"<b>VR VILLAGE PROGRESS: {GetTotalCompletedCount()}/50</b>");
                sb.AppendLine($"GOD MODE: {(isInvisibleToMonster ? "<color=#44FF44>ON (INVISIBLE)</color>" : "<color=#FFCC44>OFF</color>")} | ENTITY AI: {(isMonsterPaused ? "<color=#44FF44>PAUSED</color>" : "<color=#FFCC44>ACTIVE</color>")}");
                for (int h = 0; h < 5; h++)
                {
                    sb.AppendLine($"{HouseNames[h]}: {GetHouseCompletedCount(h)}/10");
                }
                wristStatusText.text = sb.ToString();
            }
        }
    }
}

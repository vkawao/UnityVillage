using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace NarinVillage
{
    /// <summary>
    /// Almond Water bottle: can be placed on Desk/Drawer (Action 5) and drunk (5 bottles drunk -> Action 6 & teleport out).
    /// Can be drunk by:
    /// 1) Clicking the "6. DRINK ALMOND WATER (5x TO ESCAPE)" button on the Backrooms Terminal UI,
    /// 2) Clicking the floating "[DRINK WATER]" button above any bottle,
    /// 3) Pressing 'E' or 'F' on the keyboard while holding a bottle or standing near one,
    /// 4) Bringing a held bottle near the camera / mouth,
    /// 5) Grabbing and pressing the trigger (activate)!
    /// </summary>
    public class AlmondWaterBottle : MonoBehaviour, IPointerClickHandler
    {
        public static int TotalDrunkCount = 0;
        public const int RequiredDrinksToEscape = 5;

        private bool _isDrunk = false;
        private XRGrabInteractable _grab;
        private bool _isHeld = false;

        private void Awake()
        {
            // 1. Remove conflicting XRSimpleInteractable if present on the bottle root
            XRSimpleInteractable simple = GetComponent<XRSimpleInteractable>();
            if (simple != null)
            {
                if (Application.isPlaying) Destroy(simple);
                else DestroyImmediate(simple);
            }

            // 2. Ensure physical BoxCollider
            BoxCollider bc = GetComponent<BoxCollider>();
            if (bc == null)
            {
                bc = gameObject.AddComponent<BoxCollider>();
            }
            MeshFilter mf = GetComponent<MeshFilter>();
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

            // 3. Ensure Rigidbody
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            Vector3 localUp = transform.InverseTransformDirection(Vector3.up);
            rb.centerOfMass = -localUp * 0.08f;

            // 4. Ensure GrabAndPlaceItem rules
            GrabAndPlaceItem gpi = GetComponent<GrabAndPlaceItem>();
            if (gpi == null)
            {
                gpi = gameObject.AddComponent<GrabAndPlaceItem>();
            }
            if (gpi.placementRules == null || gpi.placementRules.Count == 0)
            {
                gpi.placementRules = new System.Collections.Generic.List<PlacementTargetRule>
                {
                    new PlacementTargetRule { targetNameContains = "Desk", houseNumber = 5, actionNumber = 5 },
                    new PlacementTargetRule { targetNameContains = "Drawer", houseNumber = 5, actionNumber = 5 }
                };
            }

            // 5. Ensure XRGrabInteractable
            _grab = GetComponent<XRGrabInteractable>();
            if (_grab == null)
            {
                _grab = gameObject.AddComponent<XRGrabInteractable>();
            }
            _grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            _grab.throwOnDetach = true;
            _grab.interactionLayers = 1;
            _grab.retainTransformParent = false;
            _grab.colliders.Clear();
            if (bc != null) _grab.colliders.Add(bc);

            _grab.selectEntered.RemoveListener(OnGrabEntered);
            _grab.selectEntered.AddListener(OnGrabEntered);
            _grab.selectExited.RemoveListener(OnGrabExited);
            _grab.selectExited.AddListener(OnGrabExited);
            _grab.activated.RemoveListener(OnActivated);
            _grab.activated.AddListener(OnActivated);
        }

        private void OnGrabEntered(SelectEnterEventArgs args) => _isHeld = true;
        private void OnGrabExited(SelectExitEventArgs args) => _isHeld = false;
        private void OnActivated(ActivateEventArgs args) => DrinkBottle();

        private void Update()
        {
            if (_isDrunk) return;

            // 1. Billboard child button canvas so it ALWAYS faces the player camera
            Transform canvasT = transform.Find("Bottle_Drink_Button_Canvas");
            if (canvasT != null && Camera.main != null)
            {
                Vector3 camPos = Camera.main.transform.position;
                Vector3 toCam = camPos - canvasT.position;
                if (toCam.sqrMagnitude > 0.001f)
                {
                    canvasT.rotation = Quaternion.LookRotation(toCam);
                }
            }

            // 2. Allow drinking by bringing the held bottle near the VR headset (mouth level)
            if (_isHeld && Camera.main != null)
            {
                float distToHead = Vector3.Distance(transform.position, Camera.main.transform.position);
                if (distToHead < 0.85f)
                {
                    DrinkBottle();
                    return;
                }
            }

            // 3. Allow pressing 'E' or 'F' on keyboard anytime in simulator when holding or looking close to bottle
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame ||
                    UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame)
                {
                    if (_isHeld || (Camera.main != null && Vector3.Distance(transform.position, Camera.main.transform.position) < 3.5f))
                    {
                        DrinkBottle();
                        return;
                    }
                }
            }
#endif
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            DrinkBottle();
        }

        private void OnMouseDown()
        {
            DrinkBottle();
        }

        public void DrinkBottle()
        {
            if (_isDrunk) return;
            _isDrunk = true;

            DrinkStaticFallback(transform.position);
            gameObject.SetActive(false);
        }

        public static void DrinkStaticFallback(Vector3 soundPos)
        {
            TotalDrunkCount++;
            AudioSource.PlayClipAtPoint(ProceduralAudioGenerator.CreateChime(523.25f, 1046.5f, 0.5f), soundPos, 0.9f);
            Debug.Log($"[VRVillage] Drank Almond Water ({TotalDrunkCount}/{RequiredDrinksToEscape})");

            if (BackroomsProceduralGenerator.Instance != null)
            {
                BackroomsProceduralGenerator.Instance.OnAlmondWaterDrunk(TotalDrunkCount);
            }

            foreach (var ui in Object.FindObjectsByType<HouseUIController>(FindObjectsSortMode.None))
            {
                if (ui != null && ui.flashlightStatusLabel != null)
                {
                    ui.flashlightStatusLabel.text = $"DRANK ALMOND WATER ({TotalDrunkCount}/{RequiredDrinksToEscape})";
                }
            }

            // Complete Action 6 on the very first drink!
            if (VillageActionManager.Instance != null)
            {
                VillageActionManager.Instance.CompleteAction(5, 6);
            }

            if (TotalDrunkCount >= RequiredDrinksToEscape && VillageActionManager.Instance != null)
            {
                VillageActionManager.Instance.TeleportToVillageSpawn();
                TotalDrunkCount = 0;
            }
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace NarinVillage
{
    /// <summary>
    /// Serialized runtime button binder ensuring all World-Space Canvas UI buttons
    /// work natively with TrackedDeviceGraphicRaycaster, GraphicRaycaster, XR Ray Interactors, and direct poke.
    /// Preserves trigger colliders + XRSimpleInteractables for universal VR and mouse compatibility!
    /// </summary>
    public class VillageUIButtonBinding : MonoBehaviour, IPointerClickHandler
    {
        public string commandType;
        public HouseUIController uiController;
        public AlmondWaterBottle targetAlmondBottle;
        public float floatParameter;

        private float _lastExecuteTime = -10f;

        private void Awake()
        {
            // Ensure collider exists with isTrigger = true for XR Ray and mouse raycasts
            BoxCollider col = GetComponent<BoxCollider>();
            if (col == null)
            {
                RectTransform rt = GetComponent<RectTransform>();
                if (rt != null)
                {
                    col = gameObject.AddComponent<BoxCollider>();
                    col.isTrigger = true;
                    col.size = new Vector3(Mathf.Max(rt.rect.width, 40f), Mathf.Max(rt.rect.height, 40f), 30f);
                }
                else
                {
                    MeshFilter mf = GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        col = gameObject.AddComponent<BoxCollider>();
                        col.center = mf.sharedMesh.bounds.center;
                        col.size = mf.sharedMesh.bounds.size;
                    }
                    else
                    {
                        col = gameObject.AddComponent<BoxCollider>();
                        col.isTrigger = true;
                        col.size = new Vector3(0.2f, 0.2f, 0.2f);
                    }
                }
            }

            // Ensure XR Interactable exists so XR Ray Interactor & Direct Interactor can select it
            XRBaseInteractable interactable = GetComponent<XRBaseInteractable>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<XRSimpleInteractable>();
            }
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.activated.RemoveListener(OnActivated);
            interactable.activated.AddListener(OnActivated);

            Button btn = GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(ExecuteCommandDebounced);
                btn.onClick.AddListener(ExecuteCommandDebounced);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args) => ExecuteCommandDebounced();
        private void OnActivated(ActivateEventArgs args) => ExecuteCommandDebounced();

        public void OnPointerClick(PointerEventData eventData)
        {
            ExecuteCommandDebounced();
        }

        private void OnMouseDown()
        {
            ExecuteCommandDebounced();
        }

        public void ExecuteCommandDebounced()
        {
            if (Time.unscaledTime - _lastExecuteTime < 0.20f) return;
            _lastExecuteTime = Time.unscaledTime;
            ExecuteCommand();
        }

        public void ExecuteCommand()
        {
            if (uiController == null)
            {
                uiController = GetComponentInParent<HouseUIController>();
                if (uiController == null)
                {
                    var allControllers = FindObjectsByType<HouseUIController>(FindObjectsSortMode.None);
                    foreach (var ctrl in allControllers)
                    {
                        if (commandType.StartsWith("Shop") && ctrl.name.Contains("House3")) { uiController = ctrl; break; }
                        if (commandType.StartsWith("Storage") && ctrl.name.Contains("House1")) { uiController = ctrl; break; }
                        if (commandType.StartsWith("Boombox") && ctrl.name.Contains("House2")) { uiController = ctrl; break; }
                        if (commandType.StartsWith("Mechanic") && ctrl.name.Contains("House4")) { uiController = ctrl; break; }
                        if ((commandType.StartsWith("Flashlight") || commandType.StartsWith("Drink")) && ctrl.name.Contains("House5")) { uiController = ctrl; break; }
                    }
                    if (uiController == null && allControllers.Length > 0)
                        uiController = allControllers[0];
                }
            }

            switch (commandType)
            {
                case "GodMode":
                    if (VillageActionManager.Instance != null)
                        VillageActionManager.Instance.ToggleGodMode();
                    break;
                case "PauseMonster":
                    if (VillageActionManager.Instance != null)
                        VillageActionManager.Instance.ToggleMonsterPause();
                    break;
                case "TeleportSpawn":
                    if (VillageActionManager.Instance != null)
                        VillageActionManager.Instance.TeleportToVillageSpawn();
                    break;
                case "TeleportBackrooms":
                    if (VillageActionManager.Instance != null)
                    {
                        VillageActionManager.Instance.CompleteAction(5, 2);
                        VillageActionManager.Instance.TeleportToBackrooms();
                    }
                    break;
                case "ToggleBackroomsMode":
                    if (BackroomsProceduralGenerator.Instance != null)
                        BackroomsProceduralGenerator.Instance.ToggleMode();
                    break;
                case "RegenerateBackrooms":
                    if (BackroomsProceduralGenerator.Instance != null)
                        BackroomsProceduralGenerator.Instance.RegenerateMaze();
                    break;
                case "CycleBackroomsSkybox":
                    if (BackroomsProceduralGenerator.Instance != null)
                        BackroomsProceduralGenerator.Instance.CycleSkyboxMode();
                    break;
                case "StorageLock":
                    if (uiController != null) uiController.OnStorageLockDoor();
                    else
                    {
                        var ctrl = FindFirstObjectByType<HouseUIController>();
                        if (ctrl != null) ctrl.OnStorageLockDoor();
                        else if (VillageActionManager.Instance != null) VillageActionManager.Instance.CompleteAction(1, 9);
                    }
                    break;
                case "StorageUnlock":
                    if (uiController != null) uiController.OnStorageUnlockDoor();
                    else
                    {
                        var ctrl = FindFirstObjectByType<HouseUIController>();
                        if (ctrl != null) ctrl.OnStorageUnlockDoor();
                        else if (VillageActionManager.Instance != null) VillageActionManager.Instance.CompleteAction(1, 10);
                    }
                    break;
                case "BoomboxPower":
                    if (uiController != null) uiController.OnBoomboxPowerToggle();
                    else if (VillageActionManager.Instance != null) VillageActionManager.Instance.CompleteAction(2, 4);
                    break;
                case "BoomboxOff":
                    if (uiController != null) uiController.OnBoomboxStopOff();
                    else if (VillageActionManager.Instance != null) VillageActionManager.Instance.CompleteAction(2, 4);
                    break;
                case "BoomboxNext":
                    if (uiController != null) uiController.OnBoomboxNextTrack();
                    else if (VillageActionManager.Instance != null) VillageActionManager.Instance.CompleteAction(2, 5);
                    break;
                case "ShopStart":
                    if (uiController != null) uiController.OnShopStartShopping();
                    else if (VillageActionManager.Instance != null) VillageActionManager.Instance.CompleteAction(3, 1);
                    break;
                case "ShopBottlePrice":
                case "ShopCategory":
                    if (uiController != null) uiController.OnShopAddBottlePrice(floatParameter > 0f ? floatParameter : 2.50f);
                    else if (VillageActionManager.Instance != null) VillageActionManager.Instance.CompleteAction(3, 2);
                    break;
                case "ShopPrice":
                    if (uiController != null) uiController.OnShopAddGroceryPrice(floatParameter);
                    else if (VillageActionManager.Instance != null) VillageActionManager.Instance.CompleteAction(3, 5);
                    break;
                case "ShopPay":
                    if (uiController != null) uiController.OnShopPayAndConfirm();
                    else if (VillageActionManager.Instance != null) VillageActionManager.Instance.CompleteAction(3, 9);
                    break;
                case "MechanicHorn":
                    if (uiController != null) uiController.OnMechanicTestHorn();
                    else
                    {
                        AudioSource.PlayClipAtPoint(ProceduralAudioGenerator.CreateCarHorn(0.75f), transform.position, 1.0f);
                        if (VillageActionManager.Instance != null) VillageActionManager.Instance.CompleteAction(4, 9);
                    }
                    break;
                case "MechanicEngine":
                    if (uiController != null) uiController.OnMechanicTestEngine();
                    else
                    {
                        AudioSource.PlayClipAtPoint(ProceduralAudioGenerator.CreateEngineRev(1.8f), transform.position, 1.0f);
                        if (VillageActionManager.Instance != null) VillageActionManager.Instance.CompleteAction(4, 10);
                    }
                    break;
                case "FlashlightToggle":
                    if (uiController != null) uiController.OnFlashlightToggleSwitch();
                    else if (VillageActionManager.Instance != null) VillageActionManager.Instance.CompleteAction(5, 4);
                    break;
                case "DrinkAlmond":
                    if (targetAlmondBottle != null)
                        targetAlmondBottle.DrinkBottle();
                    else if (uiController != null)
                        uiController.OnDrinkAlmondWaterHelper();
                    else
                        AlmondWaterBottle.DrinkStaticFallback(transform.position);
                    break;
                case "TestPlayerFoundSound":
                    var monster = UnityEngine.Object.FindAnyObjectByType<BackroomsMonsterAI>();
                    if (monster != null) monster.TestPlayPlayerFoundSound();
                    break;
                case "ForceSpawnMonster":
                    if (BackroomsProceduralGenerator.Instance != null)
                        BackroomsProceduralGenerator.Instance.SpawnMonster("Terminal Button Pressed");
                    break;
            }
        }
    }
}

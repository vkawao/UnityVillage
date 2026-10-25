using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace NarinVillage
{
    /// <summary>
    /// Manages the Wrist Menu UI canvas.
    /// - Automatically docks to the active Left Controller or Left Hand in the scene.
    /// - Guarantees correct localPosition (-0.02, 0.08, -0.06), localRotation (50, 0, 0), and scale (0.0006).
    /// - Sets Canvas.worldCamera = Camera.main and ensures TrackedDeviceGraphicRaycaster is enabled.
    /// - If in XR Interaction Simulator without controllers active, floats as an accessible HUD in view.
    /// - Keeps God Mode, Entity AI Pause, and Teleport buttons registered and operational.
    /// </summary>
    public class WristUIController : MonoBehaviour
    {
        [Header("Status & Text References")]
        public Text statusText;
        public Text godModeText;
        public Text pauseEntityText;

        private Canvas _canvas;
        private Transform _leftTarget;
        private bool _isDockedToHand = false;
        private bool _manuallyOpened = false;
        private float _searchTimer = 0f;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas != null)
            {
                _canvas.renderMode = RenderMode.WorldSpace;
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            if (GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            {
                var tdgr = gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                tdgr.ignoreReversedGraphics = false;
            }
        }

        private void Start()
        {
            EnsureWorldCamera();
            RegisterWithActionManager();
            TryDockToLeftController();
            if (!_isDockedToHand && _canvas != null)
            {
                _canvas.enabled = false;
            }
        }

        private void Update()
        {
            EnsureWorldCamera();

            // Keyboard shortcut 'M' toggles wrist canvas visibility in editor / simulator
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame)
            {
                _manuallyOpened = !_manuallyOpened;
                if (_canvas != null) _canvas.enabled = _manuallyOpened;
            }
#endif

            // Periodic search for Left Controller if not yet docked or if controller was swapped
            _searchTimer += Time.deltaTime;
            if (_searchTimer > 1.0f)
            {
                _searchTimer = 0f;
                if (!_isDockedToHand || _leftTarget == null || !_leftTarget.gameObject.activeInHierarchy)
                {
                    TryDockToLeftController();
                }
            }

            // If untracked, only position in peripheral view IF user explicitly enabled canvas via 'M' key
            if (!_isDockedToHand)
            {
                if (_canvas != null && _canvas.enabled && Camera.main != null)
                {
                    Transform cam = Camera.main.transform;
                    Vector3 targetPos = cam.position + cam.forward * 0.90f - cam.up * 0.35f - cam.right * 0.40f;
                    transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 6f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(transform.position - cam.position), Time.deltaTime * 6f);
                    transform.localScale = Vector3.one * 0.0006f;
                }
            }

            // Update live progress label
            if (statusText != null && VillageActionManager.Instance != null)
            {
                int total = VillageActionManager.Instance.GetTotalCompletedCount();
                statusText.text = $"<b>PROGRESS:</b> {total}/50 ACTIONS COMPLETED";
            }
        }

        private void EnsureWorldCamera()
        {
            if (_canvas != null && _canvas.worldCamera == null && Camera.main != null)
            {
                _canvas.worldCamera = Camera.main;
            }
        }

        public void TryDockToLeftController()
        {
            Transform foundTarget = FindLeftControllerTransform();
            if (foundTarget != null)
            {
                _leftTarget = foundTarget;
                transform.SetParent(foundTarget, false);
                transform.localPosition = new Vector3(-0.02f, 0.08f, -0.06f);
                transform.localRotation = Quaternion.Euler(50f, 0f, 0f);
                transform.localScale = Vector3.one * 0.0006f;
                _isDockedToHand = true;

                RectTransform rt = GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(360f, 310f);
                }

                if (_canvas != null)
                {
                    _canvas.enabled = true;
                }
                Debug.Log($"[VRVillage] WristDebugCanvas docked cleanly to {foundTarget.name}");
            }
            else
            {
                _isDockedToHand = false;
                if (_canvas != null && !_manuallyOpened)
                {
                    _canvas.enabled = false;
                }
            }
        }

        private Transform FindLeftControllerTransform()
        {
            XROrigin origin = FindFirstObjectByType<XROrigin>();
            Transform searchRoot = (origin != null) ? origin.transform : null;

            if (searchRoot != null)
            {
                Transform left = FindDeepChild(searchRoot, "Left Controller") ??
                                 FindDeepChild(searchRoot, "LeftHand") ??
                                 FindDeepChild(searchRoot, "Left Hand") ??
                                 FindDeepChild(searchRoot, "LeftHandDirectInteraction");

                if (left != null && left.gameObject.activeInHierarchy)
                    return left;
            }

            // Scene-wide fallback
            GameObject leftObj = GameObject.Find("Left Controller") ?? GameObject.Find("LeftHand");
            if (leftObj != null && leftObj.activeInHierarchy)
                return leftObj.transform;

            return null;
        }

        private Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), childName);
                if (found != null) return found;
            }
            return null;
        }

        private void RegisterWithActionManager()
        {
            if (VillageActionManager.Instance == null) return;

            if (godModeText != null && !VillageActionManager.Instance.godModeButtonTexts.Contains(godModeText))
            {
                VillageActionManager.Instance.godModeButtonTexts.Add(godModeText);
                VillageActionManager.Instance.godModeButtonText = godModeText;
            }

            if (pauseEntityText != null && !VillageActionManager.Instance.monsterPauseButtonTexts.Contains(pauseEntityText))
            {
                VillageActionManager.Instance.monsterPauseButtonTexts.Add(pauseEntityText);
            }

            VillageActionManager.Instance.RefreshAllUI();
        }
    }
}
